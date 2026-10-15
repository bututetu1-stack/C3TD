using UnityEngine;

/// <summary>
/// ガチャの演出のあいだだけ流す BGM を、コードで作る（音の素材は要らない。2026-10-15）。
/// 8 秒のループ：神秘的な和音（Am9 → Fmaj7 → C → G）のパッドと、低いベースと、鐘のアルペジオに残響をかける。
/// 継ぎ目が鳴らないよう、残響はループの頭に回り込ませる。音量は BGM の設定（AudioManager.BgmLevel）に従う。
/// </summary>
public static class GachaMusic
{
    private const int Rate = 22050;
    private const float Bar = 2f;
    private static AudioClip loop;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        loop = null;
    }

    // 和音（パッドの音・Hz）とベース。1 小節 2 秒、4 小節で 1 周
    private static readonly float[][] Chords =
    {
        new[] { 220f, 261.63f, 329.63f, 493.88f },  // Am9（ラ・ド・ミ・シ）
        new[] { 174.61f, 220f, 261.63f, 329.63f },  // Fmaj7（ファ・ラ・ド・ミ）
        new[] { 196f, 261.63f, 329.63f, 392f },     // C/G（ソ・ド・ミ・ソ）
        new[] { 196f, 246.94f, 293.66f, 440f },     // G add9（ソ・シ・レ・ラ）
    };
    private static readonly float[] Bass = { 110f, 87.31f, 130.81f, 98f };
    // アルペジオで鳴らす和音の音の順（1 小節に 8 つ）
    private static readonly int[] Arp = { 0, 1, 2, 3, 2, 1, 2, 3 };

    /// <summary>ループの AudioClip（はじめて呼んだときに作る）</summary>
    public static AudioClip Loop()
    {
        if (loop != null) return loop;
        int n = Mathf.RoundToInt(Bar * Chords.Length * Rate);
        var dry = new float[n];
        for (int b = 0; b < Chords.Length; b++)
        {
            float t0 = b * Bar;
            foreach (float f in Chords[b]) Pad(dry, f, t0);
            BassNote(dry, Bass[b], t0);
            for (int k = 0; k < Arp.Length; k++)
                Bell(dry, Chords[b][Arp[k]] * 2f, t0 + k * Bar / Arp.Length, k == 0 ? 0.09f : 0.06f);
        }
        float[] data = Reverb(dry);
        float peak = 0f;
        foreach (float x in data) peak = Mathf.Max(peak, Mathf.Abs(x));
        if (peak > 0f) for (int i = 0; i < data.Length; i++) data[i] *= 0.8f / peak;
        loop = AudioClip.Create("gacha_music", n, 1, Rate, false);
        loop.SetData(data, 0);
        return loop;
    }

    /// <summary>やわらかいパッド：少しずらした 2 つの声に倍音を 3 つ。小節の前後 0.4 秒ずつ重ねてつなぐ</summary>
    private static void Pad(float[] d, float f, float t0)
    {
        const float fade = 0.4f;
        float start = t0 - fade, length = Bar + fade * 2f;
        int from = Mathf.RoundToInt(start * Rate), count = Mathf.RoundToInt(length * Rate);
        foreach (float detune in new[] { 0.997f, 1.003f })
        {
            float w = 2f * Mathf.PI * f * detune / Rate;
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)Rate;
                float env = Smoothstep(Mathf.Clamp01(t / (fade * 2f))) * Smoothstep(Mathf.Clamp01((length - t) / (fade * 2f)));
                float trem = 1f + 0.12f * Mathf.Sin(2f * Mathf.PI * 0.5f * (start + t));
                float v = Mathf.Sin(phase) + 0.35f * Mathf.Sin(phase * 2f) + 0.15f * Mathf.Sin(phase * 3f);
                Put(d, from + i, v * env * trem * 0.05f);
                phase += w;
            }
        }
    }

    /// <summary>低いベース：小節の頭でふくらみ、少しずつ小さく</summary>
    private static void BassNote(float[] d, float f, float t0)
    {
        int from = Mathf.RoundToInt(t0 * Rate), count = Mathf.RoundToInt(Bar * Rate);
        float w = 2f * Mathf.PI * f / Rate, phase = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)Rate;
            float env = Mathf.Clamp01(t / 0.08f) * (0.55f + 0.45f * Mathf.Exp(-t * 1.5f)) * Mathf.Clamp01((Bar - t) / 0.1f);
            Put(d, from + i, (Mathf.Sin(phase) + 0.25f * Mathf.Sin(phase * 2f)) * env * 0.16f);
            phase += w;
        }
    }

    /// <summary>鐘のアルペジオの 1 音</summary>
    private static void Bell(float[] d, float f, float at, float volume)
    {
        int from = Mathf.RoundToInt(at * Rate), count = Mathf.RoundToInt(1.2f * Rate);
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)Rate;
            float env = Mathf.Exp(-t * 4f) * Mathf.Clamp01(t * 300f);
            float v = Mathf.Sin(2f * Mathf.PI * f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * f * 2.76f * t) * Mathf.Exp(-t * 8f);
            Put(d, from + i, v * env * volume);
        }
    }

    /// <summary>ループの長さで折り返して足す（ループの終わりからはみ出た分は頭に回る）</summary>
    private static void Put(float[] d, int index, float value)
    {
        int n = d.Length;
        index %= n;
        if (index < 0) index += n;
        d[index] += value;
    }

    private static float Smoothstep(float x)
    {
        return x * x * (3f - 2f * x);
    }

    /// <summary>
    /// 残響（くし形フィルタ 4 つと全域通過 2 つ）。2 周ぶん流して 2 周目を使うので、残響の尾がループの頭につながる
    /// </summary>
    private static float[] Reverb(float[] dry)
    {
        int n = dry.Length;
        int[] combs = { 641, 757, 863, 977 };
        float[] feedback = { 0.78f, 0.76f, 0.74f, 0.72f };
        var wet = new float[n];
        var buffers = new float[combs.Length][];
        var pos = new int[combs.Length];
        for (int c = 0; c < combs.Length; c++) buffers[c] = new float[combs[c]];
        int[] alls = { 225, 341 };
        var ab = new float[alls.Length][];
        var apos = new int[alls.Length];
        for (int a = 0; a < alls.Length; a++) ab[a] = new float[alls[a]];
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < n; i++)
            {
                float x = dry[i] * 0.25f, y = 0f;
                for (int c = 0; c < combs.Length; c++)
                {
                    float o = buffers[c][pos[c]];
                    buffers[c][pos[c]] = x + o * feedback[c];
                    pos[c] = (pos[c] + 1) % combs[c];
                    y += o;
                }
                for (int a = 0; a < alls.Length; a++)
                {
                    float o = ab[a][apos[a]];
                    float v = y + o * 0.5f;
                    ab[a][apos[a]] = v;
                    apos[a] = (apos[a] + 1) % alls[a];
                    y = o - v * 0.5f;
                }
                if (pass == 1) wet[i] = dry[i] + y * 0.6f;
            }
        }
        return wet;
    }
}
