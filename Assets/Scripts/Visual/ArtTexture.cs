using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resources の画像を、縮小した読み書きできるテクスチャとして読み込む（C3将棋と同じ仕組み）。
/// インポート設定で「読み書き可能」にしなくても使えるので、画像は置くだけでよい。
/// </summary>
public static class ArtTexture
{
    /// <summary>長い辺が maxSize 以下になるように縮めて読み込む（なければ null）</summary>
    public static Texture2D Load(string path, int maxSize)
    {
        Texture2D src = Resources.Load<Texture2D>(path);
        if (src == null) return null;

        float scale = Mathf.Min(1f, (float)maxSize / Mathf.Max(src.width, src.height));
        int w = Mathf.Max(1, Mathf.RoundToInt(src.width * scale));
        int h = Mathf.Max(1, Mathf.RoundToInt(src.height * scale));

        // 半分ずつ縮めて、細かい模様のちらつきを抑える
        var temps = new List<RenderTexture>();
        Texture from = src;
        int cw = src.width, ch = src.height;
        while (cw > w * 2)
        {
            cw = Mathf.Max(w, cw / 2);
            ch = Mathf.Max(h, ch / 2);
            RenderTexture step = Temporary(cw, ch);
            Graphics.Blit(from, step);
            temps.Add(step);
            from = step;
        }
        RenderTexture dst = Temporary(w, h);
        Graphics.Blit(from, dst);
        temps.Add(dst);

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = dst;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        RenderTexture.active = prev;
        foreach (RenderTexture rt in temps) RenderTexture.ReleaseTemporary(rt);
        Resources.UnloadAsset(src);

        tex.name = path;
        tex.filterMode = FilterMode.Trilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.hideFlags = HideFlags.DontSave;
        tex.Apply(true);
        return tex;
    }

    /// <summary>
    /// 透過のない画像（真っ黒な背景に描いた光・炎など）なら、黒を透明に変える。
    /// 明るいところほど不透明になるので、黒背景で描かれたエフェクトがそのまま使える。
    /// </summary>
    public static void KeyOutBlackIfOpaque(Texture2D tex)
    {
        Color[] px = tex.GetPixels();
        int seeThrough = 0;
        for (int i = 0; i < px.Length; i++)
            if (px[i].a < 0.9f) seeThrough++;
        if (seeThrough > px.Length / 1000) return; // もともと透過している

        for (int i = 0; i < px.Length; i++)
        {
            Color c = px[i];
            float a = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            px[i] = a < 0.02f ? Color.clear : new Color(c.r / a, c.g / a, c.b / a, a);
        }
        tex.SetPixels(px);
        tex.Apply(true);
    }

    /// <summary>
    /// 透過のない画像（黒い背景に描いた物の絵）なら、画像のふちから続いている黒だけを透明にする。
    /// 絵の中の暗い色（扉の茶色など）は透けない。背景との境目は明るさに応じて少しだけ透かす。
    /// </summary>
    public static void KeyOutBorderBlackIfOpaque(Texture2D tex)
    {
        const float threshold = 0.1f;
        Color[] px = tex.GetPixels();
        int seeThrough = 0;
        for (int i = 0; i < px.Length; i++)
            if (px[i].a < 0.9f) seeThrough++;
        if (seeThrough > px.Length / 1000) return; // もともと透過している

        int w = tex.width, h = tex.height;
        var background = new bool[px.Length];
        var queue = new Queue<int>();
        for (int x = 0; x < w; x++)
        {
            Seed(px, background, queue, x, 0, w, threshold);
            Seed(px, background, queue, x, h - 1, w, threshold);
        }
        for (int y = 0; y < h; y++)
        {
            Seed(px, background, queue, 0, y, w, threshold);
            Seed(px, background, queue, w - 1, y, w, threshold);
        }
        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            int x = i % w, y = i / w;
            if (x > 0) Seed(px, background, queue, x - 1, y, w, threshold);
            if (x < w - 1) Seed(px, background, queue, x + 1, y, w, threshold);
            if (y > 0) Seed(px, background, queue, x, y - 1, w, threshold);
            if (y < h - 1) Seed(px, background, queue, x, y + 1, w, threshold);
        }

        var result = new Color[px.Length];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                Color c = px[i];
                if (background[i])
                {
                    result[i] = Color.clear;
                    continue;
                }
                bool edge = (x > 0 && background[i - 1]) || (x < w - 1 && background[i + 1]) ||
                            (y > 0 && background[i - w]) || (y < h - 1 && background[i + w]);
                float v = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                if (edge && v < threshold * 3f) c.a = v / (threshold * 3f);
                result[i] = c;
            }
        }
        tex.SetPixels(result);
        tex.Apply(true);
    }

    private static void Seed(Color[] px, bool[] background, Queue<int> queue, int x, int y, int w, float threshold)
    {
        int i = y * w + x;
        if (background[i]) return;
        Color c = px[i];
        if (Mathf.Max(c.r, Mathf.Max(c.g, c.b)) >= threshold) return;
        background[i] = true;
        queue.Enqueue(i);
    }

    private static RenderTexture Temporary(int w, int h)
    {
        RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        rt.filterMode = FilterMode.Bilinear;
        return rt;
    }
}
