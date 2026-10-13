using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 開発用：出撃中の戦闘を自動で戦う（バランスの確かめ用）。
/// 近接職は経路の合流点の拠点寄りに、遠距離職は経路を多く射程に入れられる高台に置き、
/// 必殺技は溜まったらすぐ使う。人が上手に遊ぶよりは下手なので、これで楽に勝てるなら簡単すぎる目安になる。
/// エディタから Battle の GameObject に AddComponent して使う。
/// </summary>
public class AutoPilot : MonoBehaviour
{
    /// <summary>必殺技を使うか</summary>
    public bool UseSkills = true;

    /// <summary>0 より大きければ、毎フレームこの速さにする（早送りで確かめる用）</summary>
    public float TimeScale;

    /// <summary>
    /// true なら初めて遊ぶ人のまね。DP が足りる人と置く場所を Seed でばらばらに選ぶ
    /// （場所は、いちばん良い場所の点数の NoviceCut 倍以上のところから）。置いたあと NoviceDelay 秒は次を置かない。
    /// </summary>
    public bool Novice;
    public int Seed;
    public float NoviceCut = 0.6f;
    public float NoviceDelay = 2f;

    /// <summary>置く順番（職業）</summary>
    private static readonly UnitClass[] Order =
        { UnitClass.Guard, UnitClass.Sniper, UnitClass.Defender, UnitClass.Caster, UnitClass.Medic };

    private Battle battle;
    private System.Random rng;
    private float noviceWait;
    // 経路のマスごとの「通る経路の数」と「拠点への近さ（0〜1）」
    private readonly Dictionary<Vector2Int, int> pathCount = new Dictionary<Vector2Int, int>();
    private readonly Dictionary<Vector2Int, float> progress = new Dictionary<Vector2Int, float>();

    void Start()
    {
        battle = GetComponent<Battle>();
        foreach (var kv in battle.Stage.paths)
        {
            List<Vector2Int> cells = Expand(kv.Value);
            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int c = cells[i];
                pathCount[c] = (pathCount.TryGetValue(c, out int n) ? n : 0) + 1;
                float p = cells.Count > 1 ? i / (float)(cells.Count - 1) : 1f;
                progress[c] = Mathf.Max(progress.TryGetValue(c, out float q) ? q : 0f, p);
            }
        }

        rng = new System.Random(Seed);
    }

    private static List<Vector2Int> Expand(List<Vector2Int> points)
    {
        var cells = new List<Vector2Int>();
        for (int i = 0; i < points.Count; i++)
        {
            if (i == 0) { cells.Add(points[0]); continue; }
            Vector2Int a = points[i - 1], b = points[i];
            Vector2Int step = new Vector2Int(System.Math.Sign(b.x - a.x), System.Math.Sign(b.y - a.y));
            for (Vector2Int c = a + step; c != b + step; c += step) cells.Add(c);
        }
        return cells;
    }

    void Update()
    {
        if (battle == null || battle.State.Phase != GamePhase.Playing) return;
        if (TimeScale > 0f) Time.timeScale = TimeScale;

        if (UseSkills)
            foreach (Character c in battle.Characters.ToArray())
                if (c != null && c.Alive && c.Skills != null && c.Skills.HasUltimate && c.Skills.Ready) c.Skills.Activate();

        if (Novice)
        {
            DeployNovice();
            return;
        }

        // 優先の高い順に、置ける人を1人置く。優先の高い人の DP が足りないなら貯める
        foreach (UnitClass cls in Order)
        {
            foreach (SquadMember m in battle.Squad)
            {
                if (m.Data.unitClass != cls || !m.Ready) continue;
                if (cls == UnitClass.Medic && !HasMelee()) continue;
                if (!battle.CanAfford(m)) return;
                Vector2Int? cell = BestCell(m);
                if (cell.HasValue && battle.Deploy(m, cell.Value) != null) return;
            }
        }
    }

    /// <summary>いま DP が足りる人からばらばらに1人選んで、まあまあ良い場所のどこかへ置く</summary>
    private void DeployNovice()
    {
        noviceWait -= Time.deltaTime;
        if (noviceWait > 0f) return;
        var ready = new List<SquadMember>();
        foreach (SquadMember m in battle.Squad)
            if (m.Ready && battle.CanAfford(m)) ready.Add(m);
        while (ready.Count > 0)
        {
            SquadMember m = ready[rng.Next(ready.Count)];
            ready.Remove(m);
            var cells = new List<Vector2Int>();
            var scores = new List<float>();
            float best = 0f;
            foreach (Vector2Int cell in battle.PlaceableCells(m))
            {
                float s = Score(m, cell);
                if (s <= 0f) continue;
                cells.Add(cell);
                scores.Add(s);
                best = Mathf.Max(best, s);
            }
            for (int i = cells.Count - 1; i >= 0; i--)
                if (scores[i] < best * NoviceCut) cells.RemoveAt(i);
            if (cells.Count == 0) continue;
            if (battle.Deploy(m, cells[rng.Next(cells.Count)]) != null) noviceWait = NoviceDelay;
            return;
        }
    }

    private bool HasMelee()
    {
        foreach (Character c in battle.Characters)
            if (c.Class.placement == Placement.Ground) return true;
        return false;
    }

    private Vector2Int? BestCell(SquadMember m)
    {
        Vector2Int? best = null;
        float bestScore = float.MinValue;
        foreach (Vector2Int cell in battle.PlaceableCells(m))
        {
            float s = Score(m, cell);
            if (s > bestScore) { bestScore = s; best = cell; }
        }
        return bestScore > 0f ? best : null;
    }

    private float Score(SquadMember m, Vector2Int cell)
    {
        if (m.Class.placement == Placement.Ground)
        {
            // 合流点の、拠点の少し手前
            if (!pathCount.TryGetValue(cell, out int n)) return 0f;
            float p = progress[cell];
            return n * 100f + p * 40f - (p > 0.95f ? 30f : 0f);
        }

        float range = m.Data.stats.range;
        float score = 0f;
        if (m.Class.attack == AttackKind.Heal)
        {
            foreach (Character c in battle.Characters)
                if (Combat.InRange(cell, c.Cell, range)) score += c.Class.placement == Placement.Ground ? 30f : 5f;
            return score;
        }
        foreach (var kv in pathCount)
        {
            if (!Combat.InRange(cell, kv.Key, range)) continue;
            score += kv.Value * (1f + progress[kv.Key]);
            Character occ = battle.CharacterAt(kv.Key);
            if (occ != null && occ.Class.placement == Placement.Ground) score += 6f;
        }
        return score;
    }
}
