using UnityEngine;
using UnityEngine.UIElements;

public enum IconKind
{
    Pause,
    Play,
    FastForward,
    Clock,
    Paper,   // 敵（課題の紙）
    Heart,
    Retry,
    Next,
    Cross,   // 撤退
    Bolt,    // 必殺技
    Back,    // 戻る
    Lock,    // 未開放
    Star,    // ★
}

/// <summary>Painter2D で描く小さなアイコン（フォントに頼らないので、どの環境でも同じ形）</summary>
public class IconView : VisualElement
{
    private IconKind kind;
    private Color color;

    public IconView(IconKind kind, float size, Color color)
    {
        this.kind = kind;
        this.color = color;
        style.width = size;
        style.height = size;
        style.flexShrink = 0;
        pickingMode = PickingMode.Ignore;
        generateVisualContent += Draw;
    }

    public void SetKind(IconKind value)
    {
        if (kind == value) return;
        kind = value;
        MarkDirtyRepaint();
    }

    public void SetColor(Color value)
    {
        if (color == value) return;
        color = value;
        MarkDirtyRepaint();
    }

    private void Draw(MeshGenerationContext ctx)
    {
        float s = Mathf.Min(layout.width, layout.height);
        if (s < 1f || float.IsNaN(s)) return;
        var c = new Vector2(layout.width * 0.5f, layout.height * 0.5f);
        Painter2D p = ctx.painter2D;
        p.fillColor = color;
        p.strokeColor = color;
        p.lineJoin = LineJoin.Round;
        p.lineCap = LineCap.Round;

        switch (kind)
        {
            case IconKind.Pause:
                Rect(p, c.x - s * 0.26f, c.y - s * 0.3f, s * 0.17f, s * 0.6f);
                Rect(p, c.x + s * 0.09f, c.y - s * 0.3f, s * 0.17f, s * 0.6f);
                break;

            case IconKind.Play:
                Poly(p, new Vector2(c.x - s * 0.2f, c.y - s * 0.32f), new Vector2(c.x + s * 0.32f, c.y), new Vector2(c.x - s * 0.2f, c.y + s * 0.32f));
                break;

            case IconKind.FastForward:
                Poly(p, new Vector2(c.x - s * 0.38f, c.y - s * 0.28f), new Vector2(c.x, c.y), new Vector2(c.x - s * 0.38f, c.y + s * 0.28f));
                Poly(p, new Vector2(c.x - s * 0.02f, c.y - s * 0.28f), new Vector2(c.x + s * 0.36f, c.y), new Vector2(c.x - s * 0.02f, c.y + s * 0.28f));
                break;

            case IconKind.Clock:
                p.lineWidth = s * 0.1f;
                p.BeginPath();
                p.Arc(c, s * 0.36f, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Stroke();
                p.BeginPath();
                p.MoveTo(new Vector2(c.x, c.y - s * 0.2f));
                p.LineTo(c);
                p.LineTo(new Vector2(c.x + s * 0.16f, c.y + s * 0.08f));
                p.Stroke();
                break;

            case IconKind.Paper:
            {
                float x0 = c.x - s * 0.27f, x1 = c.x + s * 0.27f, y0 = c.y - s * 0.36f, y1 = c.y + s * 0.36f, f = s * 0.17f;
                Poly(p, new Vector2(x0, y0), new Vector2(x1 - f, y0), new Vector2(x1, y0 + f), new Vector2(x1, y1), new Vector2(x0, y1));
                p.strokeColor = new Color(0f, 0f, 0f, 0.55f);
                p.lineWidth = s * 0.06f;
                for (int i = 0; i < 3; i++)
                {
                    float y = y0 + s * (0.26f + i * 0.14f);
                    p.BeginPath();
                    p.MoveTo(new Vector2(x0 + s * 0.1f, y));
                    p.LineTo(new Vector2(x1 - s * 0.1f, y));
                    p.Stroke();
                }
                break;
            }

            case IconKind.Heart:
            {
                var bottom = new Vector2(c.x, c.y + s * 0.34f);
                var top = new Vector2(c.x, c.y - s * 0.16f);
                p.BeginPath();
                p.MoveTo(bottom);
                p.BezierCurveTo(new Vector2(c.x - s * 0.52f, c.y + s * 0.02f), new Vector2(c.x - s * 0.36f, c.y - s * 0.44f), top);
                p.BezierCurveTo(new Vector2(c.x + s * 0.36f, c.y - s * 0.44f), new Vector2(c.x + s * 0.52f, c.y + s * 0.02f), bottom);
                p.ClosePath();
                p.Fill();
                break;
            }

            case IconKind.Retry:
                p.lineWidth = s * 0.11f;
                p.BeginPath();
                p.Arc(c, s * 0.3f, Angle.Degrees(-60f), Angle.Degrees(220f));
                p.Stroke();
                {
                    float a = -60f * Mathf.Deg2Rad;
                    var tip = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s * 0.3f;
                    Poly(p, tip + new Vector2(s * 0.16f, s * 0.02f), tip + new Vector2(-s * 0.1f, -s * 0.12f), tip + new Vector2(-s * 0.02f, s * 0.16f));
                }
                break;

            case IconKind.Back:
                p.lineWidth = s * 0.12f;
                p.BeginPath();
                p.MoveTo(new Vector2(c.x + s * 0.3f, c.y));
                p.LineTo(new Vector2(c.x - s * 0.24f, c.y));
                p.Stroke();
                Poly(p, new Vector2(c.x - s * 0.02f, c.y - s * 0.24f), new Vector2(c.x - s * 0.34f, c.y), new Vector2(c.x - s * 0.02f, c.y + s * 0.24f));
                break;

            case IconKind.Lock:
                p.lineWidth = s * 0.1f;
                p.BeginPath();
                p.Arc(new Vector2(c.x, c.y - s * 0.06f), s * 0.18f, Angle.Degrees(180f), Angle.Degrees(360f));
                p.Stroke();
                Rect(p, c.x - s * 0.28f, c.y - s * 0.04f, s * 0.56f, s * 0.4f);
                break;

            case IconKind.Star:
            {
                var pts = new Vector2[10];
                for (int i = 0; i < 10; i++)
                {
                    float r = i % 2 == 0 ? s * 0.46f : s * 0.19f;
                    float a = (-90f + i * 36f) * Mathf.Deg2Rad;
                    pts[i] = new Vector2(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r + s * 0.03f);
                }
                Poly(p, pts);
                break;
            }

            case IconKind.Bolt:
                Poly(p,
                    new Vector2(c.x + s * 0.08f, c.y - s * 0.42f),
                    new Vector2(c.x - s * 0.26f, c.y + s * 0.06f),
                    new Vector2(c.x - s * 0.02f, c.y + s * 0.06f),
                    new Vector2(c.x - s * 0.1f, c.y + s * 0.42f),
                    new Vector2(c.x + s * 0.26f, c.y - s * 0.08f),
                    new Vector2(c.x + s * 0.02f, c.y - s * 0.08f));
                break;

            case IconKind.Cross:
                p.lineWidth = s * 0.14f;
                p.BeginPath();
                p.MoveTo(new Vector2(c.x - s * 0.26f, c.y - s * 0.26f));
                p.LineTo(new Vector2(c.x + s * 0.26f, c.y + s * 0.26f));
                p.MoveTo(new Vector2(c.x + s * 0.26f, c.y - s * 0.26f));
                p.LineTo(new Vector2(c.x - s * 0.26f, c.y + s * 0.26f));
                p.Stroke();
                break;

            case IconKind.Next:
                p.lineWidth = s * 0.12f;
                p.BeginPath();
                p.MoveTo(new Vector2(c.x - s * 0.3f, c.y));
                p.LineTo(new Vector2(c.x + s * 0.24f, c.y));
                p.Stroke();
                Poly(p, new Vector2(c.x + s * 0.02f, c.y - s * 0.24f), new Vector2(c.x + s * 0.34f, c.y), new Vector2(c.x + s * 0.02f, c.y + s * 0.24f));
                break;
        }
    }

    private static void Rect(Painter2D p, float x, float y, float w, float h)
    {
        Poly(p, new Vector2(x, y), new Vector2(x + w, y), new Vector2(x + w, y + h), new Vector2(x, y + h));
    }

    private static void Poly(Painter2D p, params Vector2[] pts)
    {
        p.BeginPath();
        p.MoveTo(pts[0]);
        for (int i = 1; i < pts.Length; i++) p.LineTo(pts[i]);
        p.ClosePath();
        p.Fill();
    }
}
