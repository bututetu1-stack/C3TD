using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ガチャの演出の音を、コードで作って鳴らす（音の素材は要らない。2026-10-15）。
///   land 箱が落ちた・charge0〜2 ふくらむ（だんだん高く）・upgrade 昇格・burst はじける・flip めくる
///   rare2 ★2・rare3 ★3（鐘のアルペジオ）・star ★がひとつ出る・stamp NEW の判子・tap 待っているあいだの心音
///   fanfare ★3 のスポットライト（金管のような和音）・omen 確定演出（地鳴りと光）・rainbow 虹（駆け上がる鐘）・deal カードを配る
/// 演出のあいだの BGM は GachaMusic。
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
            case "fanfare": return Fanfare();
            case "omen": return Omen();
            case "rainbow": return Rainbow();
            case "deal": return Deal();
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

    /// <summary>★3：ド・ミ・ソと駆け上がって、ドミソドの和音を伸ばす（金管のように、はじめは倍音が少なく明るくなっていく）</summary>
    private static float[] Fanfare()
    {
        float[] d = Buffer(2.6f);
        float[] run = { 523.25f, 659.25f, 783.99f };
        for (int k = 0; k < run.Length; k++) Brass(d, run[k], 0.09f * k, 0.16f, 0.35f);
        foreach (float f in new[] { 523.25f, 659.25f, 783.99f, 1046.5f }) Brass(d, f, 0.3f, 2.2f, 0.22f);
        Brass(d, 261.63f, 0.3f, 2.2f, 0.2f);
        // シャラーンと光る音
        var rnd = new System.Random(11);
        float prev = 0f;
        int from = Mathf.RoundToInt(0.3f * Rate);
        for (int i = 0; from + i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float n = (float)rnd.NextDouble() * 2f - 1f;
            float hp = n - prev;
            prev = n;
            d[from + i] += hp * Mathf.Exp(-t * 2.5f) * 0.05f;
        }
        // 伸ばしている和音は、少しずつ小さく（ずっと同じ大きさだと耳に残りすぎる）
        for (int i = from; i < d.Length; i++)
        {
            float t = (i - from) / (float)Rate;
            d[i] *= Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(t / 2f));
        }
        return d;
    }

    /// <summary>金管らしい音：倍音 6 つ。鳴りはじめは高い倍音が小さく、すぐに明るくなる。少しだけゆれる</summary>
    private static void Brass(float[] d, float f, float at, float length, float volume)
    {
        int start = Mathf.RoundToInt(at * Rate), count = Mathf.RoundToInt(length * Rate);
        float phase = 0f;
        for (int i = 0; i < count && start + i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float attack = Mathf.Clamp01(t / 0.04f);
            float release = Mathf.Clamp01((length - t) / Mathf.Min(0.35f, length * 0.5f));
            float bright = Mathf.Clamp01(t / 0.12f);
            float vib = 1f + 0.004f * Mathf.Sin(2f * Mathf.PI * 5.5f * t) * Mathf.Clamp01((t - 0.3f) / 0.3f);
            phase += 2f * Mathf.PI * f * vib / Rate;
            float v = 0f;
            for (int h = 1; h <= 6; h++) v += Mathf.Sin(phase * h) / h * (h == 1 ? 1f : 0.3f + 0.7f * bright);
            d[start + i] += v * attack * release * volume;
        }
    }

    /// <summary>確定演出：ゴゴゴと地鳴りが高まり、光の柱で明るい和音が開く</summary>
    private static float[] Omen()
    {
        float[] d = Buffer(1.9f);
        var rnd = new System.Random(13);
        float lp = 0f, phase = 0f, band = 0f, bandLp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float n = (float)rnd.NextDouble() * 2f - 1f;
            lp += (n - lp) * 0.02f;
            float rumble = Mathf.Clamp01(t / 0.5f) * Mathf.Exp(-Mathf.Max(0f, t - 0.5f) * 3f);
            phase += 2f * Mathf.PI * 42f / Rate;
            // だんだん高くなるシューッという音
            float cut = Mathf.Lerp(0.02f, 0.35f, Mathf.Clamp01(t / 0.6f));
            band += (n - band) * cut;
            bandLp += (band - bandLp) * cut * 0.5f;
            float whoosh = (band - bandLp) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.7f));
            d[i] = lp * rumble * 3f + Mathf.Sin(phase) * rumble * 0.5f + whoosh * 0.5f;
        }
        foreach (float f in new[] { 1046.5f, 1318.5f, 1568f, 2093f })
            Add(d, Bell(f, 1.2f, 2.6f), 0.55f, 0.16f);
        return d;
    }

    /// <summary>虹：鐘がラ・ド・レ・ミ・ソの音階で 2 オクターブ半を一気に駆け上がる</summary>
    private static float[] Rainbow()
    {
        float[] notes = { 523.25f, 587.33f, 659.25f, 783.99f, 880f, 1046.5f, 1174.7f, 1318.5f, 1568f, 1760f, 2093f, 2349.3f, 2637f };
        float[] d = Buffer(0.045f * notes.Length + 1.2f);
        for (int k = 0; k < notes.Length; k++) Add(d, Bell(notes[k], 1.1f, 3.5f), 0.045f * k, 0.22f);
        return d;
    }

    /// <summary>カードを配る：短いシュッ（めくる音より軽く、高いほうへ流れる）</summary>
    private static float[] Deal()
    {
        float[] d = Buffer(0.16f);
        var rnd = new System.Random(17);
        float band = 0f, bandLp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float n = (float)rnd.NextDouble() * 2f - 1f;
            float cut = Mathf.Lerp(0.08f, 0.45f, t / 0.16f);
            band += (n - band) * cut;
            bandLp += (band - bandLp) * cut * 0.4f;
            d[i] = (band - bandLp) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.16f)) * 0.6f;
        }
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
