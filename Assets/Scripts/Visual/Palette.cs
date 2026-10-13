using UnityEngine;

/// <summary>見た目の色。調整はここでまとめて行う（UI の文字色などは Resources/UI/Theme.uss）</summary>
public static class Palette
{
    // ---------------- 全体 ----------------
    public static readonly Color Background = Hex("#080B13");
    public static readonly Color Grid = new Color(0.55f, 0.68f, 0.9f, 0.045f);
    public static readonly Color BoardFill = Hex("#0E131E");
    public static readonly Color BoardLine = new Color(0.55f, 0.65f, 0.85f, 0.28f);
    public static readonly Color BoardShadow = new Color(0f, 0f, 0f, 0.55f);
    public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.42f);

    // ---------------- 差し色 ----------------
    public static readonly Color Amber = Hex("#F4B63F");
    public static readonly Color AmberBright = Hex("#FFD27A");
    public static readonly Color Cyan = Hex("#45D0E4");
    public static readonly Color Crimson = Hex("#EF4A55");
    public static readonly Color Text = Hex("#EEF1F6");
    public static readonly Color TextSub = Hex("#8E98AC");
    public static readonly Color Ink = Hex("#14110A");

    // ---------------- マス ----------------
    public static readonly Color Ground = Hex("#525C70");
    public static readonly Color GroundAlt = Hex("#57627A");
    public static readonly Color HighGround = Hex("#A3AEC3");
    public static readonly Color HighGroundSide = Hex("#3F4A61");
    public static readonly Color Wall = Hex("#151A25");
    public static readonly Color WallHatch = new Color(0.6f, 0.7f, 0.9f, 0.028f);
    public static readonly Color Spawn = Hex("#B23A44");
    public static readonly Color SpawnStripe = new Color(0f, 0f, 0f, 0.22f);
    public static readonly Color SpawnGlow = Hex("#FF5A66");
    public static readonly Color Base = Hex("#2E6FD8");
    public static readonly Color BaseGlow = Hex("#66BEFF");
    public static readonly Color TileLabel = Color.white;
    public static readonly Color PathMark = new Color(1f, 0.38f, 0.42f, 0.8f);

    // ---------------- 敵 ----------------
    public static readonly Color EnemyOutline = new Color(0.06f, 0.05f, 0.08f, 0.95f);
    public static readonly Color BossOutline = Hex("#FFC94D");
    public static readonly Color BossAura = new Color(1f, 0.2f, 0.25f, 0.5f);
    public static readonly Color EnemyLabel = Color.white;
    public static readonly Color EnemyFallback = Hex("#C8504A");

    // ---------------- UI ----------------
    public static readonly Color UiPanel = new Color(0.04f, 0.055f, 0.09f, 0.9f);
    public static readonly Color UiPanelLine = new Color(1f, 1f, 1f, 0.1f);
    public static readonly Color UiBand = new Color(0.03f, 0.045f, 0.08f, 0.94f);
    public static readonly Color UiStripe = new Color(1f, 1f, 1f, 0.025f);
    public static readonly Color UiButton = new Color(0.1f, 0.13f, 0.2f, 0.95f);
    public static readonly Color UiButtonHover = new Color(0.17f, 0.21f, 0.3f, 0.98f);
    public static readonly Color UiButtonLine = new Color(1f, 1f, 1f, 0.16f);
    public static readonly Color UiShade = new Color(0.01f, 0.015f, 0.03f, 0.92f);
    public static readonly Color UiVignette = new Color(0f, 0f, 0.01f, 0.75f);
    public static readonly Color UiDim = new Color(0.01f, 0.02f, 0.04f, 0.62f);

    // ---------------- 戦闘 ----------------
    public static readonly Color EnemyHp = Hex("#FF4D5A");
    public static readonly Color AllyHp = Hex("#5BE08F");
    public static readonly Color PhysicalDamage = Hex("#FFF3D6");
    public static readonly Color ArtsDamage = Hex("#D9A8FF");
    public static readonly Color AllyDamage = Hex("#FF7A7A");
    public static readonly Color HealText = Hex("#7CFFB0");
    public static readonly Color HealGlow = Hex("#6CF2A8");
    public static readonly Color ArtsGlow = Hex("#B98CFF");
    public static readonly Color DeployGlow = Hex("#FFE3A3");
    public static readonly Color RetreatGlow = Hex("#8FD8FF");

    // ---------------- マスの強調 ----------------
    public static readonly Color HighlightPlaceable = new Color(0.27f, 0.82f, 0.9f, 0.3f);
    public static readonly Color HighlightRange = new Color(0.96f, 0.71f, 0.25f, 0.24f);
    public static readonly Color HighlightHover = new Color(1f, 1f, 1f, 0.5f);
    public static readonly Color HighlightInvalid = new Color(0.94f, 0.29f, 0.33f, 0.45f);

    /// <summary>"#RRGGBB" か "#RRGGBBAA" を色にする。読めなければ fallback</summary>
    public static Color Hex(string hex, Color fallback)
    {
        Color c;
        if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out c)) return c;
        return fallback;
    }

    public static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }

    private static Color Hex(string hex)
    {
        return Hex(hex, Color.magenta);
    }
}
