using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 仮素材のスプライトをコードで作ってキャッシュする。どれも 1 ユニット四方に収まる。
/// 白や灰色の濃淡で作るので、SpriteRenderer.color を掛けて色を付ける。
/// 図形は「符号付き距離」（内側が負。座標は -1〜1）で描くので、縁がなめらかになる。
/// </summary>
public static class SpriteFactory
{
    private const int Resolution = 256;   // 主な図形（マス・敵）
    private const int Small = 128;        // 光・粒など

    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        cache.Clear();
    }

    // ================= 塗りの図形 =================

    public static Sprite Square
    {
        get { return Make("square", 8, (p, px) => White(1f)); }
    }

    public static Sprite RoundedSquare
    {
        get { return Make("roundsquare", Resolution, (p, px) => White(Cover(Box(p, 0.95f, 0.16f), px))); }
    }

    public static Sprite Circle
    {
        get { return Make("circle", Resolution, (p, px) => White(Cover(p.magnitude - 0.96f, px))); }
    }

    /// <summary>敵の縁取り用。形だけの白い図形</summary>
    public static Sprite ForShape(EnemyShape shape)
    {
        return Make("shape_" + shape, Resolution, (p, px) => White(Cover(ShapeDistance(shape, p), px)));
    }

    /// <summary>左上から光が当たったような陰影つきの図形（敵の体）</summary>
    public static Sprite ShadedShape(EnemyShape shape)
    {
        return Make("shaded_" + shape, Resolution, (p, px) =>
        {
            float d = ShapeDistance(shape, p);
            float lp = (p - new Vector2(-0.35f, 0.42f)).magnitude;
            float v = 1f - 0.34f * Mathf.Clamp01(lp / 1.7f);
            float rim = Mathf.Clamp01(1f + d / 0.2f);
            v *= 1f - rim * rim * 0.25f;
            return Gray(v, Cover(d, px));
        });
    }

    /// <summary>つやの白いハイライト（楕円）</summary>
    public static Sprite Highlight
    {
        get
        {
            return Make("highlight", Small, (p, px) =>
            {
                float e = (p.x / 0.95f) * (p.x / 0.95f) + (p.y / 0.6f) * (p.y / 0.6f);
                float a = Mathf.Clamp01(1f - e);
                return White(a * a);
            });
        }
    }

    // ================= 光・輪・粒 =================

    /// <summary>中心から外へ薄れる丸（影・光）</summary>
    public static Sprite SoftCircle
    {
        get
        {
            return Make("softcircle", Small, (p, px) =>
            {
                float a = Mathf.Clamp01(1f - p.magnitude);
                return White(a * a);
            });
        }
    }

    /// <summary>芯が強く、すそが長い光</summary>
    public static Sprite Glow
    {
        get
        {
            return Make("glow", Small, (p, px) =>
            {
                float r = p.magnitude;
                return White(Mathf.Exp(-r * r * 6f) * Mathf.Clamp01((1f - r) * 5f));
            });
        }
    }

    public static Sprite Ring
    {
        get { return Make("ring", Resolution, (p, px) => White(Cover(Mathf.Abs(p.magnitude - 0.88f) - 0.06f, px))); }
    }

    public static Sprite ThinRing
    {
        get { return Make("thinring", Resolution, (p, px) => White(Cover(Mathf.Abs(p.magnitude - 0.93f) - 0.025f, px))); }
    }

    /// <summary>横に長いひし形（飛び散る破片）</summary>
    public static Sprite Shard
    {
        get { return Make("shard", Small, (p, px) => White(Cover((Mathf.Abs(p.x) + Mathf.Abs(p.y) * 3.2f - 0.95f) * 0.3f, px))); }
    }

    /// <summary>右向きの「く」の字（経路の矢印）</summary>
    public static Sprite Chevron
    {
        get
        {
            return Make("chevron", Small, (p, px) =>
            {
                float d = Mathf.Min(
                    Segment(p, new Vector2(-0.42f, 0.55f), new Vector2(0.3f, 0f)),
                    Segment(p, new Vector2(0.3f, 0f), new Vector2(-0.42f, -0.55f))) - 0.17f;
                return White(Cover(d, px));
            });
        }
    }

    // ================= マス =================

    /// <summary>ふちが面取りされたパネル。上と左のふちが明るく、下と右が暗い</summary>
    public static Sprite BevelTile
    {
        get
        {
            return Make("beveltile", Resolution, (p, px) =>
            {
                const float half = 0.95f, radius = 0.16f, bevel = 0.15f;
                float d = Box(p, half, radius);
                const float e = 0.01f;
                var g = new Vector2(
                    Box(p + new Vector2(e, 0f), half, radius) - Box(p - new Vector2(e, 0f), half, radius),
                    Box(p + new Vector2(0f, e), half, radius) - Box(p - new Vector2(0f, e), half, radius));
                if (g.sqrMagnitude > 1e-10f) g.Normalize();
                float rim = Mathf.Clamp01(1f + d / bevel);
                float light = Vector2.Dot(g, new Vector2(-0.5f, 0.866f));
                float v = 0.84f + rim * rim * light * 0.34f;
                float groove = 1f - Mathf.Clamp01(Mathf.Abs(d + bevel) / (px * 1.3f));
                v -= groove * 0.08f;
                v += p.y * 0.035f;
                return Gray(v, Cover(d, px));
            });
        }
    }

    /// <summary>角の丸い四角に斜めの縞（壁・出撃口の模様）</summary>
    public static Sprite HatchTile
    {
        get
        {
            return Make("hatchtile", Resolution, (p, px) =>
            {
                float a = Cover(Box(p, 0.95f, 0.16f), px);
                float s = Mathf.Repeat((p.x + p.y) * 3.5f, 1f);
                float stripe = Mathf.Clamp01((0.25f - Mathf.Abs(s - 0.5f)) / (px * 5f) + 0.5f);
                return White(a * stripe);
            });
        }
    }

    /// <summary>ぼけた四角い影</summary>
    public static Sprite SoftShadow
    {
        get { return Make("softshadow", Small, (p, px) => White(1f - Smooth(-0.22f, 0.2f, Box(p, 0.76f, 0.25f)))); }
    }

    /// <summary>方眼の1マス（Tiled で敷き詰める）</summary>
    public static Sprite GridCell
    {
        get
        {
            return Make("gridcell", Small, (p, px) =>
            {
                float edge = 1f - Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y));
                return White(Mathf.Clamp01(1f - edge / (px * 0.9f)));
            });
        }
    }

    // ================= 9スライス（大きさを変えても角が崩れない） =================

    public static Sprite PanelFill
    {
        get { return Make("panelfill", Small, (p, px) => White(Cover(Box(p, 0.98f, 0.5f), px)), new Vector4(40, 40, 40, 40)); }
    }

    public static Sprite PanelLine
    {
        get
        {
            return Make("panelline", Small, (p, px) =>
                White(Cover(Mathf.Abs(Box(p, 0.95f, 0.47f)) - px * 1.4f, px)), new Vector4(40, 40, 40, 40));
        }
    }

    // ================= 生成 =================

    private static Sprite Make(string key, int size, Func<Vector2, float, Color> pixel, Vector4 border = default(Vector4))
    {
        Sprite sprite;
        if (cache.TryGetValue(key, out sprite) && sprite != null) return sprite;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true); // ミップマップつき：縮んで表示されても縁がガタつかない
        tex.name = "Generated_" + key;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 4;
        var pixels = new Color32[size * size];
        float px = 2f / size; // 1ピクセルの幅（正規化座標）
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + 0.5f) * px - 1f, (y + 0.5f) * px - 1f);
                pixels[y * size + x] = pixel(p, px);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(true, true);

        sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect, border);
        sprite.name = key;
        cache[key] = sprite;
        return sprite;
    }

    private static Color White(float alpha)
    {
        return new Color(1f, 1f, 1f, alpha);
    }

    private static Color Gray(float v, float alpha)
    {
        v = Mathf.Clamp01(v);
        return new Color(v, v, v, alpha);
    }

    /// <summary>距離から、縁をなめらかにした不透明度を出す</summary>
    private static float Cover(float distance, float pixel)
    {
        return Mathf.Clamp01(0.5f - distance / pixel);
    }

    private static float Smooth(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    private static float ShapeDistance(EnemyShape shape, Vector2 p)
    {
        switch (shape)
        {
            case EnemyShape.Square: return Box(p, 0.9f, 0.24f);
            case EnemyShape.Diamond: return (Mathf.Abs(p.x) + Mathf.Abs(p.y) - 0.96f) * 0.7071f;
            case EnemyShape.Hexagon: return RegularPolygon(p, 6, 0.96f);
            default: return p.magnitude - 0.96f;
        }
    }

    /// <summary>半辺 half、角の半径 radius の四角までの距離</summary>
    private static float Box(Vector2 p, float half, float radius)
    {
        var q = new Vector2(Mathf.Abs(p.x) - (half - radius), Mathf.Abs(p.y) - (half - radius));
        var outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
        return outside.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
    }

    /// <summary>外接円の半径 radius の正多角形（辺が上下）までの距離</summary>
    private static float RegularPolygon(Vector2 p, int sides, float radius)
    {
        float apothem = radius * Mathf.Cos(Mathf.PI / sides);
        float d = float.MinValue;
        for (int i = 0; i < sides; i++)
        {
            float angle = (i + 0.5f) * 2f * Mathf.PI / sides;
            d = Mathf.Max(d, Vector2.Dot(p, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))) - apothem);
        }
        return d;
    }

    /// <summary>線分 a-b までの距離</summary>
    private static float Segment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return (p - (a + ab * t)).magnitude;
    }
}
