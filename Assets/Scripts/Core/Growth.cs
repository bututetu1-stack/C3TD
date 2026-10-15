using System.Collections.Generic;
using UnityEngine;

/// <summary>手に入れた物（結果の画面・ガチャの画面に出す）。id が "coin" なら部費</summary>
public class Reward
{
    public string id;
    public int count;
    public string Name { get { return id == Growth.CoinId ? "部費" : Growth.ItemName(id); } }
}

/// <summary>ガチャの1回分</summary>
public class GachaResult
{
    public EquipmentData Equipment;
    public bool IsNew;

    /// <summary>重ねた後の数（0〜4）</summary>
    public int Stacks;

    /// <summary>重ねきっていて部費に換わったときの部費（なければ 0）</summary>
    public int Coins;
}

/// <summary>
/// 育成とガチャ（ソシャゲの育成のように。決まりは Data/items.json、持ち物と部員の育ち具合は Progress）。
///   周回：クリアすると部費と章の素材が落ちる（★3・強襲で多め。負けたら部費だけ少し）
///   育成：素材と部費で部員のレベル（1〜20）を上げ、Lv10・20 で昇進する
///   ガチャ：部費で備品を引き、部員 1 人に 1 つ付ける
/// 出撃中の効き目（体力・攻撃力・防御・攻撃間隔・最初の SP・SP の量・再配置）は Character・SkillRunner・Battle が読む。
/// Upgrades.Enabled が false のあいだ（BalanceRun）は効かない。
/// </summary>
public static class Growth
{
    public const string CoinId = "coin";
    public const int MaxStacks = 4;

    private static ItemsData D { get { return GameData.Items; } }
    private static bool On { get { return Upgrades.Enabled; } }

    // ================= 名前 =================

    public static MaterialData Material(string id)
    {
        foreach (MaterialData m in D.materials) if (m.id == id) return m;
        return null;
    }

    public static EquipmentData Equipment(string id)
    {
        foreach (EquipmentData e in D.equipment) if (e.id == id) return e;
        return null;
    }

    public static string ItemName(string id)
    {
        MaterialData m = Material(id);
        return m != null ? m.name : id;
    }

    /// <summary>備品の効き目の文（重ねた分を含む）</summary>
    public static string Describe(EquipmentData e, int stacks)
    {
        if (e == null) return "";
        float v = e.value * (1f + 0.25f * stacks);
        string n = v.ToString("0.#");
        switch (e.stat)
        {
            case "hp": return "体力 +" + n + "%";
            case "atk": return "攻撃力 +" + n + "%";
            case "def": return "防御 +" + n + "%";
            case "interval": return "攻撃間隔 -" + n + "%";
            case "sp": return "必殺技の最初の SP +" + n;
            case "redeploy": return "再配置までの時間 -" + n + "%";
            default: return e.stat + " " + n;
        }
    }

    // ================= 部員の育ち具合 =================

    public static int Level(string id) { return Mathf.Max(1, Progress.MemberLevel(id)); }

    /// <summary>いまの昇進で上げられる最高のレベル</summary>
    public static int LevelCap(string id)
    {
        int promotion = Progress.MemberPromotion(id);
        if (promotion < D.growth.promotions.Count) return Mathf.Min(D.growth.maxLevel, D.growth.promotions[promotion].at);
        return D.growth.maxLevel;
    }

    /// <summary>次のレベルへ上げる費用（上げられなければ null）</summary>
    public static LevelCost NextLevelCost(string id, out int coin)
    {
        coin = 0;
        int level = Level(id);
        if (level >= LevelCap(id)) return null;
        foreach (LevelCost c in D.growth.levels)
        {
            if (level < c.from || level > c.to) continue;
            coin = Mathf.RoundToInt(c.coinPerLevel * level);
            return c;
        }
        return null;
    }

    public static bool CanLevelUp(string id)
    {
        int coin;
        LevelCost c = NextLevelCost(id, out coin);
        return c != null && Progress.Coins >= coin && (string.IsNullOrEmpty(c.item) || Progress.ItemCount(c.item) >= c.count);
    }

    public static bool LevelUp(string id)
    {
        int coin;
        LevelCost c = NextLevelCost(id, out coin);
        if (c == null || !CanLevelUp(id)) return false;
        Progress.AddCoins(-coin);
        if (!string.IsNullOrEmpty(c.item)) Progress.AddItem(c.item, -c.count);
        Progress.SetMemberGrowth(id, Level(id) + 1, Progress.MemberPromotion(id));
        return true;
    }

    /// <summary>次の昇進（いまのレベルで昇進できなくても返す。もう昇進しきっていれば null）</summary>
    public static PromotionData NextPromotion(string id)
    {
        int promotion = Progress.MemberPromotion(id);
        return promotion < D.growth.promotions.Count ? D.growth.promotions[promotion] : null;
    }

    public static bool CanPromote(string id)
    {
        PromotionData p = NextPromotion(id);
        if (p == null || Level(id) < p.at || Progress.Coins < p.coin) return false;
        foreach (ItemCount i in p.items) if (Progress.ItemCount(i.id) < i.count) return false;
        return true;
    }

    public static bool Promote(string id)
    {
        PromotionData p = NextPromotion(id);
        if (p == null || !CanPromote(id)) return false;
        Progress.AddCoins(-p.coin);
        foreach (ItemCount i in p.items) Progress.AddItem(i.id, -i.count);
        Progress.SetMemberGrowth(id, Level(id), Progress.MemberPromotion(id) + 1);
        return true;
    }

    // ================= 出撃中の効き目 =================

    /// <summary>レベル・昇進・備品で上がる %（stat は hp / atk / def / interval / redeploy）</summary>
    public static float Percent(string id, string stat)
    {
        if (!On || string.IsNullOrEmpty(id)) return 0f;
        float p = 0f;
        if (stat == "hp" || stat == "atk" || stat == "def") p += (Level(id) - 1) * D.growth.perLevel;
        int promotion = Progress.MemberPromotion(id);
        for (int i = 0; i < promotion && i < D.growth.promotions.Count; i++)
            if (stat == "atk") p += D.growth.promotions[i].atkPercent;
        EquipmentData e = Equipment(Progress.MemberEquip(id));
        if (e != null && e.stat == stat) p += e.value * (1f + 0.25f * Progress.EquipmentStacks(e.id));
        return p;
    }

    /// <summary>必殺技の最初の SP に足す量</summary>
    public static float InitialSp(string id)
    {
        if (!On || string.IsNullOrEmpty(id)) return 0f;
        float sp = 0f;
        int promotion = Progress.MemberPromotion(id);
        for (int i = 0; i < promotion && i < D.growth.promotions.Count; i++) sp += D.growth.promotions[i].initialSp;
        EquipmentData e = Equipment(Progress.MemberEquip(id));
        if (e != null && e.stat == "sp") sp += e.value * (1f + 0.25f * Progress.EquipmentStacks(e.id));
        return sp;
    }

    /// <summary>必殺技の SP の量の倍率</summary>
    public static float SpCostScale(string id)
    {
        if (!On || string.IsNullOrEmpty(id)) return 1f;
        float p = 0f;
        int promotion = Progress.MemberPromotion(id);
        for (int i = 0; i < promotion && i < D.growth.promotions.Count; i++) p += D.growth.promotions[i].spCostPercent;
        return Mathf.Max(0.3f, 1f + p / 100f);
    }

    /// <summary>体力・攻撃力・防御（強化・レベル・昇進・備品のぶん込み。編成画面の札に出す数と、出撃で使う数）</summary>
    public static void Stats(CharacterData d, out float hp, out float atk, out float def)
    {
        hp = d.stats.hp * Upgrades.HpScale(d.unitClass) * (1f + Percent(d.id, "hp") / 100f);
        atk = d.stats.atk * Upgrades.AtkScale(d.unitClass) * (1f + Percent(d.id, "atk") / 100f);
        def = d.stats.def * (1f + Percent(d.id, "def") / 100f);
    }

    // ================= 周回で落ちる物 =================

    /// <summary>出撃が終わったときに手に入る物（Progress に足して、結果の画面に出す一覧を返す）</summary>
    public static List<Reward> GrantBattle(StageData stage, bool won, int stars, bool hard, int endlessWaves)
    {
        var rewards = new List<Reward>();
        if (stage == null) return rewards;
        CampaignData campaign = GameData.Campaign;
        ChapterData chapter = campaign.ChapterOf(stage.id);
        int chapterIndex = chapter != null ? campaign.chapters.IndexOf(chapter) : 0;
        int stageIndex = chapter != null ? chapter.stages.IndexOf(stage.id) : 0;

        if (stage.endless != null)
        {
            Add(rewards, CoinId, Mathf.RoundToInt(D.coins.endlessPerWave * endlessWaves));
            // 守りきった波 3 つにつき 1 つ、それまでの章の素材がまざって落ちる
            var pool = new List<string>(D.chapterDrops.Values);
            int unlocked = Mathf.Clamp(1 + endlessWaves / 6, 1, pool.Count);
            for (int i = 0; i < endlessWaves / 3; i++) Add(rewards, pool[Random.Range(0, unlocked)], 1);
        }
        else
        {
            float coin = D.coins.baseCoins + D.coins.perChapter * chapterIndex + D.coins.perStage * stageIndex;
            if (won && stars >= 3) coin *= 1.2f;
            if (hard) coin *= 1.5f;
            if (!won) coin *= D.coins.lossRate;
            Add(rewards, CoinId, Mathf.RoundToInt(coin));
            string material;
            if (won && chapter != null && D.chapterDrops.TryGetValue(chapter.id, out material))
            {
                int count = D.materialDrop.baseCount + stageIndex / 2 + (stars >= 3 ? 1 : 0) + Random.Range(0, 2);
                if (hard) count *= 2;
                Add(rewards, material, count);
            }
        }
        foreach (Reward r in rewards)
        {
            if (r.id == CoinId) Progress.AddCoins(r.count);
            else Progress.AddItem(r.id, r.count);
        }
        return rewards;
    }

    private static void Add(List<Reward> list, string id, int count)
    {
        if (count <= 0) return;
        foreach (Reward r in list)
            if (r.id == id) { r.count += count; return; }
        list.Add(new Reward { id = id, count = count });
    }

    /// <summary>
    /// この仕組みを入れる前にクリアしていた作戦のぶんを、一度だけまとめて渡す（★と強襲もふくめ、おまけなしの量）。
    /// 渡した部費を返す（もう渡していれば 0）
    /// </summary>
    public static int GrantPastClears()
    {
        if (Progress.PastRewardsGiven) return 0;
        Progress.PastRewardsGiven = true;
        int before = Progress.Coins;
        CampaignData campaign = GameData.Campaign;
        foreach (ChapterData chapter in campaign.chapters)
        {
            int chapterIndex = campaign.chapters.IndexOf(chapter);
            for (int i = 0; i < chapter.stages.Count; i++)
            {
                string id = chapter.stages[i];
                if (!Progress.IsCleared(id)) continue;
                float coin = (D.coins.baseCoins + D.coins.perChapter * chapterIndex + D.coins.perStage * i) * (Progress.Stars(id) >= 3 ? 1.2f : 1f) * (Progress.IsHardCleared(id) ? 1.5f : 1f);
                Progress.AddCoins(Mathf.RoundToInt(coin));
                string material;
                if (D.chapterDrops.TryGetValue(chapter.id, out material))
                    Progress.AddItem(material, D.materialDrop.baseCount + i / 2 + (Progress.Stars(id) >= 3 ? 1 : 0));
            }
        }
        return Progress.Coins - before;
    }

    // ================= ガチャ =================

    /// <summary>今日のピックアップ（★3 を日ごとに順に。★3 がなければ null）</summary>
    public static EquipmentData Pickup()
    {
        var pool = D.equipment.FindAll(e => e.rarity >= 3);
        if (pool.Count == 0) return null;
        int day = (int)(System.DateTime.Now.Date - new System.DateTime(2026, 1, 1)).TotalDays;
        return pool[((day % pool.Count) + pool.Count) % pool.Count];
    }

    private static string Today { get { return System.DateTime.Now.ToString("yyyy-MM-dd"); } }

    /// <summary>今日の無料の 1 回がまだあるか</summary>
    public static bool FreePullAvailable()
    {
        return D.gacha.freeDaily && Progress.FreePullDate != Today;
    }

    /// <summary>天井まであと何回（その回で★3 確定）。天井がなければ -1</summary>
    public static int PityLeft()
    {
        int max = D.gacha.pity;
        return max > 0 ? Mathf.Max(1, max - Progress.GachaPity) : -1;
    }

    /// <summary>count 回引く（部費が足りなければ空）。10 回なら最後の 1 回は★2 以上。天井あり</summary>
    public static List<GachaResult> Pull(int count, bool free = false)
    {
        var results = new List<GachaResult>();
        int cost = count >= 10 ? D.gacha.cost10 : D.gacha.cost * count;
        if (free)
        {
            if (!FreePullAvailable()) return results;
            count = 1;
            cost = 0;
        }
        if (count <= 0 || Progress.Coins < cost || D.equipment.Count == 0) return results;
        if (free) Progress.FreePullDate = Today;
        Progress.AddCoins(-cost);
        EquipmentData pickup = Pickup();
        bool high = false;
        int pity = Progress.GachaPity;
        int pityMax = D.gacha.pity;
        for (int i = 0; i < count; i++)
        {
            int rarity = RollRarity();
            if (count >= 10 && i == count - 1 && !high) rarity = Mathf.Max(rarity, 2);
            // 天井：★3 が出ないまま pity 回引いていたら、次は★3
            if (pityMax > 0 && pity >= pityMax - 1) rarity = 3;
            pity = rarity >= 3 ? 0 : pity + 1;
            if (rarity >= 2) high = true;
            var pool = D.equipment.FindAll(e => e.rarity == rarity);
            if (pool.Count == 0) pool = D.equipment;
            EquipmentData pick = pool[Random.Range(0, pool.Count)];
            // ★3 は pickupRate% で今日のピックアップ
            if (rarity >= 3 && pickup != null && Random.value * 100f < D.gacha.pickupRate) pick = pickup;
            var r = new GachaResult { Equipment = pick };
            int stacks;
            bool owned = Progress.HasEquipment(pick.id, out stacks);
            if (!owned)
            {
                Progress.AddEquipment(pick.id);
                r.IsNew = true;
            }
            else if (stacks < MaxStacks)
            {
                Progress.StackEquipment(pick.id);
                r.Stacks = stacks + 1;
            }
            else
            {
                List<int> refunds = D.gacha.duplicateCoins;
                r.Coins = refunds.Count >= pick.rarity ? refunds[pick.rarity - 1] : 20;
                r.Stacks = MaxStacks;
                Progress.AddCoins(r.Coins);
            }
            results.Add(r);
        }
        Progress.GachaPity = pity;
        Progress.GachaPulls += count;
        return results;
    }

    private static int RollRarity()
    {
        List<float> rates = D.gacha.rates;
        float total = 0f;
        foreach (float r in rates) total += r;
        float x = Random.value * Mathf.Max(0.01f, total);
        for (int i = 0; i < rates.Count; i++)
        {
            if (x < rates[i]) return i + 1;
            x -= rates[i];
        }
        return 1;
    }
}
