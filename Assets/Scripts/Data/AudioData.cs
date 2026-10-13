using System.Collections.Generic;

/// <summary>音ごとの調整（Resources/Data/audio.json）。書いていない音は、音量 1・ファイル全体を使う</summary>
public class AudioData
{
    public Dictionary<string, AudioEntry> bgm = new Dictionary<string, AudioEntry>();
    public Dictionary<string, AudioEntry> se = new Dictionary<string, AudioEntry>();

    public AudioEntry Bgm(string key) { return Find(bgm, key); }
    public AudioEntry Se(string key) { return Find(se, key); }

    private static AudioEntry Find(Dictionary<string, AudioEntry> map, string key)
    {
        AudioEntry e;
        return key != null && map != null && map.TryGetValue(key, out e) ? e : null;
    }
}

public class AudioEntry
{
    /// <summary>音量（0〜1）</summary>
    public float volume = 1f;

    /// <summary>使う部分の始まり（秒）。BGM は、くり返すときにここへ戻る</summary>
    public float start;

    /// <summary>使う部分の終わり（秒）。0 ならファイルの終わり（BGM は終わりの静かな部分を自動で除く）</summary>
    public float end;
}
