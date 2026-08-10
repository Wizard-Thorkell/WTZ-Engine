// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.Shared.ViewVariables;

namespace Robust.Shared.Map
{
    /// <summary>
    ///     A square section of a <see cref="MapGridComponent"/>.
    /// </summary>
    internal sealed class MapChunk
    {
        /// <summary>
        /// New SnapGrid cells are allocated with this capacity.
        /// </summary>
        private const int SnapCellStartingCapacity = 1;

        private readonly Vector2i _gridIndices;

        [ViewVariables] internal readonly Tile[,] Tiles;
        private Dictionary<int, Tile[,]>? _layerTiles;
        private Dictionary<int, int>? _layerFilledTiles;
        private readonly SnapGridCell[,] _snapGrid;

        /// <summary>
        /// Keeps a running count of the number of filled tiles in this chunk.
        /// </summary>
        /// <remarks>
        /// This will always be between 0 and <see cref="ChunkSize"/>^2.
        /// </remarks>
        [ViewVariables]
        internal int FilledTiles { get; set; }

        public bool IsCompletelyEmpty => FilledTiles == 0 && (_layerFilledTiles == null || _layerFilledTiles.Count == 0);

        /// <summary>
        /// Chunk-local AABB of this chunk.
        /// </summary>
        [ViewVariables]
        public Box2i CachedBounds { get; set; }

        [ViewVariables]
        internal HashSet<string> Fixtures = new();

        /// <summary>
        /// The last game simulation tick that a tile on this chunk was modified.
        /// </summary>
        [ViewVariables]
        public GameTick LastTileModifiedTick { get; set; }

        /// <summary>
        /// Setting this property to <see langword="true"/> suppresses collision regeneration on the chunk until the
        /// property is set to <see langword="false"/>.
        /// </summary>
        public bool SuppressCollisionRegeneration { get; set; }

        /// <summary>
        ///     Constructs an instance of a MapChunk.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="chunkSize"></param>
        public MapChunk(int x, int y, ushort chunkSize)
        {
            _gridIndices = new Vector2i(x, y);
            ChunkSize = chunkSize;

            Tiles = new Tile[ChunkSize, ChunkSize];
            _snapGrid = new SnapGridCell[ChunkSize, ChunkSize];
        }

        /// <summary>
        ///     The number of tiles per side of the square chunk.
        /// </summary>
        public ushort ChunkSize { get; }

        /// <summary>
        ///     The X index of this chunk inside the <see cref="MapGridComponent"/>.
        /// </summary>
        public int X => _gridIndices.X;

        /// <summary>
        ///     The Y index of this chunk inside the <see cref="MapGridComponent"/>.
        /// </summary>
        public int Y => _gridIndices.Y;

        /// <summary>
        ///     The positional indices of this chunk in the <see cref="MapGridComponent"/>.
        /// </summary>
        public Vector2i Indices => _gridIndices;

        /// <summary>
        /// Returns the tile at the given chunk indices.
        /// </summary>
        /// <param name="xIndex">The X tile index relative to the chunk origin.</param>
        /// <param name="yIndex">The Y tile index relative to the chunk origin.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException">The index is less than or greater than the size of the chunk.</exception>
        public Tile GetTile(ushort xIndex, ushort yIndex)
        {

            if (xIndex >= ChunkSize)
                throw new ArgumentOutOfRangeException(nameof(xIndex), "Tile indices out of bounds.");

            if (yIndex >= ChunkSize)
                throw new ArgumentOutOfRangeException(nameof(yIndex), "Tile indices out of bounds.");

            return Tiles[xIndex, yIndex];
        }

        public Tile GetTile(Vector2i indices)
        {
            return Tiles[indices.X, indices.Y];
        }

        public Tile GetTile(ushort xIndex, ushort yIndex, int z)
        {
            if (z == 0)
                return GetTile(xIndex, yIndex);

            if (_layerTiles == null || !_layerTiles.TryGetValue(z, out var tiles))
                return Tile.Empty;

            if (xIndex >= ChunkSize)
                throw new ArgumentOutOfRangeException(nameof(xIndex), "Tile indices out of bounds.");

            if (yIndex >= ChunkSize)
                throw new ArgumentOutOfRangeException(nameof(yIndex), "Tile indices out of bounds.");

            return tiles[xIndex, yIndex];
        }

        public Tile GetTile(Vector2i indices, int z)
        {
            return GetTile((ushort) indices.X, (ushort) indices.Y, z);
        }

        /// <summary>
        ///     Transforms Tile indices relative to the grid into tile indices relative to this chunk.
        /// </summary>
        /// <param name="gridTile">Tile indices relative to the grid.</param>
        /// <returns>Tile indices relative to this chunk.</returns>
        public Vector2i GridTileToChunkTile(Vector2i gridTile)
        {
            var x = MathHelper.Mod(gridTile.X, ChunkSize);
            var y = MathHelper.Mod(gridTile.Y, ChunkSize);
            return new Vector2i(x, y);
        }

        /// <summary>
        ///     Translates chunk tile indices to grid tile indices.
        /// </summary>
        /// <param name="chunkTile">The indices relative to the chunk origin.</param>
        /// <returns>The indices relative to the grid origin.</returns>
        public Vector2i ChunkTileToGridTile(Vector2i chunkTile)
        {
            return chunkTile + _gridIndices * ChunkSize;
        }

        /// <summary>
        /// Returns the anchored cell at the given tile indices.
        /// </summary>
        public IEnumerable<EntityUid> GetSnapGridCell(ushort xCell, ushort yCell)
        {
            if (xCell >= ChunkSize)
                throw new ArgumentOutOfRangeException(nameof(xCell), "Tile indices out of bounds.");

            if (yCell >= ChunkSize)
                throw new ArgumentOutOfRangeException(nameof(yCell), "Tile indices out of bounds.");

            var cell = _snapGrid[xCell, yCell];
            var list = cell.Center;

            if (list == null)
            {
                return Array.Empty<EntityUid>();
            }

            return list;
        }

        internal List<EntityUid>? GetSnapGrid(ushort xCell, ushort yCell)
        {
            if (xCell >= ChunkSize)
                throw new ArgumentOutOfRangeException(nameof(xCell), "Tile indices out of bounds.");

            if (yCell >= ChunkSize)
                throw new ArgumentOutOfRangeException(nameof(yCell), "Tile indices out of bounds.");

            var cell = _snapGrid[xCell, yCell];
            return cell.Center;
        }

        /// <summary>
        /// Adds an entity to the anchor cell at the given tile indices.
        /// </summary>
        public void AddToSnapGridCell(ushort xCell, ushort yCell, EntityUid euid)
        {
            if (xCell >= ChunkSize)
                throw new ArgumentOutOfRangeException(nameof(xCell), "Tile indices out of bounds.");

            if (yCell >= ChunkSize)
                throw new ArgumentOutOfRangeException(nameof(yCell), "Tile indices out of bounds.");

            ref var cell = ref _snapGrid[xCell, yCell];
            cell.Center ??= new List<EntityUid>(SnapCellStartingCapacity);

            DebugTools.Assert(!cell.Center.Contains(euid));
            cell.Center.Add(euid);
        }

        /// <summary>
        /// Removes an entity from the anchor cell at the given tile indices.
        /// </summary>
        public void RemoveFromSnapGridCell(ushort xCell, ushort yCell, EntityUid euid)
        {
            if (xCell >= ChunkSize)
                throw new ArgumentOutOfRangeException(nameof(xCell), "Tile indices out of bounds.");

            if (yCell >= ChunkSize)
                throw new ArgumentOutOfRangeException(nameof(yCell), "Tile indices out of bounds.");

            ref var cell = ref _snapGrid[xCell, yCell];
            cell.Center?.Remove(euid);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"Chunk {_gridIndices}";
        }

        private struct SnapGridCell
        {
            public List<EntityUid>? Center;
        }

        /// <summary>
        /// Sets the tile without any callbacks.
        /// Do not call this unless you know what you are doing.
        /// </summary>
        internal bool TrySetTile(ushort xIndex, ushort yIndex, Tile tile, out Tile oldTile, out bool shapeChanged)
        {
            if (xIndex >= Tiles.Length)
                throw new ArgumentOutOfRangeException(nameof(xIndex), "Tile indices out of bounds.");

            if (yIndex >= Tiles.Length)
                throw new ArgumentOutOfRangeException(nameof(yIndex), "Tile indices out of bounds.");

            shapeChanged = false;

            ref var tileRef = ref Tiles[xIndex, yIndex];
            if (tileRef == tile)
            {
                oldTile = default;
                return false;
            }

            if (tileRef.IsEmpty)
            {
                if (!tile.IsEmpty)
                {
                    FilledTiles += 1;
                    shapeChanged = true;
                }
            }
            else if (tile.IsEmpty)
            {
                FilledTiles -= 1;
                shapeChanged = true;
            }

            DebugTools.Assert(FilledTiles >= 0);

            oldTile = tileRef;
            tileRef = tile;
            ValidateChunk();
            return true;
        }

        internal bool TrySetTile(ushort xIndex, ushort yIndex, int z, Tile tile, out Tile oldTile, out bool shapeChanged)
        {
            if (z == 0)
                return TrySetTile(xIndex, yIndex, tile, out oldTile, out shapeChanged);

            if (xIndex >= Tiles.Length)
                throw new ArgumentOutOfRangeException(nameof(xIndex), "Tile indices out of bounds.");

            if (yIndex >= Tiles.Length)
                throw new ArgumentOutOfRangeException(nameof(yIndex), "Tile indices out of bounds.");

            shapeChanged = false;

            Tile[,]? tiles = null;
            if ((_layerTiles == null || !_layerTiles.TryGetValue(z, out tiles)) && tile.IsEmpty)
            {
                oldTile = Tile.Empty;
                return false;
            }

            tiles ??= GetOrAddLayer(z);
            ref var tileRef = ref tiles[xIndex, yIndex];
            if (tileRef == tile)
            {
                oldTile = default;
                return false;
            }

            _layerFilledTiles ??= new Dictionary<int, int>();
            _layerFilledTiles.TryGetValue(z, out var filledTiles);

            if (tileRef.IsEmpty)
            {
                if (!tile.IsEmpty)
                    filledTiles += 1;
            }
            else if (tile.IsEmpty)
            {
                filledTiles -= 1;
            }

            oldTile = tileRef;
            tileRef = tile;

            if (filledTiles <= 0)
            {
                _layerFilledTiles.Remove(z);
                _layerTiles?.Remove(z);

                if (_layerTiles is { Count: 0 })
                    _layerTiles = null;

                if (_layerFilledTiles.Count == 0)
                    _layerFilledTiles = null;
            }
            else
            {
                _layerFilledTiles[z] = filledTiles;
            }

            ValidateZLevelLayers();
            return true;
        }

        public IEnumerable<int> GetExistingLayers()
        {
            if (FilledTiles > 0)
                yield return 0;

            if (_layerFilledTiles == null)
                yield break;

            foreach (var layer in _layerFilledTiles.Keys)
            {
                yield return layer;
            }
        }

        public Dictionary<int, Tile[]>? CopyNonZeroLayerData()
        {
            if (_layerTiles == null || _layerTiles.Count == 0)
                return null;

            var data = new Dictionary<int, Tile[]>(_layerTiles.Count);
            foreach (var (z, tiles) in _layerTiles)
            {
                var buffer = new Tile[ChunkSize * ChunkSize];
                for (var x = 0; x < ChunkSize; x++)
                {
                    for (var y = 0; y < ChunkSize; y++)
                    {
                        buffer[x * ChunkSize + y] = tiles[x, y];
                    }
                }

                data[z] = buffer;
            }

            return data;
        }

        public IEnumerable<int> GetExistingLayersAt(ushort xIndex, ushort yIndex, int minZ, int maxZ)
        {
            if (xIndex >= ChunkSize)
                throw new ArgumentOutOfRangeException(nameof(xIndex), "Tile indices out of bounds.");

            if (yIndex >= ChunkSize)
                throw new ArgumentOutOfRangeException(nameof(yIndex), "Tile indices out of bounds.");

            if (minZ > maxZ)
                yield break;

            if (minZ <= 0 && maxZ >= 0 && !Tiles[xIndex, yIndex].IsEmpty)
                yield return 0;

            if (_layerTiles == null)
                yield break;

            foreach (var (z, tiles) in _layerTiles)
            {
                if (z < minZ || z > maxZ || tiles[xIndex, yIndex].IsEmpty)
                    continue;

                yield return z;
            }
        }

        private Tile[,] GetOrAddLayer(int z)
        {
            _layerTiles ??= new Dictionary<int, Tile[,]>();
            if (_layerTiles.TryGetValue(z, out var tiles))
                return tiles;

            tiles = new Tile[ChunkSize, ChunkSize];
            _layerTiles[z] = tiles;
            return tiles;
        }

        [Conditional("DEBUG")]
        public void ValidateChunk()
        {
            var totalFilled = 0;
            foreach (var t in Tiles)
            {
                if (!t.IsEmpty)
                    totalFilled += 1;
            }
            DebugTools.Assert(totalFilled == FilledTiles);
        }

        [Conditional("DEBUG")]
        public void ValidateZLevelLayers()
        {
            if (_layerTiles == null)
            {
                DebugTools.Assert(_layerFilledTiles == null || _layerFilledTiles.Count == 0);
                return;
            }

            DebugTools.Assert(_layerFilledTiles != null);
            DebugTools.Assert(_layerTiles.Count == _layerFilledTiles.Count);

            foreach (var (z, tiles) in _layerTiles)
            {
                DebugTools.Assert(z != 0);
                var totalFilled = 0;
                foreach (var tile in tiles)
                {
                    if (!tile.IsEmpty)
                        totalFilled += 1;
                }

                DebugTools.Assert(_layerFilledTiles.TryGetValue(z, out var filledTiles));
                DebugTools.Assert(filledTiles > 0);
                DebugTools.Assert(totalFilled == filledTiles);
            }
        }
    }

    /// <summary>
    /// Event delegate for <see cref="MapChunk.TileModified"/>.
    /// </summary>
    /// <param name="mapChunk">Chunk that the tile was on.</param>
    /// <param name="tileIndices">hunk Indices of the tile that was modified.</param>
    /// <param name="newTile">New version of the tile.</param>
    /// <param name="oldTile">Old version of the tile.</param>
    /// <param name="chunkShapeChanged">If changing this tile changed the shape of the chunk.</param>
    internal delegate void TileModifiedDelegate(EntityUid uid, MapGridComponent grid, MapChunk mapChunk, Vector2i tileIndices, Tile newTile, Tile oldTile, bool chunkShapeChanged);
}
