using System.Collections.Generic;

/// <summary>
/// 作戦の注意（編成画面に出す）。ステージに出てくる敵の性質から、どんな部員がいるとよいかを出し、
/// いまの編成でまかなえていなければ「！」にする（飛ぶ敵がいるのに狙撃・術師がいない、など）。
/// </summary>
public static class StageTips
{
    public class Tip
    {
        public string Text;

        /// <summary>true：まかなえている、false：まかなえていない、null：知らせるだけ</summary>
        public bool? Covered;
    }

    public static List<Tip> For(StageData stage, List<string> squad)
    {
        var tips = new List<Tip>();
        if (stage == null) return tips;
        List<EnemyData> enemies = Enemies(stage);

        bool airAttack = false, arts = false, physical = false, medic = false;
        foreach (string id in squad)
        {
            CharacterData d = GameData.Character(id);
            if (d == null) continue;
            ClassData c = GameData.Class(d.unitClass);
            if (c.attack == AttackKind.Heal)
            {
                medic = true;
                continue;
            }
            if (c.attack == AttackKind.Ranged || d.antiAir) airAttack = true;
            if (c.damageType == DamageType.Arts) arts = true;
            else physical = true;
        }

        Add(tips, enemies, e => e.flying, "空を飛ぶ敵", "狙撃・術師で落とす", airAttack);
        Add(tips, enemies, e => !e.boss && e.def >= 300f, "硬い敵", "術師の術は防御が効かない", arts);
        Add(tips, enemies, e => !e.boss && e.res >= 40f, "術が効きにくい敵", "狙撃・前衛の物理で", physical);
        Add(tips, enemies, e => e.dodge > 0f, "物理をかわす敵", "術師の術は当たる", arts);
        Add(tips, enemies, e => e.range > 0f, "部員を撃つ敵", "医療で回復", medic);
        Add(tips, enemies, e => e.dpDrain > 0f, "DP を減らす敵", "早めに倒そう", null);
        Add(tips, enemies, e => e.boss, "ボス", "必殺技を取っておこう", null);
        return tips;
    }

    private static void Add(List<Tip> tips, List<EnemyData> enemies, System.Func<EnemyData, bool> match, string what, string advice, bool? covered)
    {
        var names = new List<string>();
        foreach (EnemyData e in enemies)
            if (match(e) && names.Count < 2) names.Add(e.name);
        if (names.Count == 0) return;
        tips.Add(new Tip { Text = what + "（" + string.Join("・", names) + "）：" + advice, Covered = covered });
    }

    /// <summary>ステージに出てくる敵（ウェーブ・エンドレスの組とボス・呼ばれて分かれて出る敵）</summary>
    private static List<EnemyData> Enemies(StageData stage)
    {
        var ids = new List<string>();
        foreach (WaveData w in stage.waves) ids.Add(w.enemy);
        if (stage.endless != null)
        {
            foreach (EndlessPool p in stage.endless.pools) ids.AddRange(p.enemies);
            ids.AddRange(stage.endless.bosses);
        }
        var list = new List<EnemyData>();
        var seen = new HashSet<string>();
        for (int i = 0; i < ids.Count; i++)
        {
            if (!seen.Add(ids[i])) continue;
            EnemyData e = GameData.Enemy(ids[i]);
            if (e == null) continue;
            list.Add(e);
            if (e.summon != null && !string.IsNullOrEmpty(e.summon.enemy)) ids.Add(e.summon.enemy);
            if (e.split != null && !string.IsNullOrEmpty(e.split.enemy)) ids.Add(e.split.enemy);
        }
        return list;
    }
}
