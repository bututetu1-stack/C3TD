using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resources/Data/ の JSON を読み込んでキャッシュする。
/// 読めなかったときはコンソールにエラーを出して null（ルールなどは初期値）を返す。
/// </summary>
public static class GameData
{
    private const string StageDir = "Data/Stages/";
    private const string EnemyDir = "Data/Enemies/";
    private const string CharacterDir = "Data/Characters/";
    private const string SummonDir = "Data/Summons/";
    private const string ClassesPath = "Data/classes";
    private const string RulesPath = "Data/rules";
    private const string SquadPath = "Data/squad";

    private static readonly Dictionary<string, StageData> stages = new Dictionary<string, StageData>();
    private static readonly Dictionary<string, EnemyData> enemies = new Dictionary<string, EnemyData>();
    private static readonly Dictionary<string, CharacterData> characters = new Dictionary<string, CharacterData>();
    private static readonly Dictionary<string, CharacterData> summons = new Dictionary<string, CharacterData>();
    private static Dictionary<string, ClassData> classes;
    private static RulesData rules;
    private static SquadData squad;

    // ドメインのリロードなしでプレイしても、JSON の変更が反映されるようにする
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ClearCache()
    {
        stages.Clear();
        enemies.Clear();
        characters.Clear();
        summons.Clear();
        classes = null;
        rules = null;
        squad = null;
    }

    public static StageData Stage(string id) { return Load(StageDir, id, stages); }
    public static EnemyData Enemy(string id) { return Load(EnemyDir, id, enemies); }
    public static CharacterData Character(string id) { return Load(CharacterDir, id, characters); }

    /// <summary>召喚されるユニット（Resources/Data/Summons/{id}.json。部員と同じ書き方）</summary>
    public static CharacterData Summon(string id) { return Load(SummonDir, id, summons); }

    public static RulesData Rules
    {
        get
        {
            if (rules == null) rules = Parse<RulesData>(RulesPath) ?? new RulesData();
            return rules;
        }
    }

    public static SquadData Squad
    {
        get
        {
            if (squad == null) squad = Parse<SquadData>(SquadPath) ?? new SquadData();
            return squad;
        }
    }

    public static ClassData Class(UnitClass unitClass)
    {
        if (classes == null) classes = Parse<Dictionary<string, ClassData>>(ClassesPath) ?? new Dictionary<string, ClassData>();
        ClassData data;
        if (!classes.TryGetValue(unitClass.ToString(), out data))
        {
            Debug.LogError("Resources/" + ClassesPath + ".json に職業 \"" + unitClass + "\" がありません");
            data = new ClassData { name = unitClass.ToString() };
            classes[unitClass.ToString()] = data;
        }
        return data;
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
        value = Parse<T>(dir + id);
        if (value != null) cache[id] = value;
        return value;
    }

    private static T Parse<T>(string path) where T : class
    {
        var asset = Resources.Load<TextAsset>(path);
        if (asset == null)
        {
            Debug.LogError("データが見つかりません: Resources/" + path + ".json");
            return null;
        }
        try
        {
            return JsonMapper.To<T>(MiniJson.Parse(asset.text), asset.name + ".json");
        }
        catch (FormatException e)
        {
            Debug.LogError("Resources/" + path + ".json を読めません。" + e.Message);
            return null;
        }
    }
}
