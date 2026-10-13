using System.Collections.Generic;
using UnityEngine;

/// <summary>StageMap から、盤・マス・出撃口と拠点の印・経路の矢印を作る</summary>
public static class MapBuilder
{
    // 描画の順番
    public const int OrderBackground = -100;
    public const int OrderGrid = -90;
    public const int OrderBoard = -80;
    public const int OrderTile = 0;
    public const int OrderTileOverlay = 1;
    public const int OrderHighShadow = 2;
    public const int OrderHighSide = 3;
    public const int OrderHighTop = 4;
    public const int OrderMarkerGlow = 5;
    public const int OrderMarker = 6;
    public const int OrderPath = 8;
    public const int OrderLabel = 9;

    private const float TileScale = 0.95f;
    private const float HighLift = 0.09f;      // 高台の上面を持ち上げる量（マス）
    private const float BoardPadding = 0.28f;  // 盤のふちの幅（マス）

    public static Transform Build(StageMap map, Transform parent)
    {
        var root = new GameObject("Map").transform;
        root.SetParent(parent, false);
        BuildBackdrop(map, root);

        foreach (var tile in map.AllTiles())
        {
            var obj = new GameObject("Tile_" + tile.Cell.x + "_" + tile.Cell.y);
            obj.transform.SetParent(root, false);
            obj.transform.localPosition = map.CellToWorld(tile.Cell);
            Transform t = obj.transform;

            switch (tile.Type)
            {
                case TileType.HighGround:
                    Add(t, "Shadow", SpriteFactory.SoftShadow, Palette.Shadow, new Vector3(0.06f, -0.16f), new Vector2(1.12f, 1.05f), OrderHighShadow);
                    Add(t, "Side", SpriteFactory.RoundedSquare, Palette.HighGroundSide, new Vector3(0f, -HighLift * 0.5f), Vector2.one * TileScale, OrderHighSide);
                    tile.Renderer = Add(t, "Top", SpriteFactory.BevelTile, Palette.HighGround, new Vector3(0f, HighLift * 0.5f), new Vector2(TileScale, TileScale - HighLift * 0.5f), OrderHighTop);
                    break;

                case TileType.Wall:
                    tile.Renderer = Add(t, "Body", SpriteFactory.RoundedSquare, Palette.Wall, Vector3.zero, Vector2.one * TileScale, OrderTile);
                    Add(t, "Hatch", SpriteFactory.HatchTile, Palette.WallHatch, Vector3.zero, Vector2.one * TileScale, OrderTileOverlay);
                    break;

                case TileType.Spawn:
                    tile.Renderer = Add(t, "Body", SpriteFactory.BevelTile, Palette.Spawn, Vector3.zero, Vector2.one * TileScale, OrderTile);
                    Add(t, "Hatch", SpriteFactory.HatchTile, Palette.SpawnStripe, Vector3.zero, Vector2.one * TileScale, OrderTileOverlay);
                    BuildMarker(t, "Spawn", "出撃", "SPAWN", Palette.SpawnGlow, 90f, true);
                    break;

                case TileType.Base:
                    tile.Renderer = Add(t, "Body", SpriteFactory.BevelTile, Palette.Base, Vector3.zero, Vector2.one * TileScale, OrderTile);
                    BuildMarker(t, "Base", "拠点", "BASE", Palette.BaseGlow, 0f, false);
                    break;

                default:
                    bool alt = (tile.Cell.x + tile.Cell.y) % 2 == 0;
                    tile.Renderer = Add(t, "Body", SpriteFactory.BevelTile, alt ? Palette.GroundAlt : Palette.Ground, Vector3.zero, Vector2.one * TileScale, OrderTile);
                    break;
            }
        }
        return root;
    }

    /// <summary>敵の経路に、拠点へ流れる矢印を並べる</summary>
    public static void DrawPath(List<Vector3> points, Transform parent)
    {
        PathFlow.Create(points, parent, Palette.PathMark, OrderPath);
    }

    /// <summary>出撃口・拠点の印。Art/Map/{name}.png があればその絵を、なければ光の輪を出す</summary>
    private static void BuildMarker(Transform tile, string name, string label, string english, Color glow, float spin, bool glowArt)
    {
        Add(tile, "Glow", SpriteFactory.Glow, Palette.WithAlpha(glow, 0.45f), Vector3.zero, Vector2.one * 1.1f, OrderMarkerGlow);
        PulseRing.Create(tile, SpriteFactory.Ring, glow, 1.8f, 0.35f, 1.05f, 0f, OrderMarker);
        PulseRing.Create(tile, SpriteFactory.ThinRing, glow, 1.8f, 0.35f, 1.05f, 0.5f, OrderMarker);

        Sprite art = Art.Map(name, glowArt);
        if (art != null)
        {
            var sr = Add(tile, "Art", art, Color.white, Vector3.zero, Vector2.one * 0.92f, OrderMarker);
            if (spin != 0f) sr.gameObject.AddComponent<Spinner>().degreesPerSecond = spin;
            var text = WorldText.Create(tile, label, 2.4f, Palette.TileLabel, OrderLabel, true);
            text.transform.localPosition = new Vector3(0f, -0.33f, 0f);
            return;
        }

        var main = WorldText.Create(tile, label, 3.4f, Palette.TileLabel, OrderLabel, true);
        main.transform.localPosition = new Vector3(0f, -0.06f, 0f);
        var sub = WorldText.Create(tile, english, 1.5f, Palette.WithAlpha(Color.white, 0.75f), OrderLabel, false, true);
        sub.characterSpacing = 18f;
        sub.transform.localPosition = new Vector3(0f, 0.3f, 0f);
    }

    /// <summary>背景（絵があれば）・方眼・盤・四隅の飾り</summary>
    private static void BuildBackdrop(StageMap map, Transform root)
    {
        Rect r = map.WorldRect;
        Vector3 center = new Vector3(r.center.x, r.center.y, 0f);

        Sprite art = Art.Background("Battle");
        if (art != null)
        {
            var bg = Add(root, "BackgroundArt", art, new Color(0.5f, 0.52f, 0.58f), center, Vector2.one, OrderBackground);
            bg.gameObject.AddComponent<CoverCamera>();
        }

        // 方眼（マスの境目にそろうように、マップと同じ偶奇の大きさにする）
        var grid = Add(root, "Grid", SpriteFactory.GridCell, Palette.Grid, center, Vector2.one, OrderGrid);
        grid.drawMode = SpriteDrawMode.Tiled;
        grid.size = new Vector2(map.Width + 40, map.Height + 30);

        Vector2 boardSize = new Vector2(r.width + BoardPadding * 2f, r.height + BoardPadding * 2f);
        var shadow = Add(root, "BoardShadow", SpriteFactory.PanelFill, Palette.BoardShadow, center + new Vector3(0.1f, -0.22f), Vector2.one, OrderBoard - 2);
        shadow.drawMode = SpriteDrawMode.Sliced;
        shadow.size = boardSize + new Vector2(0.2f, 0.2f);
        var board = Add(root, "Board", SpriteFactory.PanelFill, Palette.BoardFill, center, Vector2.one, OrderBoard - 1);
        board.drawMode = SpriteDrawMode.Sliced;
        board.size = boardSize;
        var line = Add(root, "BoardLine", SpriteFactory.PanelLine, Palette.BoardLine, center, Vector2.one, OrderBoard);
        line.drawMode = SpriteDrawMode.Sliced;
        line.size = boardSize;

        // 四隅の L 字（作戦盤らしさ）
        float hx = boardSize.x * 0.5f + 0.16f;
        float hy = boardSize.y * 0.5f + 0.16f;
        const float len = 0.55f, thick = 0.06f;
        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sy = -1; sy <= 1; sy += 2)
            {
                Vector3 corner = center + new Vector3(sx * hx, sy * hy);
                Add(root, "Corner", SpriteFactory.Square, Palette.Amber, corner + new Vector3(-sx * len * 0.5f, 0f), new Vector2(len, thick), OrderBoard);
                Add(root, "Corner", SpriteFactory.Square, Palette.Amber, corner + new Vector3(0f, -sy * len * 0.5f), new Vector2(thick, len), OrderBoard);
            }
        }
    }

    private static SpriteRenderer Add(Transform parent, string name, Sprite sprite, Color color, Vector3 localPos, Vector2 scale, int order)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPos;
        obj.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }
}
