using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SE と BGM を鳴らす。ファイルは Resources/Audio/SE/{key}.wav と Resources/Audio/BGM/{key}.ogg に置く。
/// ファイルがなければ何も鳴らさない（エラーにしない）。
/// </summary>
public class AudioManager : MonoBehaviour
{
    private const string SEDir = "Audio/SE/";
    private const string BGMDir = "Audio/BGM/";

    private static AudioManager instance;

    private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private AudioSource seSource;
    private AudioSource bgmSource;
    private string currentBgm;

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
                instance.bgmSource = obj.AddComponent<AudioSource>();
                instance.bgmSource.playOnAwake = false;
                instance.bgmSource.loop = true;
            }
            return instance;
        }
    }

    public static void PlaySE(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        AudioClip clip = Instance.Load(SEDir + key);
        if (clip != null) Instance.seSource.PlayOneShot(clip);
    }

    /// <summary>BGM を切り替える。key が空かファイルがなければ止める</summary>
    public static void PlayBGM(string key)
    {
        var self = Instance;
        if (key == self.currentBgm && self.bgmSource.isPlaying) return;
        self.currentBgm = key;
        AudioClip clip = string.IsNullOrEmpty(key) ? null : self.Load(BGMDir + key);
        if (clip == null)
        {
            self.bgmSource.Stop();
            return;
        }
        self.bgmSource.clip = clip;
        self.bgmSource.Play();
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
