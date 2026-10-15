using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ガチャの演出の音を、コードで作って鳴らす（音の素材は要らない。2026-10-15）。
///   land 箱が落ちた・charge0〜2 ふくらむ（だんだん高く）・upgrade 昇格・burst はじける・flip めくる
///   rare2 ★2・rare3 ★3（鐘のアルペジオ）・star ★がひとつ出る・stamp NEW の判子・tap 待っているあいだの心音
/// 音量は効果音の設定（AudioManager.SeLevel）に従う。
/// </summary>
public static class GachaSound
{
    private const int Rate = 44100;
    private static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        clips.Clear();
    }

    public static void Play(string name, float volume = 0.8f)
    {
        AudioClip clip = Get(name);
        if (clip != null) AudioManager.PlayClip(clip, volume);
    }

    private static AudioClip Get(string name)
    {
        AudioClip clip;
        if (clips.TryGetValue(name, out clip)) return clip;
        float[] data = Make(name);
        if (data != null)
        {
            // どの音も、頭と終わりをなめらかに（途中で切れた余韻がプツッと鳴らないよう）
            Smooth(data);
            // 割れないよう、いちばん大きいところを 0.9 にそろえる（小さい音は大きくしない）
            float peak = 0f;
            foreach (float x in data) peak = Mathf.Max(peak, Mathf.Abs(x));
            if (peak > 0.9f) for (int i = 0; i < data.Length; i++) data[i] *= 0.9f / peak;
            clip = AudioClip.Create("gacha_" + name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
        }
        clips[name] = clip;
        return clip;
    }

    private static float[] Make(string name)
    {
        switch (name)
        {
            case "land": return Land();
            case "charge0": return Charge(0);
            case "charge1": return Charge(1);
            case "charge2": return Charge(2);
            case "upgrade": return Upgrade();
            case "burst": return Burst();
            case "flip": return Flip();
            case "rare2": return Chime(new[] { 784f, 1175f }, 0.09f, 0.9f);
            case "rare3": return Chime(new[] { 1047f, 1319f, 1568f, 2093f, 2637f }, 0.075f, 1.6f);
            case "star": return Chime(new[] { 1568f }, 0f, 0.5f);
            case "stamp": return Stamp();
            case "tap": return Heartbeat();
            default: return null;
        }
    }

    // ================= 音 =================

    private static float[] Buffer(float seconds)
    {
        return new float[Mathf.CeilToInt(seconds * Rate)];
    }

    /// <summary>箱が落ちた：低いドンと砂ぼこり</summary>
    private static float[] Land()
    {
        float[] d = Buffer(0.45f);
        var rnd = new System.Random(1);
        float phase = 0f, noise = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float f = Mathf.Lerp(90f, 38f, Mathf.Clamp01(t / 0.3f));
            phase += 2f * Mathf.PI * f / Rate;
            noise = noise * 0.9f + ((float)rnd.NextDouble() * 2f - 1f) * 0.1f;
            d[i] = Mathf.Sin(phase) * Mathf.Exp(-t * 9f) * 0.9f + noise * Mathf.Exp(-t * 14f) * 1.2f;
        }
        return d;
    }

    /// <summary>ふくらむ：上がっていく音と、きらきら（level が上がるほど高く）</summary>
    private static float[] Charge(int level)
    {
        float[] d = Buffer(0.5f);
        float from = 330f * (1f + 0.33f * level), to = 760f * (1f + 0.33f * level);
        float phase = 0f, phase2 = 0f;
        var rnd = new System.Random(10 + level);
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float k = t / 0.5f;
            float f = Mathf.Lerp(from, to, k * k);
            phase += 2f * Mathf.PI * f / Rate;
            phase2 += 2f * Mathf.PI * f * 2.01f / Rate;
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(k)) * (0.6f + 0.4f * k);
            d[i] = (Mathf.Sin(phase) * 0.5f + Mathf.Sin(phase2) * 0.2f) * env * 0.7f;
        }
        // きらきら：高い短い音をところどころに（1 サンプルだけの点はプチッと鳴るので使わない）
        for (int n = 0; n < 8 + level * 3; n++)
        {
            float at = (float)rnd.NextDouble() * 0.42f;
            Add(d, Ping(2600f + (float)rnd.NextDouble() * 2400f), at, 0.12f + 0.06f * level);
        }
        return Smooth(d);
    }

    /// <summary>昇格：一気に駆け上がる音と鐘</summary>
    private static float[] Upgrade()
    {
        float[] d = Buffer(1.25f);
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float k = Mathf.Clamp01(t / 0.45f);
            float f = Mathf.Lerp(250f, 1600f, k * k);
            phase += 2f * Mathf.PI * f / Rate;
            float env = t < 0.45f ? 0.3f + 0.7f * k : Mathf.Exp(-(t - 0.45f) * 8f);
            d[i] = (Mathf.Sin(phase) + 0.35f * Mathf.Sin(phase * 2f)) * env * 0.45f;
        }
        Add(d, Chime(new[] { 1568f, 2093f }, 0.05f, 0.6f), 0.42f, 0.7f);
        return d;
    }

    /// <summary>はじける：ドーンと、ザーッと、明るい和音</summary>
    private static float[] Burst()
    {
        float[] d = Buffer(1.6f);
        var rnd = new System.Random(3);
        float phase = 0f, lp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float f = Mathf.Lerp(70f, 32f, Mathf.Clamp01(t / 0.5f));
            phase += 2f * Mathf.PI * f / Rate;
            float n = (float)rnd.NextDouble() * 2f - 1f;
            lp += (n - lp) * Mathf.Lerp(0.9f, 0.08f, Mathf.Clamp01(t / 0.6f));
            d[i] = Mathf.Sin(phase) * Mathf.Exp(-t * 4f) * 0.9f + lp * Mathf.Exp(-t * 3.5f) * 0.7f;
        }
        // 和音（ド・ミ・ソ・ド）
        foreach (float f in new[] { 523f, 659f, 784f, 1047f })
            Add(d, Bell(f, 1.4f, 2.2f), 0.02f, 0.18f);
        return d;
    }

    /// <summary>めくる：短いシュッ</summary>
    private static float[] Flip()
    {
        float[] d = Buffer(0.12f);
        var rnd = new System.Random(5);
        float prev = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float n = (float)rnd.NextDouble() * 2f - 1f;
            float hp = n - prev;
            prev = n;
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.12f));
            d[i] = hp * env * 0.35f + Mathf.Sin(2f * Mathf.PI * 1400f * t) * Mathf.Exp(-t * 60f) * 0.25f;
        }
        return d;
    }

    /// <summary>NEW の判子：ポンと短く低い音</summary>
    private static float[] Stamp()
    {
        float[] d = Buffer(0.3f);
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float f = Mathf.Lerp(220f, 140f, Mathf.Clamp01(t / 0.15f));
            d[i] = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 18f) * 0.8f;
        }
        Add(d, Bell(1760f, 0.25f, 12f), 0f, 0.2f);
        return d;
    }

    /// <summary>押すのを待つあいだの、どくん・どくん</summary>
    private static float[] Heartbeat()
    {
        float[] d = Buffer(0.6f);
        foreach (float at in new[] { 0f, 0.22f })
        {
            int start = Mathf.RoundToInt(at * Rate);
            for (int i = 0; start + i < d.Length && i < Rate * 0.18f; i++)
            {
                float t = i / (float)Rate;
                d[start + i] += Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(65f, 45f, t / 0.18f) * t) * Mathf.Exp(-t * 22f) * (at == 0f ? 0.9f : 0.6f);
            }
        }
        return d;
    }

    /// <summary>鐘の音を notes の順に gap 秒ずつずらして重ねる</summary>
    private static float[] Chime(float[] notes, float gap, float length)
    {
        float[] d = Buffer(length + gap * notes.Length);
        for (int n = 0; n < notes.Length; n++) Add(d, Bell(notes[n], length, 3.2f), gap * n, 0.4f);
        return d;
    }

    /// <summary>鐘：基音と、少しずれた倍音が減衰する</summary>
    private static float[] Bell(float f, float length, float decay)
    {
        float[] d = Buffer(length);
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float env = Mathf.Exp(-t * decay) * Mathf.Clamp01(t * 400f);
            d[i] = (Mathf.Sin(2f * Mathf.PI * f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * f * 2.76f * t) * Mathf.Exp(-t * 6f)
                + 0.25f * Mathf.Sin(2f * Mathf.PI * f * 5.4f * t) * Mathf.Exp(-t * 12f)) * env;
        }
        return d;
    }

    /// <summary>きらっ：高い音が 0.06 秒ほどで消える</summary>
    private static float[] Ping(float f)
    {
        float[] d = Buffer(0.06f);
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            d[i] = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 70f) * Mathf.Clamp01(t * 2000f);
        }
        return d;
    }

    private static void Add(float[] into, float[] sound, float at, float volume)
    {
        int start = Mathf.RoundToInt(at * Rate);
        for (int i = 0; i < sound.Length && start + i < into.Length; i++) into[start + i] += sound[i] * volume;
    }

    /// <summary>ブツッと鳴らないよう、頭と終わりを少しなめらかに</summary>
    private static float[] Smooth(float[] d)
    {
        int fade = Mathf.Min(d.Length / 2, Rate / 200);
        for (int i = 0; i < fade; i++)
        {
            float k = i / (float)fade;
            d[i] *= k;
            d[d.Length - 1 - i] *= k;
        }
        return d;
    }
}
