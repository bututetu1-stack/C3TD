using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 本素材の画像を読み込む。決まった場所に置けば次の再生から使われ、なければ null（仮素材のまま動く）。
/// どの画像も「長い辺 = 1ユニット」の大きさにそろえるので、解像度を気にせず差し替えられる。
///
///   Art/Icons/{characterId}.png    部員のアイコン（正方形）
///   Art/Enemies/{enemyId}.png      敵の絵（透過PNG）
///   Art/Effects/{名前}.png          エフェクト（黒背景なら黒を透明にする）
///   Art/Map/{Spawn|Base}.png        出撃口・拠点の印（黒背景なら黒を透明にする）
///   Art/Background/{名前}.png       背景
/// </summary>
public static class Art
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        cache.Clear();
    }

    public static Sprite Icon(string characterId) { return Load("Art/Icons/" + characterId, 512, KeyMode.None); }

    /// <summary>アイコンを丸く切り抜いたもの（マップ上の駒）。なければ null</summary>
    public static Sprite IconCircle(string characterId)
    {
        string key = "circle:" + characterId;
        Sprite sprite;
        if (cache.TryGetValue(key, out sprite)) return sprite;

        Texture2D src = ArtTexture.Load("Art/Icons/" + characterId, 256);
        if (src != null)
        {
            int size = Mathf.Min(src.width, src.height);
            int ox = (src.width - size) / 2, oy = (src.height - size) / 2;
            Color[] from = src.GetPixels(ox, oy, size, size);
            var px = new Color[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half, dy = y + 0.5f - half;
                    float edge = Mathf.Clamp01(half - Mathf.Sqrt(dx * dx + dy * dy)); // 縁を1ピクセルぶんなめらかに
                    Color c = from[y * size + x];
                    c.a *= edge;
                    px[y * size + x] = c;
                }
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.name = key;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Trilinear;
            tex.hideFlags = HideFlags.DontSave;
            tex.SetPixels(px);
            tex.Apply(true, true);
            Object.Destroy(src);
            sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = key;
        }
        cache[key] = sprite;
        return sprite;
    }
    public static Sprite Enemy(string enemyId) { return Load("Art/Enemies/" + enemyId, 512, KeyMode.Object); }
    public static Sprite Effect(string name) { return Load("Art/Effects/" + name, 512, KeyMode.Glow); }
    /// <summary>glow なら光る絵（黒を明るさに応じて透明に）、そうでなければ物の絵（縁から続く黒い背景だけ透明に）</summary>
    public static Sprite Map(string name, bool glow) { return Load("Art/Map/" + name, 512, glow ? KeyMode.Glow : KeyMode.Object); }
    public static Sprite Background(string name) { return Load("Art/Background/" + name, 2048, KeyMode.None); }

    /// <summary>透過のない画像の背景の抜き方</summary>
    private enum KeyMode { None, Glow, Object }

    private static Sprite Load(string path, int maxSize, KeyMode key)
    {
        Sprite sprite;
        if (cache.TryGetValue(path, out sprite)) return sprite; // ないことも覚えておく

        Texture2D tex = ArtTexture.Load(path, maxSize);
        if (tex != null)
        {
            if (key == KeyMode.Glow) ArtTexture.KeyOutBlackIfOpaque(tex);
            if (key == KeyMode.Object) ArtTexture.KeyOutBorderBlackIfOpaque(tex);
            float ppu = Mathf.Max(tex.width, tex.height);
            sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);
            sprite.name = path;
        }
        cache[path] = sprite;
        return sprite;
    }
}
