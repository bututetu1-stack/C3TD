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
}

[Serializable]
public class ProgressData
{
    public List<StageRecord> stages = new List<StageRecord>();
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

    public static void RecordClear(string stageId, int life, int maxLife)
    {
        StageRecord r = Record(stageId);
        if (r == null)
        {
            r = new StageRecord { id = stageId };
            Data.stages.Add(r);
        }
        r.cleared = true;
        r.stars = Mathf.Max(r.stars, life >= maxLife ? 3 : 2);
        Save();
    }

    /// <summary>前回の編成（なければ squad.json の並び）</summary>
    public static List<string> Squad
    {
        get
        {
            var list = new List<string>(Data.squad.Count > 0 ? Data.squad : GameData.Squad.members);
            // もう無い部員は外す
            List<string> all = GameData.CharacterIds();
            list.RemoveAll(id => !all.Contains(id));
            return list;
        }
        set
        {
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
