using UnityEngine;

/// <summary>マスの種類。マップの文字と1対1で対応する</summary>
public enum TileType
{
    Ground,     // .  地上：敵が通る。近接職を配置できる
    HighGround, // H  高台：敵は通らない。遠距離職を配置できる
    Wall,       // X  壁：配置できない
    Spawn,      // S  敵の出撃口
    Base,       // B  防衛拠点
}

/// <summary>マップの1マス</summary>
public class Tile
{
    public readonly Vector2Int Cell;
    public readonly TileType Type;

    /// <summary>マスの見た目（MapBuilder が設定する）</summary>
    public SpriteRenderer Renderer;

    public Tile(Vector2Int cell, TileType type)
    {
        Cell = cell;
        Type = type;
    }

    /// <summary>敵が歩けるマスか</summary>
    public bool IsWalkable
    {
        get { return Type == TileType.Ground || Type == TileType.Spawn || Type == TileType.Base; }
    }

    public static bool TryParse(char c, out TileType type)
    {
        switch (c)
        {
            case '.': type = TileType.Ground; return true;
            case 'H': type = TileType.HighGround; return true;
            case 'X': type = TileType.Wall; return true;
            case 'S': type = TileType.Spawn; return true;
            case 'B': type = TileType.Base; return true;
        }
        type = TileType.Wall;
        return false;
    }
}
