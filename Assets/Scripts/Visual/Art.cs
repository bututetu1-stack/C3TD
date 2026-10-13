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

    public static Sprite Icon(string characterId) { return Load("Art/Icons/" + characterId, 512, false); }
    public static Sprite Enemy(string enemyId) { return Load("Art/Enemies/" + enemyId, 512, false); }
    public static Sprite Effect(string name) { return Load("Art/Effects/" + name, 512, true); }
    public static Sprite Map(string name) { return Load("Art/Map/" + name, 512, true); }
    public static Sprite Background(string name) { return Load("Art/Background/" + name, 2048, false); }

    private static Sprite Load(string path, int maxSize, bool keyOutBlack)
    {
        Sprite sprite;
        if (cache.TryGetValue(path, out sprite)) return sprite; // ないことも覚えておく

        Texture2D tex = ArtTexture.Load(path, maxSize);
        if (tex != null)
        {
            if (keyOutBlack) ArtTexture.KeyOutBlackIfOpaque(tex);
            float ppu = Mathf.Max(tex.width, tex.height);
            sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);
            sprite.name = path;
        }
        cache[path] = sprite;
        return sprite;
    }
}
