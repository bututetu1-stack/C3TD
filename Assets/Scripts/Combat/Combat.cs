using System.Collections.Generic;
using UnityEngine;

/// <summary>狙う敵の選び方（Bloons の First / Last / Strong / Close）。止めている敵がいれば、どれでもその敵が先</summary>
public enum TargetMode
{
    First,  // 先頭：いちばん拠点に近い敵（ふつう）
    Last,   // 最後尾：いちばん拠点から遠い敵
    Strong, // 強い：いまの体力がいちばん多い敵
    Close,  // 近い：自分にいちばん近い敵
}

/// <summary>ダメージの計算と、狙う相手の選び方、射程のマス</summary>
public static class Combat
{
    /// <summary>狙い方の日本語（部員の札のボタン）</summary>
    public static string TargetName(TargetMode mode)
    {
        switch (mode)
        {
            case TargetMode.Last: return "最後尾";
            case TargetMode.Strong: return "強い敵";
            case TargetMode.Close: return "近い敵";
            default: return "先頭";
        }
    }

    /// <summary>
    /// 物理：攻撃力 − 防御力、術：攻撃力 ×（1 − 術耐性%）。
    /// どちらも攻撃力の minDamageRate（5%）より小さくはならない（アークナイツと同じ）。
    /// </summary>
    public static float Mitigate(float atk, DamageType type, float def, float res)
    {
        float min = atk * GameData.Rules.minDamageRate;
        float damage = type == DamageType.Arts
            ? atk * (1f - Mathf.Clamp(res, 0f, 100f) / 100f)
            : atk - def;
        return Mathf.Max(damage, min);
    }

    /// <summary>マスの中心どうしの距離が range 以下なら射程内（Phase 2 で向きを入れるまでは全方向）</summary>
    public static bool InRange(Vector2Int from, Vector2Int to, float range)
    {
        Vector2Int d = to - from;
        return d.x * d.x + d.y * d.y <= range * range + 0.001f;
    }

    public static List<Vector2Int> CellsInRange(StageMap map, Vector2Int center, float range)
    {
        var cells = new List<Vector2Int>();
        int r = Mathf.CeilToInt(range);
        for (int dy = -r; dy <= r; dy++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                var cell = new Vector2Int(center.x + dx, center.y + dy);
                if (map.InBounds(cell) && InRange(center, cell, range)) cells.Add(cell);
            }
        }
        return cells;
    }

    /// <summary>
    /// 攻撃する敵：自分が止めている敵を先に、なければ射程内から狙い方（Targeting）で選ぶ（ふつうはいちばん拠点に近い敵）。
    /// 近接の部員は飛ぶ敵を狙えない（antiAir の部員は狙える）
    /// </summary>
    public static Enemy FindEnemyTarget(Battle battle, Character c)
    {
        foreach (Enemy e in c.Blocked)
            if (e != null && e.Alive) return e;

        bool melee = c.Class.attack == AttackKind.Melee && !c.Data.antiAir;
        TargetMode mode = c.Targeting;
        Enemy best = null;
        float bestScore = float.MaxValue;
        Vector3 at = c.transform.position;
        foreach (Enemy e in battle.Enemies)
        {
            if (!e.Alive) continue;
            if (melee && e.Data.flying) continue;
            if (!InRange(c.Cell, battle.Map.WorldToCell(e.transform.position), c.Range)) continue;
            // 小さいほど狙う。同じなら拠点に近い敵
            float score;
            switch (mode)
            {
                case TargetMode.Last: score = -e.RemainingDistance; break;
                case TargetMode.Strong: score = -e.Hp * 1000f + e.RemainingDistance; break;
                case TargetMode.Close: score = (e.transform.position - at).sqrMagnitude * 1000f + e.RemainingDistance; break;
                default: score = e.RemainingDistance; break;
            }
            if (score < bestScore)
            {
                bestScore = score;
                best = e;
            }
        }
        return best;
    }

    /// <summary>回復する味方：射程内で、体力の割合がいちばん低い味方（自分も含む）</summary>
    public static Character FindHealTarget(Battle battle, Character healer)
    {
        Character best = null;
        float bestRatio = 0.999f;
        foreach (Character c in battle.Characters)
        {
            if (!c.Alive || !InRange(healer.Cell, c.Cell, healer.Range)) continue;
            float ratio = c.Hp / c.MaxHp;
            if (ratio < bestRatio)
            {
                bestRatio = ratio;
                best = c;
            }
        }
        return best;
    }
}
