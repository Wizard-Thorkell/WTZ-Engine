// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Robust.Shared.GameObjects;

/// <summary>
/// ZLevel experimental parallel 3D tile-query APIs.
/// Legacy 2D map APIs continue to address only Z layer 0.
/// </summary>
public abstract partial class SharedMapSystem
{
    public static readonly ZLevelTileIndices[] ZLevelNeighborOffsets =
    {
        new(1, 0, 0),
        new(-1, 0, 0),
        new(0, 1, 0),
        new(0, -1, 0),
        new(0, 0, 1),
        new(0, 0, -1),
    };

    public ZLevelTileRef GetZLevelTileRef(Entity<MapGridComponent> grid, ZLevelTileIndices tileCoordinates)
    {
        return GetZLevelTileRef(grid.Owner, grid.Comp, tileCoordinates);
    }

    public ZLevelTileRef GetZLevelTileRef(EntityUid uid, MapGridComponent grid, ZLevelTileIndices tileCoordinates)
    {
        if (!TryGetChunkAndOffset(uid, grid, new Vector2i(tileCoordinates.X, tileCoordinates.Y), out var chunk, out var chunkTileIndices))
            return new ZLevelTileRef(uid, tileCoordinates, Tile.Empty);

        var tile = chunk.GetTile((ushort) chunkTileIndices.X, (ushort) chunkTileIndices.Y, tileCoordinates.Z);
        return new ZLevelTileRef(uid, tileCoordinates, tile);
    }

    public bool TryGetZLevelTileRef(EntityUid uid, MapGridComponent grid, ZLevelTileIndices indices, out ZLevelTileRef tile)
    {
        if (!TryGetChunkAndOffset(uid, grid, new Vector2i(indices.X, indices.Y), out var chunk, out var chunkTileIndices))
        {
            tile = ZLevelTileRef.Zero;
            return false;
        }

        tile = new ZLevelTileRef(uid, indices, chunk.GetTile((ushort) chunkTileIndices.X, (ushort) chunkTileIndices.Y, indices.Z));
        return true;
    }

    public void SetZLevelTile(EntityUid uid, MapGridComponent grid, ZLevelTileIndices indices, Tile tile)
    {
        var gridIndices = new Vector2i(indices.X, indices.Y);
        if (indices.Z == 0)
        {
            SetTile(uid, grid, gridIndices, tile);
            return;
        }

        var chunkIndex = GetChunkIndices(gridIndices, grid.ChunkSize);
        if (!grid.Chunks.TryGetValue(chunkIndex, out var chunk))
        {
            if (tile.IsEmpty)
                return;

            chunk = GetOrAddChunk(uid, grid, chunkIndex);
        }

        var chunkTile = chunk.GridTileToChunkTile(gridIndices);

        if (!chunk.TrySetTile((ushort) chunkTile.X, (ushort) chunkTile.Y, indices.Z, tile, out var oldTile, out var shapeChanged))
            return;

        chunk.LastTileModifiedTick = _timing.CurTick;
        grid.LastTileModifiedTick = _timing.CurTick;
        Dirty(uid, grid);

        var ev = new ZLevelTileChangedEvent((uid, grid), [new ZLevelTileChangedEntry(tile, oldTile, chunkIndex, indices)]);
        RaiseLocalEvent(uid, ref ev, true);

        if (shapeChanged)
        {
            RegenerateAabb(grid);
            OnGridBoundsChange(uid, grid);
        }

        if (chunk.IsCompletelyEmpty)
            RemoveChunk(uid, grid, chunk.Indices);
    }

    public int ClearZLevelTileRegion(EntityUid uid, MapGridComponent grid, Vector2i min, Vector2i max, int z)
    {
        var changed = 0;
        var (bottomLeft, topRight) = NormalizeRegion(min, max);

        for (var x = bottomLeft.X; x <= topRight.X; x++)
        {
            for (var y = bottomLeft.Y; y <= topRight.Y; y++)
            {
                var indices = new ZLevelTileIndices(x, y, z);
                if (GetZLevelTileRef(uid, grid, indices).Tile.IsEmpty)
                    continue;

                SetZLevelTile(uid, grid, indices, Tile.Empty);
                changed++;
            }
        }

        return changed;
    }

    public int CopyZLevelTileRegion(
        EntityUid uid,
        MapGridComponent grid,
        Vector2i min,
        Vector2i max,
        int sourceZ,
        int targetZ,
        bool includeEmpty = true)
    {
        if (sourceZ == targetZ)
            return 0;

        var changed = 0;
        var (bottomLeft, topRight) = NormalizeRegion(min, max);
        var tiles = new List<(ZLevelTileIndices Indices, Tile Tile)>();

        for (var x = bottomLeft.X; x <= topRight.X; x++)
        {
            for (var y = bottomLeft.Y; y <= topRight.Y; y++)
            {
                var source = new ZLevelTileIndices(x, y, sourceZ);
                var tile = GetZLevelTileRef(uid, grid, source).Tile;

                if (!includeEmpty && tile.IsEmpty)
                    continue;

                tiles.Add((new ZLevelTileIndices(x, y, targetZ), tile));
            }
        }

        foreach (var (indices, tile) in tiles)
        {
            if (GetZLevelTileRef(uid, grid, indices).Tile == tile)
                continue;

            SetZLevelTile(uid, grid, indices, tile);
            changed++;
        }

        return changed;
    }

    public ZLevelTileIndices ZLevelTileIndicesFor(EntityUid uid, MapGridComponent grid, ZLevelEntityCoordinates coords)
    {
        var xy = TileIndicesFor(uid, grid, coords.ToEntityCoordinates());
        return new ZLevelTileIndices(xy.X, xy.Y, coords.Z);
    }

    public ZLevelTileIndices ZLevelTileIndicesFor(EntityUid uid, MapGridComponent grid, ZLevelMapCoordinates coords)
    {
        var xy = TileIndicesFor(uid, grid, new MapCoordinates(coords.Position, coords.MapId));
        var localZ = _transform.WorldToLocalZLevel(uid, coords.Z);
        return new ZLevelTileIndices(xy.X, xy.Y, localZ);
    }

    public ZLevelEntityCoordinates ToZLevelCoordinates(EntityUid gridUid, ZLevelTileIndices tile, MapGridComponent? gridComponent = null)
    {
        if (!_gridQuery.Resolve(gridUid, ref gridComponent))
            return ZLevelEntityCoordinates.Invalid;

        return new ZLevelEntityCoordinates(gridUid, tile.X * gridComponent.TileSize, tile.Y * gridComponent.TileSize, tile.Z);
    }

    public ZLevelEntityCoordinates ToZLevelCenterCoordinates(EntityUid gridUid, ZLevelTileIndices tile, MapGridComponent? gridComponent = null)
    {
        if (!_gridQuery.Resolve(gridUid, ref gridComponent))
            return ZLevelEntityCoordinates.Invalid;

        var pos = new Vector2(tile.X * gridComponent.TileSize, tile.Y * gridComponent.TileSize) + gridComponent.TileSizeHalfVector;
        return new ZLevelEntityCoordinates(gridUid, pos, tile.Z);
    }

    public ZLevelMapCoordinates GridTileToZLevelMap(EntityUid uid, MapGridComponent grid, ZLevelTileIndices tile)
    {
        var world = GridTileToWorld(uid, grid, new Vector2i(tile.X, tile.Y));
        var mapId = _xformQuery.GetComponent(uid).MapID;
        return new ZLevelMapCoordinates(world.Position, _transform.LocalToWorldZLevel(uid, tile.Z), mapId);
    }

    public IEnumerable<ZLevelTileIndices> GetZLevelNeighbors(ZLevelTileIndices origin)
    {
        foreach (var offset in ZLevelNeighborOffsets)
        {
            yield return new ZLevelTileIndices(origin.X + offset.X, origin.Y + offset.Y, origin.Z + offset.Z);
        }
    }

    public bool TryGetZLevelTileAbove(EntityUid uid, MapGridComponent grid, ZLevelTileIndices origin, out ZLevelTileRef tile)
    {
        return TryGetZLevelTileRef(uid, grid, new ZLevelTileIndices(origin.X, origin.Y, origin.Z + 1), out tile);
    }

    public bool TryGetZLevelTileBelow(EntityUid uid, MapGridComponent grid, ZLevelTileIndices origin, out ZLevelTileRef tile)
    {
        return TryGetZLevelTileRef(uid, grid, new ZLevelTileIndices(origin.X, origin.Y, origin.Z - 1), out tile);
    }

    public bool IsZLevelVerticalPassageBlocked(EntityUid uid, MapGridComponent grid, Vector2i xy, int z)
    {
        var above = GetZLevelTileRef(uid, grid, new ZLevelTileIndices(xy.X, xy.Y, z + 1));

        // Phase 2 ceiling rule:
        // a non-empty tile on the layer above seals the boundary beneath it.
        return !above.Tile.IsEmpty;
    }

    public bool IsZLevelAdjacencyOpen(EntityUid uid, MapGridComponent grid, ZLevelTileIndices source, ZLevelTileIndices target)
    {
        var deltaX = target.X - source.X;
        var deltaY = target.Y - source.Y;
        var deltaZ = target.Z - source.Z;

        if (Math.Abs(deltaX) + Math.Abs(deltaY) + Math.Abs(deltaZ) != 1)
            return false;

        if (deltaZ != 0)
        {
            var lower = Math.Min(source.Z, target.Z);
            return !IsZLevelVerticalPassageBlocked(uid, grid, new Vector2i(source.X, source.Y), lower);
        }

        return true;
    }

    public bool TryGetZLevelAdjacency(
        EntityUid uid,
        MapGridComponent grid,
        ZLevelTileIndices source,
        ZLevelAdjacencyDirection direction,
        out ZLevelAdjacency adjacency)
    {
        var target = direction switch
        {
            ZLevelAdjacencyDirection.North => new ZLevelTileIndices(source.X, source.Y + 1, source.Z),
            ZLevelAdjacencyDirection.South => new ZLevelTileIndices(source.X, source.Y - 1, source.Z),
            ZLevelAdjacencyDirection.East => new ZLevelTileIndices(source.X + 1, source.Y, source.Z),
            ZLevelAdjacencyDirection.West => new ZLevelTileIndices(source.X - 1, source.Y, source.Z),
            ZLevelAdjacencyDirection.Up => new ZLevelTileIndices(source.X, source.Y, source.Z + 1),
            ZLevelAdjacencyDirection.Down => new ZLevelTileIndices(source.X, source.Y, source.Z - 1),
            _ => source
        };

        adjacency = new ZLevelAdjacency(source, target, direction, IsZLevelAdjacencyOpen(uid, grid, source, target));
        return true;
    }

    public IEnumerable<ZLevelAdjacency> GetZLevelAdjacencies(
        EntityUid uid,
        MapGridComponent grid,
        ZLevelTileIndices origin,
        bool includeBlocked = false)
    {
        foreach (var direction in Enum.GetValues<ZLevelAdjacencyDirection>())
        {
            if (!TryGetZLevelAdjacency(uid, grid, origin, direction, out var adjacency))
                continue;

            if (!includeBlocked && !adjacency.IsOpen)
                continue;

            yield return adjacency;
        }
    }

    public bool TryGetZLevelTile(EntityUid uid, MapGridComponent grid, ZLevelTileIndices indices, out Tile tile)
    {
        if (!TryGetZLevelTileRef(uid, grid, indices, out var tileRef))
        {
            tile = Tile.Empty;
            return false;
        }

        tile = tileRef.Tile;
        return true;
    }

    public bool IsZLevelTileEmpty(EntityUid uid, MapGridComponent grid, ZLevelTileIndices indices)
    {
        return GetZLevelTileRef(uid, grid, indices).Tile.IsEmpty;
    }

    public IEnumerable<int> GetExistingZLevelLayersAt(EntityUid uid, MapGridComponent grid, Vector2i xy, int minZ, int maxZ)
    {
        if (minZ > maxZ)
            return Array.Empty<int>();

        if (!TryGetChunkAndOffset(uid, grid, xy, out var chunk, out var chunkTileIndices))
            return Array.Empty<int>();

        return chunk.GetExistingLayersAt((ushort) chunkTileIndices.X, (ushort) chunkTileIndices.Y, minZ, maxZ)
            .OrderBy(z => z)
            .ToArray();
    }

    public bool TryFindNearestZLevelSolidBelow(EntityUid uid, MapGridComponent grid, Vector2i xy, int startZ, int maxDepth, out ZLevelTileRef tile)
    {
        if (maxDepth < 0)
        {
            tile = ZLevelTileRef.Zero;
            return false;
        }

        foreach (var z in GetExistingZLevelLayersAt(uid, grid, xy, startZ - maxDepth, startZ).Reverse())
        {
            var candidate = GetZLevelTileRef(uid, grid, new ZLevelTileIndices(xy.X, xy.Y, z));
            if (candidate.Tile.IsEmpty)
                continue;

            tile = candidate;
            return true;
        }

        tile = ZLevelTileRef.Zero;
        return false;
    }

    public bool TryFindNearestZLevelSolidAbove(EntityUid uid, MapGridComponent grid, Vector2i xy, int startZ, int maxDepth, out ZLevelTileRef tile)
    {
        if (maxDepth < 0)
        {
            tile = ZLevelTileRef.Zero;
            return false;
        }

        foreach (var z in GetExistingZLevelLayersAt(uid, grid, xy, startZ, startZ + maxDepth))
        {
            var candidate = GetZLevelTileRef(uid, grid, new ZLevelTileIndices(xy.X, xy.Y, z));
            if (candidate.Tile.IsEmpty)
                continue;

            tile = candidate;
            return true;
        }

        tile = ZLevelTileRef.Zero;
        return false;
    }

    public bool CanTraverseZLevelBoundary(EntityUid uid, MapGridComponent grid, Vector2i xy, int fromZ, int toZ)
    {
        if (Math.Abs(fromZ - toZ) != 1)
            return false;

        return IsZLevelAdjacencyOpen(
            uid,
            grid,
            new ZLevelTileIndices(xy.X, xy.Y, fromZ),
            new ZLevelTileIndices(xy.X, xy.Y, toZ));
    }

    /// <summary>
    /// Returns whether the vertical stack between two z-levels is open at the given tile.
    /// This is used by client visibility/targeting to decide whether lower-floor content should be visible.
    /// </summary>
    public bool IsZLevelStackOpen(EntityUid uid, MapGridComponent grid, Vector2i xy, int fromZ, int toZ)
    {
        if (fromZ == toZ)
            return true;

        var step = Math.Sign(toZ - fromZ);
        for (var z = fromZ; z != toZ; z += step)
        {
            if (!CanTraverseZLevelBoundary(uid, grid, xy, z, z + step))
                return false;
        }

        return true;
    }

    public bool IsZLevelStackOpen(ZLevelMapCoordinates coordinates, int fromZ)
    {
        if (fromZ == coordinates.Z)
            return true;

        if (!_mapInternal.TryFindGridAt(new MapCoordinates(coordinates.Position, coordinates.MapId), out var gridUid, out var grid))
            return false;

        var xy = TileIndicesFor(gridUid, grid, new MapCoordinates(coordinates.Position, coordinates.MapId));
        return IsZLevelStackOpen(
            gridUid,
            grid,
            xy,
            _transform.WorldToLocalZLevel(gridUid, fromZ),
            _transform.WorldToLocalZLevel(gridUid, coordinates.Z));
    }

    public bool TryGetZLevelSupportTile(EntityUid uid, MapGridComponent grid, Vector2i xy, int startZ, int maxDropDepth, out ZLevelTileRef tile)
    {
        tile = ZLevelTileRef.Zero;

        if (!TryFindNearestZLevelSolidBelow(uid, grid, xy, startZ, maxDropDepth, out var support))
            return false;

        for (var z = startZ; z > support.GridIndices.Z; z--)
        {
            if (!CanTraverseZLevelBoundary(uid, grid, xy, z, z - 1))
                return false;
        }

        tile = support;
        return true;
    }

    public bool TryGetZLevelSupportTile(ZLevelMapCoordinates coordinates, int maxDropDepth, out ZLevelTileRef tile)
    {
        tile = ZLevelTileRef.Zero;
        if (!_mapInternal.TryFindGridAt(new MapCoordinates(coordinates.Position, coordinates.MapId), out var gridUid, out var grid))
            return false;

        var xy = TileIndicesFor(gridUid, grid, new MapCoordinates(coordinates.Position, coordinates.MapId));
        var localZ = _transform.WorldToLocalZLevel(gridUid, coordinates.Z);
        return TryGetZLevelSupportTile(gridUid, grid, xy, localZ, maxDropDepth, out tile);
    }

    public IEnumerable<int> GetExistingZLevelLayers(EntityUid uid, MapGridComponent grid)
    {
        var layers = new SortedSet<int>();
        foreach (var chunk in GetMapChunks(uid, grid).Values)
        {
            foreach (var layer in chunk.GetExistingLayers())
            {
                layers.Add(layer);
            }
        }

        return layers;
    }

    /// <summary>
    /// Enumerates every non-empty tile in the grid's allocated Z-level layers.
    /// Work is bounded by existing chunks and layers rather than the grid's full 3D bounds.
    /// </summary>
    public IEnumerable<ZLevelTileRef> GetAllNonEmptyZLevelTiles(EntityUid uid, MapGridComponent grid)
    {
        foreach (var chunk in GetMapChunks(uid, grid).Values)
        {
            foreach (var z in chunk.GetExistingLayers())
            {
                for (ushort x = 0; x < chunk.ChunkSize; x++)
                {
                    for (ushort y = 0; y < chunk.ChunkSize; y++)
                    {
                        var tile = chunk.GetTile(x, y, z);
                        if (tile.IsEmpty)
                            continue;

                        var gridIndices = chunk.ChunkTileToGridTile(new Vector2i(x, y));
                        yield return new ZLevelTileRef(
                            uid,
                            new ZLevelTileIndices(gridIndices.X, gridIndices.Y, z),
                            tile);
                    }
                }
            }
        }
    }

    private bool TryGetChunkAndOffset(
        EntityUid uid,
        MapGridComponent grid,
        Vector2i indices,
        [NotNullWhen(true)] out MapChunk? chunk,
        out Vector2i offset)
    {
        var chunkIndices = GetChunkIndices(indices, grid.ChunkSize);
        offset = GetChunkRelative(indices, grid.ChunkSize);
        return TryGetChunk(uid, grid, chunkIndices, out chunk);
    }

    private static (Vector2i BottomLeft, Vector2i TopRight) NormalizeRegion(Vector2i a, Vector2i b)
    {
        return (
            new Vector2i(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)),
            new Vector2i(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)));
    }
}
