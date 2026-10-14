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
                r = new MemberRecord { id = id };
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
