using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// テキストから作ったマップ。マス座標は [列, 行]（行は上から0）で、
/// ワールド座標は1マス=1ユニット、左下のマスの中心が (0, 0)。
/// </summary>
public class StageMap
{
    public readonly int Width;
    public readonly int Height;
    private readonly Tile[,] tiles;

    private StageMap(int width, int height)
    {
        Width = width;
        Height = height;
        tiles = new Tile[width, height];
    }

    /// <summary>マップの文字列から作る。行の長さがそろっていなければ、足りない分を壁で埋めて警告する</summary>
    public static StageMap FromText(IList<string> rows, string stageId)
    {
        int height = rows.Count;
        int width = 0;
        for (int y = 0; y < height; y++) width = Mathf.Max(width, rows[y] != null ? rows[y].Length : 0);

        var map = new StageMap(width, height);
        for (int y = 0; y < height; y++)
        {
            string row = rows[y] ?? "";
            if (row.Length != width)
                Debug.LogWarning(stageId + ": マップの " + y + " 行目の長さが " + row.Length + " です（ほかの行は " + width + "）。足りない分は壁にします");
            for (int x = 0; x < width; x++)
            {
                TileType type = TileType.Wall;
                if (x < row.Length && !Tile.TryParse(row[x], out type))
                    Debug.LogWarning(stageId + ": マップの [" + x + "," + y + "] の文字 '" + row[x] + "' は使えません。壁にします");
                map.tiles[x, y] = new Tile(new Vector2Int(x, y), type);
            }
        }
        return map;
    }

    public bool InBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;
    }

    public Tile Get(Vector2Int cell)
    {
        return InBounds(cell) ? tiles[cell.x, cell.y] : null;
    }

    public IEnumerable<Tile> AllTiles()
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                yield return tiles[x, y];
    }

    public Vector3 CellToWorld(Vector2Int cell)
    {
        return new Vector3(cell.x, Height - 1 - cell.y, 0f);
    }

    public Vector2Int WorldToCell(Vector3 world)
    {
        return new Vector2Int(Mathf.RoundToInt(world.x), Height - 1 - Mathf.RoundToInt(world.y));
    }

    /// <summary>マップ全体を囲む範囲（ワールド座標）</summary>
    public Rect WorldRect
    {
        get { return new Rect(-0.5f, -0.5f, Width, Height); }
    }

    /// <summary>
    /// 経路を確かめて、おかしなところを警告する（範囲外・歩けないマスを通る・出撃口や拠点で始まり終わらない）。
    /// 範囲外の点があれば false を返す。
    /// </summary>
    public bool ValidatePath(string stageId, string pathId, List<Vector2Int> path, bool air = false)
    {
        string where = stageId + ": 経路 \"" + pathId + "\"";
        if (path == null || path.Count < 2)
        {
            Debug.LogError(where + " には2点以上が必要です");
            return false;
        }
        for (int i = 0; i < path.Count; i++)
        {
            if (!InBounds(path[i]))
            {
                Debug.LogError(where + " の " + i + " 番目の点 " + path[i] + " がマップの外です");
                return false;
            }
        }
        if (Get(path[0]).Type != TileType.Spawn)
            Debug.LogWarning(where + " の始点 " + path[0] + " が出撃口（S）ではありません");
        if (Get(path[path.Count - 1]).Type != TileType.Base)
            Debug.LogWarning(where + " の終点 " + path[path.Count - 1] + " が拠点（B）ではありません");

        // 点と点のあいだで、歩けないマスを通っていないか（斜めの線は細かく区切って調べる）。飛ぶ敵の経路は調べない
        if (air) return true;
        for (int i = 0; i + 1 < path.Count; i++)
        {
            Vector2 a = path[i];
            Vector2 b = path[i + 1];
            int steps = Mathf.CeilToInt(Vector2.Distance(a, b) * 4f);
            for (int k = 0; k <= steps; k++)
            {
                Vector2 p = steps == 0 ? a : Vector2.Lerp(a, b, (float)k / steps);
                var cell = new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));
                Tile tile = Get(cell);
                if (tile != null && !tile.IsWalkable)
                {
                    Debug.LogWarning(where + " が " + path[i] + "→" + path[i + 1] + " のあいだで、歩けないマス " + cell + "（" + tile.Type + "）を通ります");
                    break;
                }
            }
        }
        return true;
    }
}
