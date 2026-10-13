using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UIElements;

/// <summary>UI の文字の役割</summary>
public enum UIFont
{
    Body,     // 日本語の本文・ボタン（Zen角ゴシック New Bold）
    Medium,   // 日本語の細め（Zen角ゴシック New Medium）
    Display,  // 数字・英字の見出し（Oswald Bold）。日本語は Body で出る
    Title,    // 日本語の大見出し（しっぽり明朝 B1 ExtraBold）
}

/// <summary>
/// フォントの読み込みと共有（どれも OFL。Resources/Fonts/Licenses）。
/// UI もマップ上の文字も SDF（距離場）で描くので、拡大・縮小しても縁がなめらか。
/// </summary>
public static class GameFonts
{
    private const string BoldPath = "Fonts/ZenKakuGothicNew-Bold";
    private const string MediumPath = "Fonts/ZenKakuGothicNew-Medium";
    private const string DisplayPath = "Fonts/Oswald-Bold";
    private const string TitlePath = "Fonts/ShipporiMinchoB1-ExtraBold";

    private static Font bold;
    private static Font medium;
    private static Font display;
    private static Font title;

    private static readonly Dictionary<UIFont, UnityEngine.TextCore.Text.FontAsset> uiAssets =
        new Dictionary<UIFont, UnityEngine.TextCore.Text.FontAsset>();
    private static TMP_FontAsset worldTMP;
    private static TMP_FontAsset worldDisplayTMP;
    private static Material outlineMaterial;
    private static Material displayOutlineMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        uiAssets.Clear();
        worldTMP = null;
        worldDisplayTMP = null;
        outlineMaterial = null;
        displayOutlineMaterial = null;
    }

    public static Font Bold { get { return bold != null ? bold : (bold = Resources.Load<Font>(BoldPath)); } }
    public static Font Medium { get { return medium != null ? medium : (medium = Resources.Load<Font>(MediumPath)); } }
    public static Font Display { get { return display != null ? display : (display = Resources.Load<Font>(DisplayPath)); } }
    public static Font Title { get { return title != null ? title : (title = Resources.Load<Font>(TitlePath)); } }

    // ---------------- UI Toolkit ----------------

    public static FontDefinition UI(UIFont kind)
    {
        var asset = UIAsset(kind);
        if (asset != null) return FontDefinition.FromSDFFont(asset);
        return FontDefinition.FromFont(Bold);
    }

    private static UnityEngine.TextCore.Text.FontAsset UIAsset(UIFont kind)
    {
        UnityEngine.TextCore.Text.FontAsset asset;
        if (uiAssets.TryGetValue(kind, out asset) && asset != null) return asset;

        Font font;
        switch (kind)
        {
            case UIFont.Medium: font = Medium; break;
            case UIFont.Display: font = Display; break;
            case UIFont.Title: font = Title; break;
            default: font = Bold; break;
        }
        if (font == null)
        {
            Debug.LogWarning("フォントが見つかりません: " + kind);
            return null;
        }
        asset = UnityEngine.TextCore.Text.FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
            UnityEngine.TextCore.Text.AtlasPopulationMode.Dynamic, true);
        if (asset == null) return null;
        asset.name = "UI_" + kind;
        // 実行時に作ったフォントでは Oswald の字詰めが強く掛かりすぎて「PAUS E」のようになるので切る
        if (kind == UIFont.Display) asset.getFontFeatures = false;
        uiAssets[kind] = asset;

        // 英字フォント・明朝にない文字は、ゴシックで出す
        if (kind == UIFont.Display || kind == UIFont.Title)
        {
            var fallback = UIAsset(UIFont.Body);
            if (fallback != null) asset.fallbackFontAssetTable = new List<UnityEngine.TextCore.Text.FontAsset> { fallback };
        }
        return asset;
    }

    // ---------------- マップ上の文字（TextMeshPro） ----------------

    public static TMP_FontAsset WorldTMP
    {
        get
        {
            if (worldTMP == null) worldTMP = CreateTMP(Bold, "WorldGothic");
            return worldTMP;
        }
    }

    /// <summary>英字・数字（Oswald）。日本語はゴシックで出す</summary>
    public static TMP_FontAsset WorldDisplayTMP
    {
        get
        {
            if (worldDisplayTMP == null)
            {
                worldDisplayTMP = CreateTMP(Display, "WorldDisplay");
                if (worldDisplayTMP != null) worldDisplayTMP.getFontFeatures = false; // UI と同じく字詰めを切る
                if (worldDisplayTMP != null && WorldTMP != null)
                    worldDisplayTMP.fallbackFontAssetTable = new List<TMP_FontAsset> { WorldTMP };
            }
            return worldDisplayTMP;
        }
    }

    /// <summary>黒い縁取りつきの共有マテリアル（図形の上の文字が読みやすいように）</summary>
    public static Material OutlineMaterial
    {
        get
        {
            if (outlineMaterial == null && WorldTMP != null) outlineMaterial = CreateOutline(WorldTMP, "WorldOutline (shared)");
            return outlineMaterial;
        }
    }

    /// <summary>英字・数字（Oswald）の縁取りつきマテリアル（ダメージの数字など）</summary>
    public static Material DisplayOutlineMaterial
    {
        get
        {
            if (displayOutlineMaterial == null && WorldDisplayTMP != null) displayOutlineMaterial = CreateOutline(WorldDisplayTMP, "DisplayOutline (shared)");
            return displayOutlineMaterial;
        }
    }

    private static Material CreateOutline(TMP_FontAsset font, string name)
    {
        var material = new Material(font.material);
        material.name = name;
        material.EnableKeyword("OUTLINE_ON");
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
        material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 0.85f));
        material.SetFloat(ShaderUtilities.ID_FaceDilate, 0.12f);
        return material;
    }

    private static TMP_FontAsset CreateTMP(Font font, string name)
    {
        if (font == null) return null;
        var asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
            AtlasPopulationMode.Dynamic, true);
        if (asset != null) asset.name = name;
        return asset;
    }
}
