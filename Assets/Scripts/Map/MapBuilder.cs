using System.Collections.Generic;
using UnityEngine;

/// <summary>StageMap から、マスと経路の見た目を作る</summary>
public static class MapBuilder
{
    // 描画の順番
    public const int OrderTile = 0;
    public const int OrderTileSide = 1;
    public const int OrderTileTop = 2;
    public const int OrderPath = 5;
    public const int OrderTileLabel = 6;

    private const float TileScale = 0.96f;
    private const float HighGroundLift = 0.12f; // 高台の側面の厚み（マス）

    public static Transform Build(StageMap map, Transform parent)
    {
        var root = new GameObject("Map").transform;
        root.SetParent(parent, false);

        foreach (var tile in map.AllTiles())
        {
            Vector3 pos = map.CellToWorld(tile.Cell);
            var obj = new GameObject("Tile_" + tile.Cell.x + "_" + tile.Cell.y);
            obj.transform.SetParent(root, false);
            obj.transform.localPosition = pos;

            switch (tile.Type)
            {
                case TileType.HighGround:
                    // 側面（下に少しずらした濃い四角）と上面で、段差があるように見せる
                    AddQuad(obj.transform, "Side", Palette.HighGroundSide, new Vector3(0f, -HighGroundLift * 0.5f, 0f), TileScale, OrderTileSide);
                    tile.Renderer = AddQuad(obj.transform, "Top", Palette.HighGround, new Vector3(0f, HighGroundLift * 0.5f, 0f), TileScale - HighGroundLift * 0.5f, OrderTileTop);
                    break;
                case TileType.Wall:
                    tile.Renderer = AddQuad(obj.transform, "Body", Palette.Wall, Vector3.zero, TileScale, OrderTile);
                    break;
                case TileType.Spawn:
                    tile.Renderer = AddQuad(obj.transform, "Body", Palette.Spawn, Vector3.zero, TileScale, OrderTile);
                    WorldText.Create(obj.transform, "出撃", 3.2f, Palette.TileLabel, OrderTileLabel, true);
                    break;
                case TileType.Base:
                    tile.Renderer = AddQuad(obj.transform, "Body", Palette.Base, Vector3.zero, TileScale, OrderTile);
                    WorldText.Create(obj.transform, "拠点", 3.2f, Palette.TileLabel, OrderTileLabel, true);
                    break;
                default:
                    bool alt = (tile.Cell.x + tile.Cell.y) % 2 == 0;
                    tile.Renderer = AddQuad(obj.transform, "Body", alt ? Palette.GroundAlt : Palette.Ground, Vector3.zero, TileScale, OrderTile);
                    break;
            }
        }
        return root;
    }

    /// <summary>敵の経路を点線で描く（どこを歩くか分かるように）</summary>
    public static void DrawPath(List<Vector3> points, Transform parent)
    {
        var root = new GameObject("Path").transform;
        root.SetParent(parent, false);
        const float spacing = 0.25f;
        const float dotSize = 0.09f;
        for (int i = 0; i + 1 < points.Count; i++)
        {
            Vector3 a = points[i];
            Vector3 b = points[i + 1];
            int count = Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(a, b) / spacing));
            for (int k = 0; k < count; k++)
            {
                var dot = AddQuad(root, "Dot", Palette.PathMark, Vector3.Lerp(a, b, (float)k / count), dotSize, OrderPath);
                dot.sprite = SpriteFactory.Circle;
            }
        }
    }

    private static SpriteRenderer AddQuad(Transform parent, string name, Color color, Vector3 localPos, float scale, int order)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPos;
        obj.transform.localScale = new Vector3(scale, scale, 1f);
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.RoundedSquare;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }
}
