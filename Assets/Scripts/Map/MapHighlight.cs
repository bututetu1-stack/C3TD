using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// マスに重ねる色：置けるマス（ドラッグ中）・射程・指しているマス。
/// 1マスに1枚の板を用意しておき、毎フレーム色を付け替える。
/// </summary>
public class MapHighlight
{
    private const int Order = 7; // マスより上、経路の矢印より下

    private readonly StageMap map;
    private readonly Dictionary<Vector2Int, SpriteRenderer> overlays = new Dictionary<Vector2Int, SpriteRenderer>();
    private readonly HashSet<Vector2Int> placeable = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> range = new HashSet<Vector2Int>();

    public MapHighlight(StageMap map, Transform parent)
    {
        this.map = map;
        var root = new GameObject("Highlights").transform;
        root.SetParent(parent, false);
        foreach (Tile tile in map.AllTiles())
        {
            var obj = new GameObject("Highlight_" + tile.Cell.x + "_" + tile.Cell.y);
            obj.transform.SetParent(root, false);
            obj.transform.localPosition = map.CellToWorld(tile.Cell) + new Vector3(0f, tile.Lift, 0f);
            obj.transform.localScale = Vector3.one * 0.9f;
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.RoundedSquare;
            sr.sortingOrder = Order;
            sr.enabled = false;
            overlays[tile.Cell] = sr;
        }
    }

    public void Clear()
    {
        placeable.Clear();
        range.Clear();
        Apply(null, false);
    }

    /// <summary>置けるマス・射程・指しているマスを表示する（null や空なら出さない）</summary>
    public void Show(IEnumerable<Vector2Int> placeableCells, IEnumerable<Vector2Int> rangeCells, Vector2Int? hover, bool hoverValid)
    {
        placeable.Clear();
        range.Clear();
        if (placeableCells != null) placeable.UnionWith(placeableCells);
        if (rangeCells != null) range.UnionWith(rangeCells);
        Apply(hover, hoverValid);
    }

    private void Apply(Vector2Int? hover, bool hoverValid)
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
        foreach (var kv in overlays)
        {
            SpriteRenderer sr = kv.Value;
            Color c;
            if (hover.HasValue && hover.Value == kv.Key) c = hoverValid ? Palette.HighlightHover : Palette.HighlightInvalid;
            else if (range.Contains(kv.Key)) c = Palette.HighlightRange;
            else if (placeable.Contains(kv.Key)) c = Palette.WithAlpha(Palette.HighlightPlaceable, Palette.HighlightPlaceable.a * (0.6f + 0.4f * pulse));
            else
            {
                sr.enabled = false;
                continue;
            }
            sr.enabled = true;
            sr.color = c;
        }
    }
}
