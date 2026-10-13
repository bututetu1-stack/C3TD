using System.Collections.Generic;
using UnityEngine;

/// <summary>ステージの定義（Resources/Data/Stages/{id}.json）</summary>
public class StageData
{
    public string id;
    public string name;
    public int life;
    public int initialDp;
    public string bgm;

    /// <summary>マップの文字列。1行目が画面のいちばん上</summary>
    public List<string> map = new List<string>();

    /// <summary>敵の経路。マス座標 [列, 行] の配列（行は上から0）。出撃口から拠点まで順に書く</summary>
    public Dictionary<string, List<Vector2Int>> paths = new Dictionary<string, List<Vector2Int>>();

    public List<WaveData> waves = new List<WaveData>();
}

/// <summary>出撃の1グループ。time 秒に1体目が出て、interval 秒ごとに count 体出る</summary>
public class WaveData
{
    public float time;
    public string enemy;
    public string path;
    public int count = 1;
    public float interval;
}
