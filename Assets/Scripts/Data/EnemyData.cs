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

    /// <summary>倒されたとき、その場から enemy を count 体出す（同じ経路の続きを進む）。なければ null</summary>
    public EnemySplit split;

    /// <summary>攻撃が当たった部員の能力を、duration 秒だけ下げる（重ならず、当たるたびに長さを戻す）。なければ null</summary>
    public EnemyHitModifier hitModifier;

    /// <summary>場にいるあいだ、DP の回復を1秒あたりこれだけ減らす（何体いても足し算。DP は 0 より下がらない）</summary>
    public float dpDrain;

    /// <summary>interval 秒ごとに、まわり radius マスの部員を stun 秒動けなくする（ボスの大技）。なければ null</summary>
    public EnemyPulse pulse;

    /// <summary>体力より先に減る盾（0 なら盾なし）。体力バーの上に水色のバーで出す</summary>
    public float shield;

    /// <summary>interval 秒ごとに、まわり radius マスの敵（自分も）の体力を最大の percent% 回復する。なければ null</summary>
    public EnemyHeal heal;

    /// <summary>体力が hpPercent% 以下になったら、一度だけ能力が上がる（ボスの本気）。なければ null</summary>
    public EnemyEnrage enrage;

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

/// <summary>倒されたときに出す敵</summary>
public class EnemySplit
{
    public string enemy;
    public int count = 2;
}

/// <summary>攻撃が当たった部員の能力を下げる。stat は atk / def / attackInterval、percent（%）、duration（秒）</summary>
public class EnemyHitModifier
{
    public string stat;
    public float percent;
    public float duration = 3f;
}

/// <summary>interval 秒ごと（1回目は firstDelay 秒後）に、まわり radius マスの部員を stun 秒動けなくする。name は技の名前</summary>
public class EnemyPulse
{
    public float interval = 15f;
    public float firstDelay = 8f;
    public float radius = 2f;
    public float stun = 2f;
    public string name;
}

/// <summary>interval 秒ごと（1回目は interval 秒後）に、まわり radius マスの敵（自分も）の体力を最大の percent% 回復する</summary>
public class EnemyHeal
{
    public float interval = 5f;
    public float radius = 1.8f;
    public float percent = 8f;

    /// <summary>1回に回復する量の上限（ボスのように体力の多い敵が回復しすぎないように）</summary>
    public float maxAmount = 400f;
}

/// <summary>体力が hpPercent% 以下になったら、一度だけ atk・speed・def を、それぞれの %だけ上げる。name は出す言葉</summary>
public class EnemyEnrage
{
    public float hpPercent = 50f;
    public float atk;
    public float speed;
    public float def;
    public string name;
}
