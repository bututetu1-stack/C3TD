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
        Add(tips, enemies, e => e.aura != null || e.heal != null, "まわりを強くする・回復する敵", "狙撃・術師で先に倒す", airAttack);
        Add(tips, enemies, e => e.shield > 0f, "盾を持つ敵", "必殺技を重ねて一気に割る", null);
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

    /// <summary>
    /// その敵の倒し方（初めて会った敵の札の「倒し方」と、負けたときの結果のヒントに出す）。
    /// どちらでも読めるよう、起きたことではなく、することを書く
    /// </summary>
    public static string Advice(EnemyData e, bool withName = false)
    {
        if (e == null) return "";
        string core = Core(e);
        if (!core.EndsWith("。")) core += "。";
        if (!withName) return core;
        return e.name + (core.StartsWith("道に") ? "は、" : "は") + core;
    }

    private static string Core(EnemyData e)
    {
        if (e.boss)
            return "とても硬いボス。必殺技を取っておき、ボスが来たら重ねて使おう" + (e.summon != null ? "。呼ばれた手下も止められるよう、道に前衛・重装を置こう" : "");
        if (e.flying && e.res >= 40f) return "空を飛ぶうえ、術が効きにくい。狙撃を飛ぶ道（水色の矢印）の近くに置こう";
        if (e.flying) return "空を飛ぶので、前衛・重装では止められない。狙撃・術師を飛ぶ道（水色の矢印）の近くに置こう";
        if (e.aura != null) return "まわりの敵を強くする。狙撃・術師で先に倒すと、ほかの敵が弱くなる";
        if (e.heal != null) return "まわりの敵を回復する。先に倒すか、術師の範囲攻撃でまとめて削ろう";
        if (e.shield > 0f) return "体力より先に盾が減る。攻撃力の高い部員や必殺技で一気に盾を割ろう";
        if (e.split != null) return "倒すと小さな敵に分かれる。分かれた敵も止められるよう、道に前衛・重装を置こう";
        if (e.revive > 0) return "倒しても起き上がる。止めたまま、起き上がったところをもう一度倒そう";
        if (e.dodge > 0f) return "術師を道の近くの高台に置こう。術はかわされない";
        if (e.def >= 300 && e.res >= 40f) return "とても硬い。重装で長く止めて、まわりの部員みんなで攻撃しよう";
        if (e.def >= 300) return "止め役のそばの高台に術師を置こう。狙撃・前衛の攻撃はあまり通らない";
        if (e.res >= 40f) return "狙撃・前衛の物理で削ろう。術師の術はあまり効かない";
        if (e.blink != null) return "止められていないと先へ跳ぶ。前衛・重装で止めよう";
        if (e.accelerate != null) return "だんだん速くなる。出てきたら早めに止めよう";
        if (e.speed >= 1.5f) return "足が速い。前衛・重装で止めてから攻撃しよう";
        if (e.range > 0f) return "離れた部員を撃つ。医療で回復しながら、狙撃・術師で先に倒そう";
        if (e.dpDrain > 0f) return "いるあいだは DP がたまりにくい。早めに倒そう";
        return "道に前衛・重装を置いて止め、まわりの高台から攻撃しよう";
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
