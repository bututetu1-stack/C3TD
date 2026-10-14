using System.Collections.Generic;
using UnityEngine;

/// <summary>強化の1つ（Data/upgrades.json）</summary>
public class UpgradeData
{
    public string id;
    public string name;

    /// <summary>効果の文（{0} に合計の量が入る）</summary>
    public string text;

    /// <summary>効く職業（UnitClass の名前。空なら全員）</summary>
    public List<string> classes = new List<string>();

    /// <summary>hp / atk / dp / redeploy</summary>
    public string stat;
    public float perLevel;

    /// <summary>段ごとに使う★（数が段の上限）</summary>
    public List<int> costs = new List<int>();

    public int MaxLevel { get { return costs != null ? costs.Count : 0; } }

    public string Describe(int level)
    {
        return string.Format(text ?? "", (perLevel * level).ToString("0.#"));
    }
}

public class UpgradesData
{
    public List<UpgradeData> upgrades = new List<UpgradeData>();
}

/// <summary>
/// 強化（Kingdom Rush の★で買う強化）。作戦の★と強襲のクリア（1つにつき★1）を使って、部員を少し強くする。
/// 段の記録は Progress、決まりは Data/upgrades.json。BalanceRun の間は Enabled = false で効かないようにする（バランスは強化なしで測る）。
/// </summary>
public static class Upgrades
{
    /// <summary>false のあいだは強化が効かない（自動で戦わせてバランスを測るとき）</summary>
    public static bool Enabled = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Enabled = true;
    }

    public static List<UpgradeData> All { get { return GameData.Upgrades.upgrades; } }

    /// <summary>体力の倍率（その職業）</summary>
    public static float HpScale(UnitClass c) { return 1f + Sum("hp", c) / 100f; }

    /// <summary>攻撃力の倍率（医療なら回復量）</summary>
    public static float AtkScale(UnitClass c) { return 1f + Sum("atk", c) / 100f; }

    /// <summary>はじめの DP に足す量</summary>
    public static int ExtraDp() { return Mathf.RoundToInt(Sum("dp", null)); }

    /// <summary>再配置までの時間の倍率</summary>
    public static float RedeployScale() { return Mathf.Max(0.2f, 1f - Sum("redeploy", null) / 100f); }

    private static float Sum(string stat, UnitClass? cls)
    {
        if (!Enabled) return 0f;
        float total = 0f;
        foreach (UpgradeData u in All)
        {
            if (u.stat != stat) continue;
            if (cls.HasValue && u.classes != null && u.classes.Count > 0 && !u.classes.Contains(cls.Value.ToString())) continue;
            total += u.perLevel * Progress.UpgradeLevel(u.id);
        }
        return total;
    }

    /// <summary>手に入れた★：作戦の★の合計と、強襲でクリアした作戦の数</summary>
    public static int StarsEarned()
    {
        int stars = 0;
        foreach (string id in GameData.Campaign.AllStages())
        {
            stars += Progress.Stars(id);
            if (Progress.IsHardCleared(id)) stars++;
        }
        return stars;
    }

    public static int StarsSpent()
    {
        int spent = 0;
        foreach (UpgradeData u in All)
        {
            int level = Progress.UpgradeLevel(u.id);
            for (int i = 0; i < level && i < u.costs.Count; i++) spent += u.costs[i];
        }
        return spent;
    }

    public static int StarsLeft() { return StarsEarned() - StarsSpent(); }

    /// <summary>次の段の★（もう上がらなければ -1）</summary>
    public static int NextCost(UpgradeData u)
    {
        int level = Progress.UpgradeLevel(u.id);
        return level < u.MaxLevel ? u.costs[level] : -1;
    }

    /// <summary>1 段上げる（★が足りなければ false）</summary>
    public static bool Buy(UpgradeData u)
    {
        int cost = NextCost(u);
        if (cost < 0 || cost > StarsLeft()) return false;
        Progress.SetUpgradeLevel(u.id, Progress.UpgradeLevel(u.id) + 1);
        return true;
    }

    /// <summary>全部の段を 0 に戻す（★は戻る）</summary>
    public static void ResetAll()
    {
        foreach (UpgradeData u in All) Progress.SetUpgradeLevel(u.id, 0);
    }
}
