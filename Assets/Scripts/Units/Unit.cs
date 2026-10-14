using System.Collections.Generic;
using UnityEngine;

/// <summary>能力値の増減（バフ・デバフ）1つ。最終値 = 基本値 ×（1 + 割合の合計/100）+ 加算の合計</summary>
public class StatModifier
{
    public string Stat;      // atk / def / attackInterval / range / hits
    public float Percent;
    public float Value;
    public float Remaining;  // 残り秒（Timed のときだけ減る）
    public bool Timed;
    public object Source;    // AddOrRefreshModifier で、同じ出どころの増減を重ねないために使う
}

/// <summary>
/// 戦うもの（部員と敵）の共通部分。体力・被ダメージ・回復・HP バー・能力値の増減・スタン。
/// </summary>
public abstract class Unit : MonoBehaviour
{
    public float Hp { get; protected set; }
    public float MaxHp { get; protected set; }
    public bool Alive { get; protected set; }

    /// <summary>スタンの残り秒（0 より大きいあいだは動けない・攻撃できない）</summary>
    public float StunRemaining { get; private set; }
    public bool Stunned { get { return StunRemaining > 0f; } }

    public float Atk { get { return ApplyStat("atk", BaseAtk); } }
    public float Def { get { return ApplyStat("def", BaseDef); } }
    public virtual float Res { get { return 0f; } }

    protected abstract float BaseAtk { get; }
    protected abstract float BaseDef { get; }

    /// <summary>弾の当たる位置・演出を出す位置</summary>
    public virtual Vector3 Center
    {
        get
        {
            Vector3 p = transform.position;
            return new Vector3(p.x, p.y + 0.08f, 0f);
        }
    }

    protected Battle battle;
    protected HealthBar healthBar;
    private readonly List<StatModifier> modifiers = new List<StatModifier>();

    protected void InitHealth(float maxHp)
    {
        MaxHp = Mathf.Max(1f, maxHp);
        Hp = MaxHp;
        Alive = true;
    }

    // ================= 能力値の増減・スタン =================

    /// <summary>duration が 0 以下なら、外すまでずっと効く</summary>
    public StatModifier AddModifier(string stat, float percent, float value, float duration)
    {
        var m = new StatModifier { Stat = stat, Percent = percent, Value = value, Remaining = duration, Timed = duration > 0f };
        modifiers.Add(m);
        return m;
    }

    /// <summary>
    /// 同じ出どころ（source）・同じ能力の増減がもう付いていれば、残り時間を戻すだけにする（重ねない）。
    /// 付けたときは true を返す（演出を出すかどうかに使う）
    /// </summary>
    public bool AddOrRefreshModifier(object source, string stat, float percent, float duration)
    {
        foreach (StatModifier m in modifiers)
        {
            if (m.Source != source || m.Stat != stat) continue;
            m.Remaining = Mathf.Max(m.Remaining, duration);
            return false;
        }
        StatModifier added = AddModifier(stat, percent, 0f, duration);
        added.Source = source;
        return true;
    }

    public void RemoveModifier(StatModifier m)
    {
        modifiers.Remove(m);
    }

    /// <summary>ある能力値に掛かっている割合（%）の合計</summary>
    public float PercentOf(string stat)
    {
        float p = 0f;
        foreach (StatModifier m in modifiers)
            if (m.Stat == stat) p += m.Percent;
        return p;
    }

    protected float ApplyStat(string stat, float baseValue)
    {
        float percent = 0f, value = 0f;
        foreach (StatModifier m in modifiers)
        {
            if (m.Stat != stat) continue;
            percent += m.Percent;
            value += m.Value;
        }
        percent += ExtraPercent(stat);
        return Mathf.Max(0f, baseValue * (1f + percent / 100f) + value);
    }

    /// <summary>増減のほかに足す%（部員なら、味方全員に掛かる Battle.TeamAtkPercent）</summary>
    protected virtual float ExtraPercent(string stat)
    {
        return 0f;
    }

    public void Stun(float seconds)
    {
        if (!Alive || seconds <= 0f) return;
        StunRemaining = Mathf.Max(StunRemaining, seconds);
    }

    /// <summary>スタンと、時間つきの増減を進める（毎フレーム呼ぶ）</summary>
    protected void TickStatus(float dt)
    {
        if (StunRemaining > 0f) StunRemaining = Mathf.Max(0f, StunRemaining - dt);
        for (int i = modifiers.Count - 1; i >= 0; i--)
        {
            StatModifier m = modifiers[i];
            if (!m.Timed) continue;
            m.Remaining -= dt;
            if (m.Remaining <= 0f) modifiers.RemoveAt(i);
        }
    }

    // ================= 体力 =================

    /// <summary>攻撃を受ける。防御・術耐性で減らしたあとの、実際に減った量を返す</summary>
    public float TakeDamage(float atk, DamageType type)
    {
        if (!Alive) return 0f;
        if (Evade(type)) return 0f;
        float damage = Absorb(Combat.Mitigate(atk, type, Def, Res));
        if (damage <= 0f)
        {
            OnDamaged();
            return 0f;
        }
        Hp = Mathf.Max(0f, Hp - damage);
        if (healthBar != null) healthBar.Set(Hp / MaxHp);
        FloatingText.Damage(Center, damage, DamageColor(type));
        OnDamaged();
        if (Hp <= 0f)
        {
            Alive = false;
            OnDeath();
        }
        return damage;
    }

    /// <summary>回復する。実際に増えた量を返す</summary>
    public float Heal(float amount)
    {
        if (!Alive) return 0f;
        float before = Hp;
        Hp = Mathf.Min(MaxHp, Hp + amount);
        if (healthBar != null) healthBar.Set(Hp / MaxHp);
        float healed = Hp - before;
        if (healed >= 1f) FloatingText.Heal(Center, healed);
        return healed;
    }

    /// <summary>倒されたあとに起き上がる（OnDeath の中から呼ぶ）</summary>
    protected void Revive(float hp)
    {
        Hp = Mathf.Clamp(hp, 1f, MaxHp);
        Alive = true;
        if (healthBar != null) healthBar.Set(Hp / MaxHp);
    }

    /// <summary>撤退・拠点到達などで、倒されずに場から消える</summary>
    public void Despawn()
    {
        Alive = false;
        Destroy(gameObject);
    }

    /// <summary>体力が減る前に受け止める（敵の盾など）。体力から減らす残りを返す</summary>
    protected virtual float Absorb(float damage)
    {
        return damage;
    }

    /// <summary>攻撃をかわすか（かわしたらダメージを受けない）。敵の dodge で使う</summary>
    protected virtual bool Evade(DamageType type)
    {
        return false;
    }

    protected abstract Color DamageColor(DamageType type);
    protected virtual void OnDamaged() { }
    protected abstract void OnDeath();
}
