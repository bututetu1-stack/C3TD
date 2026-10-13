using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UIElements;

/// <summary>
/// フォントの読み込みと共有。UI もマップ上の文字も Zen角ゴシック New（OFL）を使う。
/// </summary>
public static class GameFonts
{
    private const string BoldPath = "Fonts/ZenKakuGothicNew-Bold";
    private const string MediumPath = "Fonts/ZenKakuGothicNew-Medium";

    private static Font bold;
    private static Font medium;
    private static TMP_FontAsset worldTMP;
    private static Material outlineMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        worldTMP = null;
        outlineMaterial = null;
    }

    public static Font Bold { get { return bold != null ? bold : (bold = Resources.Load<Font>(BoldPath)); } }
    public static Font Medium { get { return medium != null ? medium : (medium = Resources.Load<Font>(MediumPath)); } }

    // ---------------- UI Toolkit ----------------
    public static FontDefinition UIRegular { get { return FontDefinition.FromFont(Medium); } }
    public static FontDefinition UIBold { get { return FontDefinition.FromFont(Bold); } }

    // ---------------- マップ上の文字（TextMeshPro） ----------------
    public static TMP_FontAsset WorldTMP
    {
        get
        {
            if (worldTMP == null && Bold != null)
            {
                worldTMP = TMP_FontAsset.CreateFontAsset(Bold, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                    AtlasPopulationMode.Dynamic, true);
                if (worldTMP != null) worldTMP.name = "WorldGothic";
            }
            return worldTMP;
        }
    }

    /// <summary>黒い縁取りつきの共有マテリアル（図形の上の文字が読みやすいように）</summary>
    public static Material OutlineMaterial
    {
        get
        {
            if (outlineMaterial == null && WorldTMP != null)
            {
                outlineMaterial = new Material(WorldTMP.material);
                outlineMaterial.name = "WorldOutline (shared)";
                outlineMaterial.EnableKeyword("OUTLINE_ON");
                outlineMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
                outlineMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 0.85f));
                outlineMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0.12f);
            }
            return outlineMaterial;
        }
    }
}
