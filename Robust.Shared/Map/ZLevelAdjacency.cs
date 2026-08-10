using System;

// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

namespace Robust.Shared.Map;

/// <summary>
/// Describes a neighbor relationship between two Z-level tiles.
/// </summary>
public readonly record struct ZLevelAdjacency(
    ZLevelTileIndices Source,
    ZLevelTileIndices Target,
    ZLevelAdjacencyDirection Direction,
    bool IsOpen);

public enum ZLevelAdjacencyDirection
{
    North = 0,
    South = 1,
    East = 2,
    West = 3,
    Up = 4,
    Down = 5,
}

public static class ZLevelAdjacencyDirectionExtensions
{
    public static ZLevelAdjacencyDirection Opposite(this ZLevelAdjacencyDirection direction)
    {
        return direction switch
        {
            ZLevelAdjacencyDirection.North => ZLevelAdjacencyDirection.South,
            ZLevelAdjacencyDirection.South => ZLevelAdjacencyDirection.North,
            ZLevelAdjacencyDirection.East => ZLevelAdjacencyDirection.West,
            ZLevelAdjacencyDirection.West => ZLevelAdjacencyDirection.East,
            ZLevelAdjacencyDirection.Up => ZLevelAdjacencyDirection.Down,
            ZLevelAdjacencyDirection.Down => ZLevelAdjacencyDirection.Up,
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
    }
}
