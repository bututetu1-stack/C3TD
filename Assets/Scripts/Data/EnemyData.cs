/// <summary>敵の見た目の形</summary>
public enum EnemyShape
{
    Circle,
    Square,
    Diamond,
    Hexagon,
}

/// <summary>敵の絵（Art/Enemies/{id}.png）が向いている方向</summary>
public enum ArtFacing
{
    Right,
    Left,
}

/// <summary>敵の定義（Resources/Data/Enemies/{id}.json）</summary>
public class EnemyData
{
    public string id;
    public string name;

    /// <summary>図形の上に出す短い文字。空なら name の1文字目</summary>
    public string label;

    public bool boss;

    // 見た目
    public EnemyShape shape;
    public string color;
    public float size;

    /// <summary>Art/Enemies/{id}.png の絵を使うときの大きさ（size に掛ける）。0 なら標準</summary>
    public float artScale;

    /// <summary>絵が向いている方向。進む向きに合わせて左右を反転する</summary>
    public ArtFacing artFacing;

    // ステータス
    public int hp;
    public int atk;
    public int def;

    /// <summary>術耐性（%）。術師の攻撃をこの割合だけ減らす</summary>
    public float res;
    public float attackInterval;

    /// <summary>移動の速さ（マス/秒）</summary>
    public float speed;

    /// <summary>拠点に着いたときに減るライフ</summary>
    public int lifeDamage = 1;

    // 特別な性質（どれも省略できる）

    /// <summary>空を飛ぶ。止められず、近接（前衛・重装）の部員は攻撃できない。経路はステージの airPaths を使う</summary>
    public bool flying;

    /// <summary>0 より大きければ遠距離攻撃：歩きながら、射程（マス）内のいちばん近い部員を撃つ</summary>
    public float range;

    /// <summary>攻撃のダメージの種類（Arts なら部員の防御が効かない）</summary>
    public DamageType damageType = DamageType.Physical;

    /// <summary>攻撃が当たった部員を、この秒数だけ動けなくする（眠らせる）</summary>
    public float stunOnHit;

    /// <summary>倒されても、この回数だけ起き上がる。体力は最大の reviveHpPercent%</summary>
    public int revive;
    public float reviveHpPercent = 50f;

    /// <summary>まわりの敵の能力を上げる（なければ null）</summary>
    public EnemyAura aura;

    /// <summary>一定の間隔で手下を呼ぶ（なければ null）</summary>
    public EnemySummon summon;

    public string Label
    {
        get
        {
            if (!string.IsNullOrEmpty(label)) return label;
            return string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1);
        }
    }
}

/// <summary>まわりの敵（自分は含まない）の能力を上げる。stat は atk / def / speed、percent（%）、radius（マス）</summary>
public class EnemyAura
{
    public string stat;
    public float percent;
    public float radius = 1.5f;
}

/// <summary>interval 秒ごとに enemy を count 体、自分の位置から呼ぶ（同じ経路の続きを進む）。1回目は firstDelay 秒後</summary>
public class EnemySummon
{
    public string enemy;
    public int count = 1;
    public float interval = 15f;
    public float firstDelay = 8f;
}
