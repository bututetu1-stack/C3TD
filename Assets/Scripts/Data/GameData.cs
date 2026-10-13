using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resources/Data/ の JSON を読み込んでキャッシュする。
/// 読めなかったときはコンソールにエラーを出して null を返す。
/// </summary>
public static class GameData
{
    private const string StageDir = "Data/Stages/";
    private const string EnemyDir = "Data/Enemies/";

    private static readonly Dictionary<string, StageData> stages = new Dictionary<string, StageData>();
    private static readonly Dictionary<string, EnemyData> enemies = new Dictionary<string, EnemyData>();

    // ドメインのリロードなしでプレイしても、JSON の変更が反映されるようにする
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ClearCache()
    {
        stages.Clear();
        enemies.Clear();
    }

    public static StageData Stage(string id)
    {
        return Load(StageDir, id, stages);
    }

    public static EnemyData Enemy(string id)
    {
        return Load(EnemyDir, id, enemies);
    }

    /// <summary>Data/Stages にあるステージの id を名前順に返す</summary>
    public static List<string> StageIds()
    {
        var ids = new List<string>();
        foreach (var asset in Resources.LoadAll<TextAsset>(StageDir.TrimEnd('/'))) ids.Add(asset.name);
        ids.Sort(StringComparer.Ordinal);
        return ids;
    }

    private static T Load<T>(string dir, string id, Dictionary<string, T> cache) where T : class
    {
        if (string.IsNullOrEmpty(id)) return null;
        T value;
        if (cache.TryGetValue(id, out value)) return value;

        string path = dir + id;
        var asset = Resources.Load<TextAsset>(path);
        if (asset == null)
        {
            Debug.LogError("データが見つかりません: Resources/" + path + ".json");
            return null;
        }
        try
        {
            value = JsonMapper.To<T>(MiniJson.Parse(asset.text), id + ".json");
        }
        catch (FormatException e)
        {
            Debug.LogError("Resources/" + path + ".json を読めません。" + e.Message);
            return null;
        }
        cache[id] = value;
        return value;
    }
}
