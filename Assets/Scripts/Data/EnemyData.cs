/// <summary>敵の見た目の形</summary>
public enum EnemyShape
{
    Circle,
    Square,
    Diamond,
    Hexagon,
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

    // ステータス
    public int hp;
    public int atk;
    public int def;
    public float attackInterval;

    /// <summary>移動の速さ（マス/秒）</summary>
    public float speed;

    /// <summary>拠点に着いたときに減るライフ</summary>
    public int lifeDamage = 1;

    public string Label
    {
        get
        {
            if (!string.IsNullOrEmpty(label)) return label;
            return string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1);
        }
    }
}
