using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StageRecord
{
    public string id;
    public bool cleared;

    /// <summary>★の数（ライフを減らさずにクリアで3、減らしてクリアで2）</summary>
    public int stars;

    /// <summary>強襲（ライフ 1・敵が強い）でクリアした</summary>
    public bool hard;
}

/// <summary>強化の段</summary>
[Serializable]
public class UpgradeLevelRecord
{
    public string id;
    public int level;
}

/// <summary>編成の控え（編成1〜4）</summary>
[Serializable]
public class SquadPreset
{
    public List<string> members = new List<string>();
}

/// <summary>部員ごとの記録（出撃した回数と MVP の回数）</summary>
[Serializable]
public class MemberRecord
{
    public string id;
    public int sorties;
    public int mvp;

    /// <summary>育成：レベル（0 は 1 とみなす）・昇進の回数・付けている備品</summary>
    public int level = 1;
    public int promotion;
    public string equip;
}

/// <summary>持っている素材</summary>
[Serializable]
public class ItemStack
{
    public string id;
    public int count;
}

/// <summary>持っている備品（stacks：同じ物を重ねた数 0〜4）</summary>
[Serializable]
public class EquipRecord
{
    public string id;
    public int stacks;
}

/// <summary>エンドレスの最高記録（守りきった波の数）</summary>
[Serializable]
public class EndlessRecord
{
    public string id;
    public int wave;
}

[Serializable]
public class ProgressData
{
    public List<StageRecord> stages = new List<StageRecord>();
    public List<EndlessRecord> endless = new List<EndlessRecord>();
    public List<MemberRecord> members = new List<MemberRecord>();

    /// <summary>編成1〜4（アークナイツの編成の控え）と、いま使っている番号</summary>
    public List<SquadPreset> presets = new List<SquadPreset>();
    public int preset;
    public List<UpgradeLevelRecord> upgrades = new List<UpgradeLevelRecord>();

    /// <summary>取った実績の id と、出撃した回数（勝ち負けが決まった回）</summary>
    public List<string> achievements = new List<string>();
    public int battles;

    /// <summary>会ったことのある敵（初めて会ったときだけ説明の札を出す）。seenSeeded：前のセーブからの引き継ぎを済ませた</summary>
    public List<string> seenEnemies = new List<string>();
    public bool seenSeeded;

    /// <summary>育成とガチャ：部費・素材・備品。pastRewards：この仕組みの前にクリアした作戦のぶんを渡した</summary>
    public int coins;
    public List<ItemStack> items = new List<ItemStack>();
    public List<EquipRecord> equipment = new List<EquipRecord>();
    public bool pastRewards;

    /// <summary>ガチャ：★3 が出ないまま引いた回数（天井）と、引いた回数</summary>
    public int gachaPity;
    public int gachaPulls;

    /// <summary>無料の 1 回を引いた日（yyyy-MM-dd）</summary>
    public string freePullDate;

    /// <summary>毎日のこと（Daily）：数えている日・任務ごとの数・受け取った任務・ログインボーナスを渡した日</summary>
    public string dailyDate;
    public List<ItemStack> dailyCounts = new List<ItemStack>();
    public List<string> dailyClaimed = new List<string>();
    public string loginDate;
    public List<string> squad = new List<string>();
    public bool unlockAll;
}

/// <summary>
/// クリア状況と前回の編成を覚えておく（PlayerPrefs に JSON で保存）。
/// ステージは campaign.json の順に、前のステージをクリアすると次が開く。
/// </summary>
public static class Progress
{
    private const string Key = "c3td.progress.v1";

    private static ProgressData data;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        data = null;
    }

    private static ProgressData Data
    {
        get
        {
            if (data == null)
            {
                string json = PlayerPrefs.GetString(Key, "");
                try
                {
                    data = string.IsNullOrEmpty(json) ? new ProgressData() : JsonUtility.FromJson<ProgressData>(json);
                }
                catch (ArgumentException)
                {
                    Debug.LogWarning("セーブデータが読めないので、最初からにします");
                    data = new ProgressData();
                }
                if (data == null) data = new ProgressData();
            }
            return data;
        }
    }

    public static StageRecord Record(string stageId)
    {
        foreach (StageRecord r in Data.stages)
            if (r.id == stageId) return r;
        return null;
    }

    public static bool IsCleared(string stageId)
    {
        StageRecord r = Record(stageId);
        return r != null && r.cleared;
    }

    public static int Stars(string stageId)
    {
        StageRecord r = Record(stageId);
        return r != null ? r.stars : 0;
    }

    /// <summary>最初のステージと、前のステージをクリアしたステージが開いている（テスト用に全部開けることもできる）</summary>
    public static bool IsUnlocked(string stageId)
    {
        if (Data.unlockAll) return true;
        // unlockAfter のある章（エンドレス）は、その章の最初のステージを、指定のステージのクリアで開く
        ChapterData chapter = GameData.Campaign.ChapterOf(stageId);
        if (chapter != null && !string.IsNullOrEmpty(chapter.unlockAfter) && chapter.stages.Count > 0 && chapter.stages[0] == stageId)
            return IsCleared(chapter.unlockAfter);
        List<string> order = GameData.Campaign.AllStages();
        int index = order.IndexOf(stageId);
        if (index <= 0) return true;
        return IsCleared(order[index - 1]);
    }

    public static bool UnlockAll
    {
        get { return Data.unlockAll; }
        set
        {
            Data.unlockAll = value;
            Save();
        }
    }

    public static bool IsHardCleared(string stageId)
    {
        StageRecord r = Record(stageId);
        return r != null && r.hard;
    }

    public static void RecordClear(string stageId, int life, int maxLife, bool hard = false)
    {
        StageRecord r = Record(stageId);
        if (r == null)
        {
            r = new StageRecord { id = stageId };
            Data.stages.Add(r);
        }
        r.cleared = true;
        r.stars = Mathf.Max(r.stars, life >= maxLife ? 3 : 2);
        if (hard) r.hard = true;
        Save();
    }

    /// <summary>部員の記録（なければ null）</summary>
    public static MemberRecord Member(string id)
    {
        if (Data.members == null) return null;
        foreach (MemberRecord r in Data.members)
            if (r.id == id) return r;
        return null;
    }

    /// <summary>出撃が終わったとき：置いた部員の出撃回数と、MVP の回数を足す</summary>
    public static void RecordMembers(List<string> deployed, string mvp)
    {
        if (Data.members == null) Data.members = new List<MemberRecord>();
        foreach (string id in deployed)
        {
            MemberRecord r = Member(id);
            if (r == null)
            {
                r = new MemberRecord { id = id, level = 1 };
                Data.members.Add(r);
            }
            r.sorties++;
            if (id == mvp) r.mvp++;
        }
        Save();
    }

    /// <summary>エンドレスの最高記録（守りきった波の数。なければ 0）</summary>
    public static int BestWave(string stageId)
    {
        if (Data.endless == null) return 0;
        foreach (EndlessRecord r in Data.endless)
            if (r.id == stageId) return r.wave;
        return 0;
    }

    /// <summary>エンドレスの記録を残す。最高記録を更新したら true</summary>
    public static bool RecordEndless(string stageId, int wave)
    {
        if (Data.endless == null) Data.endless = new List<EndlessRecord>();
        EndlessRecord r = null;
        foreach (EndlessRecord e in Data.endless)
            if (e.id == stageId) r = e;
        if (r == null)
        {
            r = new EndlessRecord { id = stageId };
            Data.endless.Add(r);
        }
        bool best = wave > r.wave;
        if (best) r.wave = wave;
        Save();
        return best;
    }

    public static bool HasAchievement(string id)
    {
        return Data.achievements != null && Data.achievements.Contains(id);
    }

    public static void AddAchievement(string id)
    {
        if (Data.achievements == null) Data.achievements = new List<string>();
        if (Data.achievements.Contains(id)) return;
        Data.achievements.Add(id);
        Save();
    }

    /// <summary>その敵に会ったことがあるか（はじめて呼ばれたとき、クリアした作戦の敵を会ったことにする）</summary>
    public static bool HasSeenEnemy(string id)
    {
        if (Data.seenEnemies == null) Data.seenEnemies = new List<string>();
        if (!Data.seenSeeded)
        {
            Data.seenSeeded = true;
            foreach (string stageId in GameData.Campaign.AllStages())
            {
                if (!IsCleared(stageId)) continue;
                StageData s = GameData.Stage(stageId);
                if (s == null) continue;
                foreach (WaveData w in s.waves) SeedEnemy(w.enemy);
            }
            Save();
        }
        return Data.seenEnemies.Contains(id);
    }

    private static void SeedEnemy(string id)
    {
        if (string.IsNullOrEmpty(id) || Data.seenEnemies.Contains(id)) return;
        Data.seenEnemies.Add(id);
        EnemyData e = GameData.Enemy(id);
        if (e == null) return;
        if (e.summon != null) SeedEnemy(e.summon.enemy);
        if (e.split != null) SeedEnemy(e.split.enemy);
    }

    public static void MarkSeenEnemy(string id)
    {
        if (HasSeenEnemy(id)) return;
        Data.seenEnemies.Add(id);
        Save();
    }

    // ================= 育成とガチャ =================

    public static int Coins { get { return Data.coins; } }

    public static void AddCoins(int amount)
    {
        Data.coins = Mathf.Max(0, Data.coins + amount);
        Save();
    }

    public static int ItemCount(string id)
    {
        if (Data.items == null) return 0;
        foreach (ItemStack s in Data.items) if (s.id == id) return s.count;
        return 0;
    }

    public static void AddItem(string id, int amount)
    {
        if (Data.items == null) Data.items = new List<ItemStack>();
        ItemStack stack = null;
        foreach (ItemStack s in Data.items) if (s.id == id) stack = s;
        if (stack == null)
        {
            stack = new ItemStack { id = id };
            Data.items.Add(stack);
        }
        stack.count = Mathf.Max(0, stack.count + amount);
        Save();
    }

    public static bool HasEquipment(string id, out int stacks)
    {
        stacks = 0;
        if (Data.equipment == null) return false;
        foreach (EquipRecord e in Data.equipment)
            if (e.id == id) { stacks = e.stacks; return true; }
        return false;
    }

    public static int EquipmentStacks(string id)
    {
        int stacks;
        HasEquipment(id, out stacks);
        return stacks;
    }

    public static List<EquipRecord> OwnedEquipment
    {
        get { return Data.equipment ?? (Data.equipment = new List<EquipRecord>()); }
    }

    public static void AddEquipment(string id)
    {
        int stacks;
        if (HasEquipment(id, out stacks)) return;
        OwnedEquipment.Add(new EquipRecord { id = id });
        Save();
    }

    public static void StackEquipment(string id)
    {
        foreach (EquipRecord e in OwnedEquipment)
            if (e.id == id) e.stacks = Mathf.Min(Growth.MaxStacks, e.stacks + 1);
        Save();
    }

    private static MemberRecord MemberOrNew(string id)
    {
        if (Data.members == null) Data.members = new List<MemberRecord>();
        MemberRecord r = Member(id);
        if (r == null)
        {
            r = new MemberRecord { id = id, level = 1 };
            Data.members.Add(r);
        }
        return r;
    }

    public static int MemberLevel(string id)
    {
        MemberRecord r = Member(id);
        return r != null ? Mathf.Max(1, r.level) : 1;
    }

    public static int MemberPromotion(string id)
    {
        MemberRecord r = Member(id);
        return r != null ? r.promotion : 0;
    }

    public static string MemberEquip(string id)
    {
        MemberRecord r = Member(id);
        return r != null ? r.equip : null;
    }

    public static void SetMemberGrowth(string id, int level, int promotion)
    {
        MemberRecord r = MemberOrNew(id);
        r.level = Mathf.Max(1, level);
        r.promotion = Mathf.Max(0, promotion);
        Save();
    }

    /// <summary>備品を付ける（ほかの部員が付けていれば外す）。equipId が空なら外す</summary>
    public static void SetMemberEquip(string id, string equipId)
    {
        if (!string.IsNullOrEmpty(equipId) && Data.members != null)
            foreach (MemberRecord other in Data.members)
                if (other.id != id && other.equip == equipId) other.equip = null;
        MemberOrNew(id).equip = string.IsNullOrEmpty(equipId) ? null : equipId;
        Save();
    }

    /// <summary>その備品を付けている部員（いなければ null）</summary>
    public static string EquippedBy(string equipId)
    {
        if (Data.members == null || string.IsNullOrEmpty(equipId)) return null;
        foreach (MemberRecord r in Data.members) if (r.equip == equipId) return r.id;
        return null;
    }

    public static int GachaPity
    {
        get { return Data.gachaPity; }
        set
        {
            Data.gachaPity = Mathf.Max(0, value);
            Save();
        }
    }

    // ================= 毎日のこと =================

    public static string DailyDate { get { return Data.dailyDate; } }

    public static void ResetDaily(string date)
    {
        Data.dailyDate = date;
        Data.dailyCounts = new List<ItemStack>();
        Data.dailyClaimed = new List<string>();
        Save();
    }

    public static int DailyCount(string id)
    {
        if (Data.dailyCounts == null) return 0;
        foreach (ItemStack s in Data.dailyCounts) if (s.id == id) return s.count;
        return 0;
    }

    public static void AddDailyCount(string id, int amount)
    {
        if (Data.dailyCounts == null) Data.dailyCounts = new List<ItemStack>();
        ItemStack stack = null;
        foreach (ItemStack s in Data.dailyCounts) if (s.id == id) stack = s;
        if (stack == null)
        {
            stack = new ItemStack { id = id };
            Data.dailyCounts.Add(stack);
        }
        stack.count += amount;
        Save();
    }

    public static bool DailyClaimed(string id)
    {
        return Data.dailyClaimed != null && Data.dailyClaimed.Contains(id);
    }

    public static void SetDailyClaimed(string id)
    {
        if (Data.dailyClaimed == null) Data.dailyClaimed = new List<string>();
        if (!Data.dailyClaimed.Contains(id)) Data.dailyClaimed.Add(id);
        Save();
    }

    public static string LoginDate
    {
        get { return Data.loginDate; }
        set
        {
            Data.loginDate = value;
            Save();
        }
    }

    public static string FreePullDate
    {
        get { return Data.freePullDate; }
        set
        {
            Data.freePullDate = value;
            Save();
        }
    }

    public static int GachaPulls
    {
        get { return Data.gachaPulls; }
        set
        {
            Data.gachaPulls = Mathf.Max(0, value);
            Save();
        }
    }

    public static bool PastRewardsGiven
    {
        get { return Data.pastRewards; }
        set
        {
            Data.pastRewards = value;
            Save();
        }
    }

    /// <summary>勝ち負けが決まった出撃の回数</summary>
    public static int Battles { get { return Data.battles; } }

    public static void RecordBattle()
    {
        Data.battles++;
        Save();
    }

    /// <summary>強化の段（なければ 0）</summary>
    public static int UpgradeLevel(string id)
    {
        if (Data.upgrades == null) return 0;
        foreach (UpgradeLevelRecord r in Data.upgrades)
            if (r.id == id) return r.level;
        return 0;
    }

    public static void SetUpgradeLevel(string id, int level)
    {
        if (Data.upgrades == null) Data.upgrades = new List<UpgradeLevelRecord>();
        UpgradeLevelRecord rec = null;
        foreach (UpgradeLevelRecord r in Data.upgrades)
            if (r.id == id) rec = r;
        if (rec == null)
        {
            rec = new UpgradeLevelRecord { id = id };
            Data.upgrades.Add(rec);
        }
        rec.level = Mathf.Max(0, level);
        Save();
    }

    public const int PresetCount = 4;

    /// <summary>編成の控えを 4 つにそろえる（はじめは、これまでの編成を編成1に）</summary>
    private static void EnsurePresets()
    {
        if (Data.presets == null) Data.presets = new List<SquadPreset>();
        if (Data.presets.Count == 0)
            Data.presets.Add(new SquadPreset { members = new List<string>(Data.squad.Count > 0 ? Data.squad : GameData.Squad.members) });
        while (Data.presets.Count < PresetCount) Data.presets.Add(new SquadPreset());
        Data.preset = Mathf.Clamp(Data.preset, 0, PresetCount - 1);
    }

    /// <summary>いま使っている編成の番号（0〜3）。変えると、その控えの編成が Squad になる</summary>
    public static int Preset
    {
        get
        {
            EnsurePresets();
            return Data.preset;
        }
        set
        {
            EnsurePresets();
            Data.preset = Mathf.Clamp(value, 0, PresetCount - 1);
            Save();
        }
    }

    /// <summary>いまの編成（編成1〜4のうち使っているもの。空のこともある）</summary>
    public static List<string> Squad
    {
        get
        {
            EnsurePresets();
            var list = new List<string>(Data.presets[Data.preset].members ?? new List<string>());
            // もう無い部員は外す
            List<string> all = GameData.CharacterIds();
            list.RemoveAll(id => !all.Contains(id));
            return list;
        }
        set
        {
            EnsurePresets();
            Data.presets[Data.preset].members = new List<string>(value);
            Data.squad = new List<string>(value);
            Save();
        }
    }

    public static void ResetAll()
    {
        data = new ProgressData();
        Save();
    }

    private static void Save()
    {
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
        PlayerPrefs.Save();
    }
}
