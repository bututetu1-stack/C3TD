using UnityEngine;

/// <summary>演出の動きの緩急（t は 0〜1）</summary>
public static class Ease
{
    public static float OutCubic(float t)
    {
        t = 1f - Mathf.Clamp01(t);
        return 1f - t * t * t;
    }

    public static float InCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t;
    }

    public static float InQuad(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t;
    }

    /// <summary>少し行き過ぎてから戻る</summary>
    public static float OutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}
