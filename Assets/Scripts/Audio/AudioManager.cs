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
    private AudioSource seSource;
    private readonly AudioSource[] bgm = new AudioSource[2];
    private int bgmIndex;
    private string currentBgm;
    private float bgmVolume = 1f;
    private float bgmStart;
    private float bgmEnd;
    private float fadeElapsed = -1f;
    private float fadeLength;

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
        self.seSource.PlayOneShot(clip, entry != null ? entry.volume : 1f);
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
        self.bgmVolume = entry != null ? entry.volume : 1f;
        self.bgmStart = entry != null ? Mathf.Clamp(entry.start, 0f, clip.length) : 0f;
        self.bgmEnd = entry != null && entry.end > 0f ? Mathf.Min(entry.end, clip.length) : self.LoudEnd(clip);
        if (self.bgmEnd - self.bgmStart < 1f) self.bgmEnd = clip.length;

        current.clip = clip;
        current.volume = self.bgmVolume;
        current.time = entry != null && entry.skipIntro ? self.bgmStart : 0f;
        current.Play();
    }

    void Update()
    {
        AudioSource current = bgm[bgmIndex];
        if (current == null || current.clip == null) return;
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
        if (clip.loadType == AudioClipLoadType.Streaming) return null;
        if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
        var data = new float[clip.samples * clip.channels];
        return clip.GetData(data, 0) ? data : null;
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
