using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 仮素材の図形スプライトをコードで作ってキャッシュする。どの図形も 1 ユニット四方に収まる。
/// 図形は「符号付き距離」（内側が負、-1〜1 の正規化座標）で描くので、縁がなめらかになる。
/// </summary>
public static class SpriteFactory
{
    private const int Resolution = 128;

    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        cache.Clear();
    }

    /// <summary>角の丸い四角（マス用）</summary>
    public static Sprite RoundedSquare
    {
        get { return Shape("roundsquare", p => BoxDistance(p, 0.96f, 0.12f)); }
    }

    /// <summary>角が四角いままの四角（線・バー用）</summary>
    public static Sprite Square
    {
        get { return Shape("square", p => BoxDistance(p, 1f, 0f)); }
    }

    public static Sprite Circle
    {
        get { return Shape("circle", p => p.magnitude - 0.96f); }
    }

    public static Sprite Diamond
    {
        get { return Shape("diamond", p => (Mathf.Abs(p.x) + Mathf.Abs(p.y) - 0.96f) * 0.7071f); }
    }

    /// <summary>辺が上下にある六角形</summary>
    public static Sprite Hexagon
    {
        get { return Shape("hexagon", p => RegularPolygonDistance(p, 6, 0.96f, 0f)); }
    }

    public static Sprite ForShape(EnemyShape shape)
    {
        switch (shape)
        {
            case EnemyShape.Square: return RoundedSquare;
            case EnemyShape.Diamond: return Diamond;
            case EnemyShape.Hexagon: return Hexagon;
            default: return Circle;
        }
    }

    // ------------------------------------------------------------

    private static Sprite Shape(string key, Func<Vector2, float> distance)
    {
        Sprite sprite;
        if (cache.TryGetValue(key, out sprite) && sprite != null) return sprite;

        int n = Resolution;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.name = "Generated_" + key;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var pixels = new Color32[n * n];
        float pixel = 2f / n; // 1ピクセルの幅（正規化座標）
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                var p = new Vector2((x + 0.5f) * pixel - 1f, (y + 0.5f) * pixel - 1f);
                float a = Mathf.Clamp01(0.5f - distance(p) / pixel);
                pixels[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n, 0, SpriteMeshType.FullRect);
        sprite.name = key;
        cache[key] = sprite;
        return sprite;
    }

    /// <summary>半辺 half、角の半径 radius の四角までの距離</summary>
    private static float BoxDistance(Vector2 p, float half, float radius)
    {
        var q = new Vector2(Mathf.Abs(p.x) - (half - radius), Mathf.Abs(p.y) - (half - radius));
        var outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
        return outside.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
    }

    /// <summary>外接円の半径 radius の正多角形までの距離（凸多角形なので、各辺の外側への距離の最大）</summary>
    private static float RegularPolygonDistance(Vector2 p, int sides, float radius, float rotation)
    {
        float apothem = radius * Mathf.Cos(Mathf.PI / sides);
        float d = float.MinValue;
        for (int i = 0; i < sides; i++)
        {
            float angle = rotation + (i + 0.5f) * 2f * Mathf.PI / sides;
            var normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            d = Mathf.Max(d, Vector2.Dot(p, normal) - apothem);
        }
        return d;
    }
}
