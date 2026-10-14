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

    /// <summary>ボスが出たときに切り替える BGM のキー（省略時は boss。ファイルがなければ切り替えない）</summary>
    public string bossBgm;

    /// <summary>戦闘の背景のキー（Art/Background/{background}.png。省略やファイルがなければ Battle）</summary>
    public string background;

    /// <summary>作戦選択に出す説明</summary>
    public string description;

    /// <summary>マップの文字列。1行目が画面のいちばん上</summary>
    public List<string> map = new List<string>();

    /// <summary>敵の経路。マス座標 [列, 行] の配列（行は上から0）。出撃口から拠点まで順に書く</summary>
    public Dictionary<string, List<Vector2Int>> paths = new Dictionary<string, List<Vector2Int>>();

    /// <summary>飛ぶ敵の経路（書き方は paths と同じ。壁や高台の上も通れる）。ウェーブの path にはどちらの名前も書ける</summary>
    public Dictionary<string, List<Vector2Int>> airPaths = new Dictionary<string, List<Vector2Int>>();

    public List<WaveData> waves = new List<WaveData>();

    /// <summary>出撃中に画面の左下に出す操作のヒント（なくてよい）</summary>
    public List<HintData> hints = new List<HintData>();

    /// <summary>エンドレス（敵の波が終わらない。拠点を守れた波の数を競う）。書けば waves の代わりに波を作り続ける</summary>
    public EndlessData endless;

    /// <summary>name の先頭の「1-1」のような番号（なければ空）</summary>
    public string Code
    {
        get
        {
            string code, title;
            SplitName(out code, out title);
            return code;
        }
    }

    /// <summary>name から番号を除いた題名</summary>
    public string Title
    {
        get
        {
            string code, title;
            SplitName(out code, out title);
            return title;
        }
    }

    private void SplitName(out string code, out string title)
    {
        code = "";
        title = name ?? "";
        int space = title.IndexOfAny(new[] { ' ', '　' });
        if (space <= 0) return;
        string head = title.Substring(0, space);
        foreach (char c in head)
        {
            bool ascii = (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '-';
            if (!ascii) return;
        }
        code = head;
        title = title.Substring(space + 1).Trim();
    }
}

/// <summary>エンドレスの決まり（EndlessDirector が使う）</summary>
public class EndlessData
{
    /// <summary>最初の波が始まる秒</summary>
    public float firstDelay = 4f;

    /// <summary>波と波の間（秒）。波ごとに intervalShrink 秒ずつ短くなり、minInterval より短くはならない</summary>
    public float interval = 24f;
    public float intervalShrink = 0.2f;
    public float minInterval = 16f;

    /// <summary>波ごとに敵の体力・攻撃力が何倍ずつ上がるか（0.08 なら 1 波ごとに +8%）</summary>
    public float hpGrowth = 0.08f;
    public float atkGrowth = 0.03f;

    /// <summary>この波ごとにボス（bosses を順に）</summary>
    public int bossEvery = 10;
    public List<string> bosses = new List<string>();

    /// <summary>1つの波の敵の組の数の上限（波が進むと 3 波ごとに 1 組増える）</summary>
    public int maxGroups = 4;

    /// <summary>敵の量（課題なら何体ぶん）。波ごとに pointsPerWave ずつ増える</summary>
    public float basePoints = 4f;
    public float pointsPerWave = 0.5f;

    /// <summary>出てくる敵の組。from 波目から出る</summary>
    public List<EndlessPool> pools = new List<EndlessPool>();
}

public class EndlessPool
{
    public int from = 1;
    public List<string> enemies = new List<string>();
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

/// <summary>出撃中に出すヒント。time 秒（ステージの経過時間）に出て、duration 秒で消える</summary>
public class HintData
{
    public float time;
    public string text;
    public float duration = 8f;
}
