using TMPro;
using UnityEngine;

/// <summary>マップ上に置く文字（TextMeshPro）を作る</summary>
public static class WorldText
{
    /// <summary>fontSize はおよそ「10 で 1 マスの高さ」。display なら英字・数字用のフォント</summary>
    public static TextMeshPro Create(Transform parent, string text, float fontSize, Color color, int sortingOrder, bool outline, bool display = false)
    {
        var obj = new GameObject("Text");
        obj.transform.SetParent(parent, false);
        var tmp = obj.AddComponent<TextMeshPro>();
        TMP_FontAsset font = display ? GameFonts.WorldDisplayTMP : GameFonts.WorldTMP;
        if (font != null) tmp.font = font;
        Material outlineMaterial = display ? GameFonts.DisplayOutlineMaterial : GameFonts.OutlineMaterial;
        if (outline && outlineMaterial != null) tmp.fontSharedMaterial = outlineMaterial;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.rectTransform.sizeDelta = new Vector2(1f, 0.5f);
        tmp.sortingOrder = sortingOrder;
        return tmp;
    }
}
