using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>ステージのマップを UI の小さな升目で描く（作戦選択の説明用）。敵の経路のマスは赤みを帯びる</summary>
public static class MapPreview
{
    public static VisualElement Build(StageData stage, float maxWidth, float maxHeight)
    {
        int h = stage.map.Count;
        int w = 0;
        foreach (string row in stage.map) w = Mathf.Max(w, row != null ? row.Length : 0);
        var grid = UIFactory.Box("map-preview");
        if (w == 0 || h == 0) return grid;

        float cell = Mathf.Floor(Mathf.Min(maxWidth / w, maxHeight / h));
        grid.style.width = cell * w;
        grid.style.height = cell * h;

        HashSet<Vector2Int> pathCells = PathCells(stage.paths);
        HashSet<Vector2Int> airCells = PathCells(stage.airPaths);
        for (int y = 0; y < h; y++)
        {
            var rowBox = UIFactory.Box("map-preview-row");
            string row = stage.map[y] ?? "";
            for (int x = 0; x < w; x++)
            {
                TileType type = TileType.Wall;
                if (x < row.Length) Tile.TryParse(row[x], out type);
                var tile = UIFactory.Box("map-preview-cell");
                tile.style.width = cell;
                tile.style.height = cell;
                Color c = ColorOf(type);
                if (type == TileType.Ground && pathCells.Contains(new Vector2Int(x, y))) c = Color.Lerp(c, Palette.Crimson, 0.35f);
                // 飛ぶ敵の経路は水色（壁や高台の上も通る）
                if (type != TileType.Spawn && type != TileType.Base && airCells.Contains(new Vector2Int(x, y))) c = Color.Lerp(c, Palette.Cyan, 0.45f);
                tile.style.backgroundColor = c;
                rowBox.Add(tile);
            }
            grid.Add(rowBox);
        }
        return grid;
    }

    private static Color ColorOf(TileType type)
    {
        switch (type)
        {
            case TileType.Ground: return Palette.Ground;
            case TileType.HighGround: return Palette.HighGround;
            case TileType.Spawn: return Palette.Spawn;
            case TileType.Base: return Palette.Base;
            default: return Palette.Wall;
        }
    }

    /// <summary>経路の点と点のあいだのマス</summary>
    private static HashSet<Vector2Int> PathCells(Dictionary<string, List<Vector2Int>> paths)
    {
        var cells = new HashSet<Vector2Int>();
        if (paths == null) return cells;
        foreach (var kv in paths)
        {
            List<Vector2Int> points = kv.Value;
            for (int i = 0; i + 1 < points.Count; i++)
            {
                Vector2 a = points[i], b = points[i + 1];
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) * 2f));
                for (int k = 0; k <= steps; k++)
                {
                    Vector2 p = Vector2.Lerp(a, b, (float)k / steps);
                    cells.Add(new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y)));
                }
            }
        }
        return cells;
    }
}
