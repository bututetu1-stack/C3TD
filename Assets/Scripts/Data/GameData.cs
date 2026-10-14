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
    private const string CampaignPath = "Data/campaign";
    private const string AudioPath = "Data/audio";

    private static readonly Dictionary<string, StageData> stages = new Dictionary<string, StageData>();
    private static readonly Dictionary<string, EnemyData> enemies = new Dictionary<string, EnemyData>();
    private static readonly Dictionary<string, CharacterData> characters = new Dictionary<string, CharacterData>();
    private static readonly Dictionary<string, CharacterData> summons = new Dictionary<string, CharacterData>();
    private static Dictionary<string, ClassData> classes;
    private static RulesData rules;
    private static SquadData squad;
    private static CampaignData campaign;
    private static AudioData audio;

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
        campaign = null;
        audio = null;
    }

    public static StageData Stage(string id) { return Load(StageDir, id, stages); }
    public static EnemyData Enemy(string id) { return Load(EnemyDir, id, enemies); }
    public static CharacterData Character(string id) { return Load(CharacterDir, id, characters); }

    /// <summary>召喚されるユニット（Resources/Data/Summons/{id}.json。部員と同じ書き方）</summary>
    public static CharacterData Summon(string id) { return Load(SummonDir, id, summons); }

    /// <summary>音ごとの音量と使う部分（Data/audio.json）</summary>
    public static AudioData Audio
    {
        get
        {
            if (audio == null) audio = Parse<AudioData>(AudioPath) ?? new AudioData();
            return audio;
        }
    }

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

    public static CampaignData Campaign
    {
        get
        {
            if (campaign == null) campaign = Parse<CampaignData>(CampaignPath) ?? new CampaignData();
            return campaign;
        }
    }

    /// <summary>Data/Characters にある部員の id。squad.json に書いた順を先に、残りは名前順</summary>
    /// <summary>
    /// このステージより前の作戦（campaign.json の順）で出てくる敵。ウェーブの敵と、その敵が呼ぶ・分かれて出す敵も含める
    /// （出撃中の「NEW ENEMY」と、作戦選択の「NEW」）
    /// </summary>
    public static HashSet<string> EnemiesBefore(string stageId)
    {
        var seen = new HashSet<string>();
        foreach (string id in Campaign.AllStages())
        {
            if (id == stageId) break;
            StageData s = Stage(id);
            if (s == null) continue;
            foreach (WaveData w in s.waves) AddWithChildren(w.enemy, seen);
        }
        return seen;
    }

    private static void AddWithChildren(string enemyId, HashSet<string> seen)
    {
        if (string.IsNullOrEmpty(enemyId) || !seen.Add(enemyId)) return;
        EnemyData e = Enemy(enemyId);
        if (e == null) return;
        if (e.summon != null) AddWithChildren(e.summon.enemy, seen);
        if (e.split != null) AddWithChildren(e.split.enemy, seen);
    }

    public static List<string> CharacterIds()
    {
        var rest = new List<string>();
        foreach (var asset in Resources.LoadAll<TextAsset>(CharacterDir.TrimEnd('/'))) rest.Add(asset.name);
        rest.Sort(StringComparer.Ordinal);
        var ids = new List<string>();
        foreach (string id in Squad.members)
            if (rest.Remove(id)) ids.Add(id);
        ids.AddRange(rest);
        return ids;
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
