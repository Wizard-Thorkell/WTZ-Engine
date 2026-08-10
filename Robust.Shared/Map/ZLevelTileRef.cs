// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;

namespace Robust.Shared.Map;

/// <summary>
/// ZLevel experimental tile reference with a discrete vertical layer.
/// </summary>
[PublicAPI]
public readonly struct ZLevelTileRef : IEquatable<ZLevelTileRef>, ISpanFormattable
{
    public static ZLevelTileRef Zero => new(EntityUid.Invalid, new ZLevelTileIndices(0, 0, 0), Tile.Empty);

    public readonly EntityUid GridUid;
    public readonly ZLevelTileIndices GridIndices;
    public readonly Tile Tile;

    internal ZLevelTileRef(EntityUid gridUid, ZLevelTileIndices gridIndices, Tile tile)
    {
        GridUid = gridUid;
        GridIndices = gridIndices;
        Tile = tile;
    }

    public int X => GridIndices.X;
    public int Y => GridIndices.Y;
    public int Z => GridIndices.Z;

    public override string ToString()
    {
        return $"ZLevelTileRef: {X},{Y},{Z} ({Tile})";
    }

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return ToString();
    }

    public bool TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider)
    {
        return FormatHelpers.TryFormatInto(
            destination,
            out charsWritten,
            $"ZLevelTileRef: {X},{Y},{Z} ({Tile})");
    }

    public bool Equals(ZLevelTileRef other)
    {
        return GridUid.Equals(other.GridUid) &&
               GridIndices.Equals(other.GridIndices) &&
               Tile.Equals(other.Tile);
    }

    public override bool Equals(object? obj)
    {
        return obj is ZLevelTileRef other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(GridUid, GridIndices, Tile);
    }

    public static bool operator ==(ZLevelTileRef a, ZLevelTileRef b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(ZLevelTileRef a, ZLevelTileRef b)
    {
        return !a.Equals(b);
    }
}
