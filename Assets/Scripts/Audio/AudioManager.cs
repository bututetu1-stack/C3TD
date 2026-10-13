using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SE と BGM を鳴らす。ファイルは Resources/Audio/SE/{key}.wav と Resources/Audio/BGM/{key}.ogg に置く
/// （拡張子は見ないので mp3 なども鳴る）。ファイルがなければ何も鳴らさない（エラーにしない）。
/// BGM は、使う部分の終わりの CrossFade 秒前から頭（audio.json の start）を重ねてつなぎ、くり返す
/// （ループ用に作られていない曲でも途切れないように）。曲の終わりの静かな部分（フェードアウトや余韻）は自動で除く。
/// SE は前後の無音を切り、同じ音は MinInterval 秒に1回まで（攻撃の音が重なって大きくなりすぎないように）。
/// 音量と使う部分は Data/audio.json で音ごとに変えられる。
/// </summary>
public class AudioManager : MonoBehaviour
{
    private const string SEDir = "Audio/SE/";
    private const string BGMDir = "Audio/BGM/";
    private const float MinInterval = 0.05f;
    private const float CrossFade = 2.5f;

    /// <summary>SE の前後で、これより小さい音は無音とみなして切る</summary>
    private const float SilenceLevel = 0.01f;

    /// <summary>BGM の終わりで、曲のふだんの大きさのこの割合より静かな部分は除く</summary>
    private const float QuietTailRate = 0.3f;

    private static AudioManager instance;

    private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private readonly Dictionary<string, AudioClip> seClips = new Dictionary<string, AudioClip>();
    private readonly Dictionary<AudioClip, float> bgmEnds = new Dictionary<AudioClip, float>();
    private readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
    // 読み込んだときに使う部分を切り出せた SE（ブラウザ版では切り出せないので、PlayPart で鳴らす）
    private readonly HashSet<string> cut = new HashSet<string>();
    private const int MaxParts = 4;
    private const float PartFade = 0.05f;
    private class Part
    {
        public AudioSource Source;
        public float StopAt;
        public float Volume;
    }
    private readonly List<Part> parts = new List<Part>();
    private AudioSource seSource;
    private readonly AudioSource[] bgm = new AudioSource[2];
    private int bgmIndex;
    private string currentBgm;
    private float bgmVolume = 1f;
    private float bgmStart;
    private float bgmEnd;
    private float fadeElapsed = -1f;
    private float fadeLength;
    private AudioEntry bgmEntry;
    private bool boundsPending;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    private static AudioManager Instance
    {
        get
        {
            if (instance == null)
            {
                var obj = new GameObject("AudioManager");
                DontDestroyOnLoad(obj);
                instance = obj.AddComponent<AudioManager>();
                instance.seSource = obj.AddComponent<AudioSource>();
                instance.seSource.playOnAwake = false;
                for (int i = 0; i < 2; i++)
                {
                    instance.bgm[i] = obj.AddComponent<AudioSource>();
                    instance.bgm[i].playOnAwake = false;
                    instance.bgm[i].loop = true;
                }
            }
            return instance;
        }
    }

    // ================= SE =================

    public static void PlaySE(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        var self = Instance;
        AudioClip clip = self.SeClip(key);
        if (clip == null) return;
        float now = Time.unscaledTime;
        float last;
        if (self.lastPlayed.TryGetValue(key, out last) && now - last < MinInterval) return;
        self.lastPlayed[key] = now;
        AudioEntry entry = GameData.Audio.Se(key);
        float volume = entry != null ? entry.volume : 1f;
        // 切り出せなかった（ブラウザ版）のに使う部分が決まっているときは、その部分だけ鳴らす
        if (!self.cut.Contains(key) && entry != null && (entry.start > 0f || entry.end > 0f))
            self.PlayPart(clip, entry.start, entry.end, volume);
        else
            self.seSource.PlayOneShot(clip, volume);
    }

    /// <summary>ファイルの start〜end 秒だけ鳴らす（音の中身を読めず、切り出せないブラウザ版で使う）</summary>
    private void PlayPart(AudioClip clip, float start, float end, float volume)
    {
        Part part = parts.Find(p => !p.Source.isPlaying);
        if (part == null)
        {
            if (parts.Count >= MaxParts) return;
            part = new Part { Source = gameObject.AddComponent<AudioSource>() };
            part.Source.playOnAwake = false;
            parts.Add(part);
        }
        // まだ読み込み中なら長さが 0 になるので、書いた秒をそのまま使う
        float length = clip.loadState == AudioDataLoadState.Loaded ? clip.length : float.MaxValue;
        start = Mathf.Clamp(start, 0f, length);
        float to = end > start ? Mathf.Min(end, length) : length;
        if (to >= float.MaxValue) return;
        part.Volume = volume;
        part.Source.clip = clip;
        part.Source.volume = volume;
        part.Source.time = start;
        part.Source.Play();
        part.StopAt = Time.unscaledTime + (to - start);
    }

    /// <summary>PlayPart で鳴らしている音を、終わりの PartFade 秒で小さくして止める</summary>
    private void UpdateParts()
    {
        foreach (Part p in parts)
        {
            if (!p.Source.isPlaying) continue;
            float left = p.StopAt - Time.unscaledTime;
            if (left <= 0f) p.Source.Stop();
            else if (left < PartFade) p.Source.volume = p.Volume * left / PartFade;
        }
    }

    /// <summary>SE のファイルから、使う部分（audio.json の start・end）を前後の無音を除いて切り出す</summary>
    private AudioClip SeClip(string key)
    {
        AudioClip trimmed;
        if (seClips.TryGetValue(key, out trimmed)) return trimmed;
        AudioClip raw = Load(SEDir + key);
        trimmed = raw;
        float[] data = raw != null ? ReadAll(raw) : null;
        if (data != null)
        {
            cut.Add(key);
            int ch = raw.channels;
            AudioEntry entry = GameData.Audio.Se(key);
            int first = entry != null ? Mathf.Clamp(Mathf.RoundToInt(entry.start * raw.frequency), 0, raw.samples) : 0;
            int last = entry != null && entry.end > 0f ? Mathf.Clamp(Mathf.RoundToInt(entry.end * raw.frequency), first, raw.samples) : raw.samples;
            while (first < last && Peak(data, first, ch) < SilenceLevel) first++;
            while (last > first && Peak(data, last - 1, ch) < SilenceLevel) last--;
            int length = last - first;
            if (length > 0 && length < raw.samples)
            {
                var part = new float[length * ch];
                System.Array.Copy(data, first * ch, part, 0, part.Length);
                // 切ったところでプツッと鳴らないよう、終わりの 10 ミリ秒を小さくしていく
                int fade = Mathf.Min(length, raw.frequency / 100);
                for (int i = 0; i < fade; i++)
                    for (int c = 0; c < ch; c++) part[(length - 1 - i) * ch + c] *= i / (float)fade;
                trimmed = AudioClip.Create(key, length, ch, raw.frequency, false);
                trimmed.SetData(part, 0);
            }
        }
        seClips[key] = trimmed;
        return trimmed;
    }

    private static float Peak(float[] data, int sample, int channels)
    {
        float peak = 0f;
        for (int c = 0; c < channels; c++) peak = Mathf.Max(peak, Mathf.Abs(data[sample * channels + c]));
        return peak;
    }

    // ================= BGM =================

    /// <summary>BGM のファイルがあるか</summary>
    public static bool HasBGM(string key)
    {
        return !string.IsNullOrEmpty(key) && Instance.Load(BGMDir + key) != null;
    }

    /// <summary>BGM を切り替える（頭から鳴らす）。key が空かファイルがなければ止める</summary>
    public static void PlayBGM(string key)
    {
        var self = Instance;
        AudioSource current = self.bgm[self.bgmIndex];
        if (key == self.currentBgm && current.isPlaying) return;
        self.currentBgm = key;
        foreach (AudioSource s in self.bgm)
        {
            s.Stop();
            s.clip = null;
        }
        self.fadeElapsed = -1f;
        AudioClip clip = string.IsNullOrEmpty(key) ? null : self.Load(BGMDir + key);
        if (clip == null) return;

        AudioEntry entry = GameData.Audio.Bgm(key);
        self.bgmEntry = entry;
        self.bgmVolume = entry != null ? entry.volume : 1f;
        current.clip = clip;
        current.volume = self.bgmVolume;
        // 読み込み済みなら使う部分を決めてから鳴らす。ブラウザ版の BGM は鳴らし始めてから読み込まれるので、決めるのは Update で
        self.boundsPending = true;
        if (clip.loadState == AudioDataLoadState.Loaded) self.ResolveBounds(current);
        current.Play();
    }

    /// <summary>曲の長さが分かってから、使う部分（bgmStart〜bgmEnd）を決める。イントロを飛ばすならそこへ移る</summary>
    private void ResolveBounds(AudioSource source)
    {
        AudioClip clip = source.clip;
        if (clip.loadState != AudioDataLoadState.Loaded)
        {
            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            return;
        }
        AudioEntry entry = bgmEntry;
        bgmStart = entry != null ? Mathf.Clamp(entry.start, 0f, clip.length) : 0f;
        bgmEnd = entry != null && entry.end > 0f ? Mathf.Min(entry.end, clip.length) : LoudEnd(clip);
        if (bgmEnd - bgmStart < 1f) bgmEnd = clip.length;
        if (entry != null && entry.skipIntro) source.time = bgmStart;
        boundsPending = false;
    }

    void Update()
    {
        UpdateParts();
        AudioSource current = bgm[bgmIndex];
        if (current == null || current.clip == null) return;
        if (boundsPending)
        {
            ResolveBounds(current);
            if (boundsPending) return;
        }
        AudioSource next = bgm[1 - bgmIndex];

        if (fadeElapsed < 0f)
        {
            // 一時停止中（エディタの一時停止など）は何もしない。重ねそこねても loop で頭に戻る
            if (!current.isPlaying) return;
            fadeLength = Mathf.Min(CrossFade, (bgmEnd - bgmStart) * 0.25f);
            if (current.time < bgmEnd - fadeLength) return;
            next.clip = current.clip;
            next.time = bgmStart;
            next.volume = 0f;
            next.Play();
            fadeElapsed = 0f;
        }

        fadeElapsed += Time.unscaledDeltaTime;
        float t = fadeLength > 0f ? Mathf.Clamp01(fadeElapsed / fadeLength) : 1f;
        current.volume = bgmVolume * (1f - t);
        next.volume = bgmVolume * t;
        if (t >= 1f)
        {
            current.Stop();
            bgmIndex = 1 - bgmIndex;
            fadeElapsed = -1f;
        }
    }

    /// <summary>曲の終わりの静かな部分（フェードアウト・余韻・無音）を除いた、使う部分の終わり（秒）</summary>
    private float LoudEnd(AudioClip clip)
    {
        float end;
        if (bgmEnds.TryGetValue(clip, out end)) return end;
        end = clip.length;
        float[] data = ReadAll(clip);
        if (data != null)
        {
            // 0.25 秒ごとの大きさ（RMS）を測り、曲のふだんの大きさ（中央値）の QuietTailRate 倍を下回る終わりの部分を除く
            int ch = clip.channels;
            int window = Mathf.Max(1, clip.frequency / 4);
            int count = clip.samples / window;
            var rms = new float[count];
            for (int w = 0; w < count; w++)
            {
                double sum = 0;
                int from = w * window * ch, to = (w + 1) * window * ch;
                for (int i = from; i < to; i++) sum += data[i] * data[i];
                rms[w] = Mathf.Sqrt((float)(sum / (to - from)));
            }
            var sorted = (float[])rms.Clone();
            System.Array.Sort(sorted);
            float typical = count > 0 ? sorted[count / 2] : 0f;
            int lastLoud = count - 1;
            while (lastLoud > count / 2 && rms[lastLoud] < typical * QuietTailRate) lastLoud--;
            end = Mathf.Min(clip.length, (lastLoud + 1) * window / (float)clip.frequency);
        }
        bgmEnds[clip] = end;
        return end;
    }

    // ================= 共通 =================

    /// <summary>音の中身を読む（読めない読み込み方のときは null）</summary>
    private static float[] ReadAll(AudioClip clip)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // ブラウザ版では音の中身を読めない（C3_shogi で確かめた）。BGM と SE の使う部分は audio.json に秒で書く
        return null;
#else
        if (clip.loadType == AudioClipLoadType.Streaming) return null;
        if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
        var data = new float[clip.samples * clip.channels];
        return clip.GetData(data, 0) ? data : null;
#endif
    }

    private AudioClip Load(string path)
    {
        AudioClip clip;
        if (clips.TryGetValue(path, out clip)) return clip;
        clip = Resources.Load<AudioClip>(path);
        clips[path] = clip; // ないことも覚えておく
        return clip;
    }
}
