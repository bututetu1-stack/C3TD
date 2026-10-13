using TMPro;
using UnityEngine;

/// <summary>マップ上に置く文字（TextMeshPro）を作る</summary>
public static class WorldText
{
    /// <summary>fontSize はおよそ「10 で 1 マスの高さ」</summary>
    public static TextMeshPro Create(Transform parent, string text, float fontSize, Color color, int sortingOrder, bool outline)
    {
        var obj = new GameObject("Text");
        obj.transform.SetParent(parent, false);
        var tmp = obj.AddComponent<TextMeshPro>();
        if (GameFonts.WorldTMP != null) tmp.font = GameFonts.WorldTMP;
        if (outline && GameFonts.OutlineMaterial != null) tmp.fontSharedMaterial = GameFonts.OutlineMaterial;
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
