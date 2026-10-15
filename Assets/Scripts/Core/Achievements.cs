using System.Collections.Generic;

/// <summary>
/// 実績（Kingdom Rush の実績のように）。クリア・★・強襲・エンドレス・部員の記録・強化から判定する。
/// 出撃が終わるたびに Battle が CheckNew を呼び、新しく取ったものを結果の画面に出す。記録の画面に一覧。
/// </summary>
public static class Achievements
{
    public class Entry
    {
        public string Id;
        public string Name;
        public string Description;
        public System.Func<bool> Done;

        /// <summary>取ったときにもらえる部費</summary>
        public int Reward = 100;
    }

    private static List<Entry> all;

    public static List<Entry> All
    {
        get
        {
            if (all == null) all = Build();
            return all;
        }
    }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        all = null;
    }

    /// <summary>まだ記録していない、条件を満たした実績を記録して返す</summary>
    public static List<Entry> CheckNew()
    {
        var found = new List<Entry>();
        foreach (Entry e in All)
        {
            if (Progress.HasAchievement(e.Id)) continue;
            bool done;
            try { done = e.Done(); }
            catch (System.Exception) { done = false; }
            if (!done) continue;
            Progress.AddAchievement(e.Id);
            if (e.Reward > 0) Progress.AddCoins(e.Reward);
            found.Add(e);
        }
        return found;
    }

    public static int UnlockedCount()
    {
        int n = 0;
        foreach (Entry e in All) if (Progress.HasAchievement(e.Id)) n++;
        return n;
    }

    private static List<Entry> Build()
    {
        var list = new List<Entry>();
        System.Action<string, string, string, System.Func<bool>> add = (id, name, desc, done) =>
            list.Add(new Entry { Id = id, Name = name, Description = desc, Done = done, Reward = RewardFor(id) });

        add("first_clear", "はじめての防衛", "作戦を 1 つクリアする", () => CountStages(id => Progress.IsCleared(id)) >= 1);
        add("ch1", "前期おわり", "第1章をすべてクリアする", () => ChapterCleared("ch1"));
        add("ch2", "後期おわり", "第2章をすべてクリアする", () => ChapterCleared("ch2"));
        add("ch3", "内定", "第3章をすべてクリアする", () => ChapterCleared("ch3"));
        add("ch4", "卒業", "第4章をすべてクリアする", () => ChapterCleared("ch4"));
        add("ex", "院試合格", "番外編をすべてクリアする", () => ChapterCleared("ex"));
        add("ch1_perfect", "皆勤賞", "第1章をすべて★3 でクリアする", () => ChapterPerfect("ch1"));
        add("all_perfect", "首席", "すべての作戦を★3 でクリアする", AllPerfect);
        add("hard1", "強襲デビュー", "強襲で作戦を 1 つクリアする", () => CountStages(id => Progress.IsHardCleared(id)) >= 1);
        add("hard10", "強襲の常連", "強襲で作戦を 10 クリアする", () => CountStages(id => Progress.IsHardCleared(id)) >= 10);
        add("endless10", "部室の番人", "エンドレスで 10 波守りきる", () => BestEndless() >= 10);
        add("endless20", "不夜城", "エンドレスで 20 波守りきる", () => BestEndless() >= 20);
        add("endless30", "難攻不落の部室", "エンドレスで 30 波守りきる", () => BestEndless() >= 30);
        add("all_members", "部員総出", "部員を全員 1 回は出撃させる", () => AllMembers(r => r.sorties >= 1, false));
        add("real_mvp", "みんなが主役", "ダミー以外の部員が全員 MVP を取る", () => AllMembers(r => r.mvp >= 1, true));
        add("mvp10", "エース", "同じ部員で MVP を 10 回取る", () => AnyMember(r => r.mvp >= 10));
        add("upgrades_max", "備品そろい", "強化をすべて最後の段までそろえる", UpgradesMax);
        add("battles50", "常連", "50 回出撃する", () => Progress.Battles >= 50);
        add("promotion1", "はじめての昇進", "部員を 1 人昇進させる", () => AnyMember(r => r.promotion >= 1));
        add("promotion2", "幹部", "部員を 1 人 2 回昇進させる", () => AnyMember(r => r.promotion >= 2));
        add("rare_equip", "掘り出し物", "★3 の備品を手に入れる", () => Progress.OwnedEquipment.Exists(e => { EquipmentData d = Growth.Equipment(e.id); return d != null && d.rarity >= 3; }));
        add("collect6", "備品集め", "備品を 6 種類そろえる", () => Progress.OwnedEquipment.Count >= 6);
        add("collect_all", "備品図鑑コンプリート", "備品をすべての種類そろえる", () => Progress.OwnedEquipment.Count >= GameData.Items.equipment.Count);
        add("stack_max", "使い込み", "同じ備品を 4 回重ねる", () => Progress.OwnedEquipment.Exists(e => e.stacks >= 4));
        add("gacha10", "ガチャ好き", "ガチャを 10 回引く", () => Progress.GachaPulls >= 10);
        add("gacha100", "ガチャ沼", "ガチャを 100 回引く", () => Progress.GachaPulls >= 100);
        return list;
    }

    /// <summary>実績の部費（むずかしいものほど多く）</summary>
    private static int RewardFor(string id)
    {
        switch (id)
        {
            case "all_perfect": case "endless30": case "real_mvp": case "upgrades_max": case "collect_all": return 1000;
            case "ch3": case "ch4": case "ex": case "hard10": case "endless20": case "mvp10": case "promotion2": case "stack_max": case "gacha100": return 500;
            case "ch1": case "ch2": case "ch1_perfect": case "endless10": case "battles50": case "all_members": case "rare_equip": case "collect6": return 300;
            default: return 150;
        }
    }

    private static int CountStages(System.Func<string, bool> match)
    {
        int n = 0;
        foreach (string id in GameData.Campaign.AllStages()) if (match(id)) n++;
        return n;
    }

    private static ChapterData Chapter(string id)
    {
        foreach (ChapterData c in GameData.Campaign.chapters) if (c.id == id) return c;
        return null;
    }

    private static bool ChapterCleared(string chapterId)
    {
        ChapterData c = Chapter(chapterId);
        if (c == null || c.stages.Count == 0) return false;
        foreach (string id in c.stages) if (!Progress.IsCleared(id)) return false;
        return true;
    }

    private static bool ChapterPerfect(string chapterId)
    {
        ChapterData c = Chapter(chapterId);
        if (c == null || c.stages.Count == 0) return false;
        foreach (string id in c.stages) if (Progress.Stars(id) < 3) return false;
        return true;
    }

    private static bool AllPerfect()
    {
        foreach (string id in GameData.Campaign.AllStages())
        {
            StageData s = GameData.Stage(id);
            if (s != null && s.endless == null && Progress.Stars(id) < 3) return false;
        }
        return true;
    }

    private static int BestEndless()
    {
        int best = 0;
        foreach (string id in GameData.Campaign.AllStages())
        {
            StageData s = GameData.Stage(id);
            if (s != null && s.endless != null && Progress.BestWave(id) > best) best = Progress.BestWave(id);
        }
        return best;
    }

    /// <summary>部員全員（realOnly ならダミー以外）が条件を満たすか</summary>
    private static bool AllMembers(System.Func<MemberRecord, bool> match, bool realOnly)
    {
        int count = 0;
        foreach (string id in GameData.CharacterIds())
        {
            if (realOnly && id.StartsWith("dummy_")) continue;
            MemberRecord r = Progress.Member(id);
            if (r == null || !match(r)) return false;
            count++;
        }
        return count > 0;
    }

    private static bool AnyMember(System.Func<MemberRecord, bool> match)
    {
        foreach (string id in GameData.CharacterIds())
        {
            MemberRecord r = Progress.Member(id);
            if (r != null && match(r)) return true;
        }
        return false;
    }

    private static bool UpgradesMax()
    {
        if (Upgrades.All.Count == 0) return false;
        foreach (UpgradeData u in Upgrades.All) if (Progress.UpgradeLevel(u.id) < u.MaxLevel) return false;
        return true;
    }
}
