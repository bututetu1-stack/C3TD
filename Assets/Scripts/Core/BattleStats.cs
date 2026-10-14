using System.Collections.Generic;
using UnityEngine;

/// <summary>1人の部員のこの出撃での活躍（召喚ユニットの分は呼んだ部員に足す）</summary>
public class MemberStats
{
    public CharacterData Data;
    public ClassData Class;
    public int Order;

    /// <summary>敵に与えたダメージ（盾で受け止められた分も含む）</summary>
    public float Damage;
    public float Heal;

    /// <summary>敵から受けたダメージ</summary>
    public float Taken;

    /// <summary>敵を止めていた秒</summary>
    public float BlockTime;
    public int Kills;
    public int Deploys;

    /// <summary>MVP を決める点（与えた・回復した量と、受け止めた量を少し）</summary>
    public float Score
    {
        get { return Damage + Heal + Taken * 0.6f; }
    }
}

/// <summary>
/// 出撃中の部員ごとの活躍を数える（結果の画面で「部員の活躍」と MVP を出す）。
/// Unit.TakeDamage・Heal に攻撃した・回復した部員（source）を渡すと記録される。
/// </summary>
public class BattleStats
{
    private readonly Dictionary<string, MemberStats> members = new Dictionary<string, MemberStats>();

    public BattleStats(List<SquadMember> squad)
    {
        for (int i = 0; i < squad.Count; i++)
        {
            SquadMember m = squad[i];
            if (!members.ContainsKey(m.Data.id)) members[m.Data.id] = new MemberStats { Data = m.Data, Class = m.Class, Order = i };
        }
    }

    /// <summary>編成の順に、一度でも置いた部員</summary>
    public List<MemberStats> Deployed()
    {
        var list = new List<MemberStats>();
        foreach (MemberStats s in members.Values)
            if (s.Deploys > 0) list.Add(s);
        list.Sort((a, b) => a.Order.CompareTo(b.Order));
        return list;
    }

    /// <summary>いちばん活躍した部員（だれも何もしていなければ null）</summary>
    public MemberStats Mvp()
    {
        MemberStats best = null;
        foreach (MemberStats s in members.Values)
            if (s.Score > 0f && (best == null || s.Score > best.Score)) best = s;
        return best;
    }

    /// <summary>召喚ユニットなら呼んだ部員の記録</summary>
    private MemberStats Of(Unit unit)
    {
        var c = unit as Character;
        if (c == null) return null;
        int guard = 0;
        while (c.Summoner != null && guard++ < 8) c = c.Summoner;
        MemberStats s;
        return c.Member != null && members.TryGetValue(c.Member.Data.id, out s) ? s : null;
    }

    public void Deploy(SquadMember member)
    {
        MemberStats s;
        if (member != null && members.TryGetValue(member.Data.id, out s)) s.Deploys++;
    }

    /// <summary>source が target に amount のダメージを与えた（killed なら倒した）</summary>
    public void Hit(Unit source, Unit target, float amount, bool killed)
    {
        if (amount <= 0f) return;
        MemberStats by = Of(source);
        if (by != null && target is Enemy)
        {
            by.Damage += amount;
            if (killed) by.Kills++;
        }
        MemberStats taken = Of(target);
        if (taken != null && source is Enemy) taken.Taken += amount;
    }

    public void Healed(Unit source, float amount)
    {
        MemberStats by = Of(source);
        if (by != null && amount > 0f) by.Heal += amount;
    }

    public void Block(Character c, float dt)
    {
        MemberStats s = Of(c);
        if (s != null) s.BlockTime += dt;
    }
}
