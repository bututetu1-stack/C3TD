using UnityEngine;

/// <summary>仮素材の色。見た目の調整はここでまとめて行う</summary>
public static class Palette
{
    public static readonly Color Background = Hex("#141925");

    // マス
    public static readonly Color Ground = Hex("#7C7A74");
    public static readonly Color GroundAlt = Hex("#84827B");
    public static readonly Color HighGround = Hex("#C3CAD6");
    public static readonly Color HighGroundSide = Hex("#7D8697");
    public static readonly Color Wall = Hex("#262C3A");
    public static readonly Color Spawn = Hex("#B9443B");
    public static readonly Color Base = Hex("#3B7BD3");
    public static readonly Color TileLabel = new Color(1f, 1f, 1f, 0.95f);
    public static readonly Color PathMark = new Color(1f, 0.42f, 0.36f, 0.55f);

    // 敵
    public static readonly Color EnemyOutline = new Color(0.08f, 0.06f, 0.08f, 0.9f);
    public static readonly Color EnemyLabel = Color.white;
    public static readonly Color EnemyFallback = Hex("#C8504A");

    /// <summary>"#RRGGBB" か "#RRGGBBAA" を色にする。読めなければ fallback</summary>
    public static Color Hex(string hex, Color fallback)
    {
        Color c;
        if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out c)) return c;
        return fallback;
    }

    private static Color Hex(string hex)
    {
        return Hex(hex, Color.magenta);
    }
}
