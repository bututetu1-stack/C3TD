using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 角を斜めに落とした板（作戦盤らしい UI の土台）。塗り・縁・左の差し色・斜めの縞を Painter2D で描く。
/// 中身は普通の VisualElement と同じように Add で並べる。
/// </summary>
public class ChamferBox : VisualElement
{
    [Flags]
    public enum Corners
    {
        None = 0,
        TopLeft = 1,
        TopRight = 2,
        BottomRight = 4,
        BottomLeft = 8,
        All = 15,
    }

    private Color fill = new Color(0f, 0f, 0f, 0.85f);
    private Color stroke = Color.clear;
    private float strokeWidth = 1.5f;
    private float cut = 14f;
    private Corners corners = Corners.TopLeft | Corners.BottomRight;
    private Color accent = Color.clear;
    private float accentWidth;
    private Color stripe = Color.clear;
    private float stripeSpacing = 18f;

    public ChamferBox()
    {
        generateVisualContent += Draw;
    }

    public ChamferBox SetFill(Color color)
    {
        fill = color;
        MarkDirtyRepaint();
        return this;
    }

    public ChamferBox SetStroke(Color color, float width)
    {
        stroke = color;
        strokeWidth = width;
        MarkDirtyRepaint();
        return this;
    }

    public ChamferBox SetCut(float size, Corners which)
    {
        cut = size;
        corners = which;
        MarkDirtyRepaint();
        return this;
    }

    /// <summary>左端の差し色の帯</summary>
    public ChamferBox SetAccent(Color color, float width)
    {
        accent = color;
        accentWidth = width;
        MarkDirtyRepaint();
        return this;
    }

    /// <summary>斜めの縞模様</summary>
    public ChamferBox SetStripes(Color color, float spacing)
    {
        stripe = color;
        stripeSpacing = Mathf.Max(4f, spacing);
        MarkDirtyRepaint();
        return this;
    }

    private void Draw(MeshGenerationContext ctx)
    {
        var r = new Rect(0f, 0f, layout.width, layout.height);
        if (r.width < 1f || r.height < 1f || float.IsNaN(r.width) || float.IsNaN(r.height)) return;
        List<Vector2> shape = Polygon(r, cut, corners);
        Painter2D p = ctx.painter2D;

        FillPolygon(p, shape, fill);

        if (stripe.a > 0f)
        {
            float w = stripeSpacing * 0.5f;
            for (float x = -r.height; x < r.width; x += stripeSpacing)
            {
                var band = new List<Vector2>
                {
                    new Vector2(x, r.height),
                    new Vector2(x + w, r.height),
                    new Vector2(x + w + r.height, 0f),
                    new Vector2(x + r.height, 0f),
                };
                FillPolygon(p, Clip(band, shape), stripe);
            }
        }

        if (accent.a > 0f && accentWidth > 0f)
        {
            var bar = new List<Vector2>
            {
                new Vector2(0f, 0f),
                new Vector2(accentWidth, 0f),
                new Vector2(accentWidth, r.height),
                new Vector2(0f, r.height),
            };
            FillPolygon(p, Clip(bar, shape), accent);
        }

        if (stroke.a > 0f && strokeWidth > 0f)
        {
            float h = strokeWidth * 0.5f;
            List<Vector2> inner = Polygon(new Rect(h, h, r.width - strokeWidth, r.height - strokeWidth), Mathf.Max(0f, cut - h * 0.6f), corners);
            p.strokeColor = stroke;
            p.lineWidth = strokeWidth;
            p.lineJoin = LineJoin.Miter;
            p.BeginPath();
            p.MoveTo(inner[0]);
            for (int i = 1; i < inner.Count; i++) p.LineTo(inner[i]);
            p.ClosePath();
            p.Stroke();
        }
    }

    private static List<Vector2> Polygon(Rect r, float cut, Corners corners)
    {
        float c = Mathf.Min(cut, Mathf.Min(r.width, r.height) * 0.5f);
        var pts = new List<Vector2>();
        if ((corners & Corners.TopLeft) != 0 && c > 0f)
        {
            pts.Add(new Vector2(r.xMin, r.yMin + c));
            pts.Add(new Vector2(r.xMin + c, r.yMin));
        }
        else pts.Add(new Vector2(r.xMin, r.yMin));

        if ((corners & Corners.TopRight) != 0 && c > 0f)
        {
            pts.Add(new Vector2(r.xMax - c, r.yMin));
            pts.Add(new Vector2(r.xMax, r.yMin + c));
        }
        else pts.Add(new Vector2(r.xMax, r.yMin));

        if ((corners & Corners.BottomRight) != 0 && c > 0f)
        {
            pts.Add(new Vector2(r.xMax, r.yMax - c));
            pts.Add(new Vector2(r.xMax - c, r.yMax));
        }
        else pts.Add(new Vector2(r.xMax, r.yMax));

        if ((corners & Corners.BottomLeft) != 0 && c > 0f)
        {
            pts.Add(new Vector2(r.xMin + c, r.yMax));
            pts.Add(new Vector2(r.xMin, r.yMax - c));
        }
        else pts.Add(new Vector2(r.xMin, r.yMax));
        return pts;
    }

    private static void FillPolygon(Painter2D p, List<Vector2> pts, Color color)
    {
        if (pts == null || pts.Count < 3 || color.a <= 0f) return;
        p.fillColor = color;
        p.BeginPath();
        p.MoveTo(pts[0]);
        for (int i = 1; i < pts.Count; i++) p.LineTo(pts[i]);
        p.ClosePath();
        p.Fill();
    }

    /// <summary>凸多角形 clip の内側だけを残す（Sutherland–Hodgman）</summary>
    private static List<Vector2> Clip(List<Vector2> subject, List<Vector2> clip)
    {
        float orientation = Mathf.Sign(SignedArea(clip));
        List<Vector2> output = subject;
        for (int i = 0; i < clip.Count && output.Count > 0; i++)
        {
            Vector2 a = clip[i];
            Vector2 b = clip[(i + 1) % clip.Count];
            List<Vector2> input = output;
            output = new List<Vector2>();
            for (int j = 0; j < input.Count; j++)
            {
                Vector2 cur = input[j];
                Vector2 prev = input[(j + input.Count - 1) % input.Count];
                bool curIn = Side(a, b, cur) * orientation >= 0f;
                bool prevIn = Side(a, b, prev) * orientation >= 0f;
                if (curIn)
                {
                    if (!prevIn) output.Add(Intersect(prev, cur, a, b));
                    output.Add(cur);
                }
                else if (prevIn)
                {
                    output.Add(Intersect(prev, cur, a, b));
                }
            }
        }
        return output;
    }

    private static float Side(Vector2 a, Vector2 b, Vector2 p)
    {
        return (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
    }

    private static float SignedArea(List<Vector2> poly)
    {
        float area = 0f;
        for (int i = 0; i < poly.Count; i++)
        {
            Vector2 a = poly[i];
            Vector2 b = poly[(i + 1) % poly.Count];
            area += a.x * b.y - b.x * a.y;
        }
        return area * 0.5f;
    }

    private static Vector2 Intersect(Vector2 p1, Vector2 p2, Vector2 a, Vector2 b)
    {
        float s1 = Side(a, b, p1);
        float s2 = Side(a, b, p2);
        float t = s1 / (s1 - s2);
        return Vector2.Lerp(p1, p2, t);
    }
}
