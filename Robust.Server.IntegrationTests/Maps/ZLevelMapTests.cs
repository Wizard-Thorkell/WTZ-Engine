// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Numerics;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Timing;
using Robust.UnitTesting;

namespace Robust.Server.IntegrationTests.Maps;

[TestFixture]
internal sealed class ZLevelMapTests : RobustUnitTest
{
    public override UnitTestProject Project => UnitTestProject.Server;

    private IEntityManager _entMan = default!;
    private IMapManager _mapMan = default!;
    private SharedMapSystem _mapSys = default!;
    private SharedTransformSystem _xform = default!;
    private IGameTiming _timing = default!;

    private MapId _mapId;
    private Entity<MapGridComponent> _grid;

    [OneTimeSetUp]
    public void Setup()
    {
        IoCManager.Resolve<IComponentFactory>().GenerateNetIds();
        IoCManager.Resolve<ISerializationManager>().Initialize();

        _entMan = IoCManager.Resolve<IEntityManager>();
        _mapMan = IoCManager.Resolve<IMapManager>();
        _mapSys = _entMan.System<SharedMapSystem>();
        _xform = _entMan.System<SharedTransformSystem>();
        _timing = IoCManager.Resolve<IGameTiming>();

        var netManager = IoCManager.Resolve<INetManager>();
        if (!netManager.IsServer)
            netManager.Initialize(true);

        _mapSys.CreateMap(out _mapId);
        _grid = _mapMan.CreateGridEntity(_mapId);
    }

    [Test]
    public void EmptyZLevelWriteDoesNotAllocateChunk()
    {
        var tile = new ZLevelTileIndices(2048, 2048, 4);
        var chunkIndex = SharedMapSystem.GetChunkIndices(new Vector2i(tile.X, tile.Y), _grid.Comp.ChunkSize);
        var chunkCount = _grid.Comp.ChunkCount;

        Assert.That(_grid.Comp.HasChunk(chunkIndex), NUnit.Framework.Is.False);

        _mapSys.SetZLevelTile(_grid.Owner, _grid.Comp, tile, Tile.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(_grid.Comp.HasChunk(chunkIndex), NUnit.Framework.Is.False);
            Assert.That(_grid.Comp.ChunkCount, NUnit.Framework.Is.EqualTo(chunkCount));
        });
    }

    [Test]
    public void ZLevelOnlyChunkStateRoundTripsAndReplicatesDeletion()
    {
        var source = _mapMan.CreateGridEntity(_mapId);
        var target = _mapMan.CreateGridEntity(_mapId);
        var keepAlive = new Vector2i(-2048, -2048);
        var targetOnly = new Vector2i(8192, 8192);
        var upper = new ZLevelTileIndices(4096, 4096, 7);
        var chunkIndex = SharedMapSystem.GetChunkIndices(new Vector2i(upper.X, upper.Y), source.Comp.ChunkSize);
        var targetOnlyChunk = SharedMapSystem.GetChunkIndices(targetOnly, target.Comp.ChunkSize);

        _mapSys.SetTile(source.Owner, source.Comp, keepAlive, new Tile(1));
        _mapSys.SetTile(target.Owner, target.Comp, keepAlive, new Tile(1));
        _mapSys.SetTile(target.Owner, target.Comp, targetOnly, new Tile(3));
        _mapSys.SetZLevelTile(source.Owner, source.Comp, upper, new Tile(2));

        var getFullState = new ComponentGetState(null, GameTick.Zero);
        _entMan.EventBus.RaiseComponentEvent(source.Owner, source.Comp, ref getFullState);
        var fullState = (MapGridComponentState) getFullState.State!;
        var zChunk = fullState.FullGridData[chunkIndex];

        Assert.Multiple(() =>
        {
            Assert.That(zChunk.TileData, NUnit.Framework.Is.All.Matches<Tile>(tile => tile.IsEmpty));
            Assert.That(zChunk.ZLevelTileData, Contains.Key(upper.Z));
            Assert.That(zChunk.ZLevelTileData![upper.Z], Has.Some.Matches<Tile>(tile => !tile.IsEmpty));
        });

        var handleFullState = new ComponentHandleState(fullState, null);
        _entMan.EventBus.RaiseComponentEvent(target.Owner, target.Comp, ref handleFullState);

        Assert.Multiple(() =>
        {
            Assert.That(target.Comp.HasChunk(chunkIndex), NUnit.Framework.Is.True);
            Assert.That(target.Comp.HasChunk(targetOnlyChunk), NUnit.Framework.Is.False);
            Assert.That(_mapSys.GetZLevelTileRef(target.Owner, target.Comp, upper).Tile.TypeId, NUnit.Framework.Is.EqualTo((ushort) 2));
        });

        var originalTick = _timing.CurTick;
        try
        {
            _timing.CurTick += 1;
            var fromTick = _timing.CurTick;
            _mapSys.SetZLevelTile(source.Owner, source.Comp, upper, Tile.Empty);

            var getDeltaState = new ComponentGetState(null, fromTick);
            _entMan.EventBus.RaiseComponentEvent(source.Owner, source.Comp, ref getDeltaState);
            var deltaState = (MapGridComponentDeltaState) getDeltaState.State!;

            Assert.Multiple(() =>
            {
                Assert.That(source.Comp.HasChunk(chunkIndex), NUnit.Framework.Is.False);
                Assert.That(deltaState.ChunkData, Contains.Key(chunkIndex));
                Assert.That(deltaState.ChunkData![chunkIndex].IsDeleted(), NUnit.Framework.Is.True);
            });

            var handleDeltaState = new ComponentHandleState(deltaState, null);
            _entMan.EventBus.RaiseComponentEvent(target.Owner, target.Comp, ref handleDeltaState);

            Assert.Multiple(() =>
            {
                Assert.That(target.Comp.HasChunk(chunkIndex), NUnit.Framework.Is.False);
                Assert.That(_mapSys.GetZLevelTileRef(target.Owner, target.Comp, upper).Tile.IsEmpty, NUnit.Framework.Is.True);
            });
        }
        finally
        {
            _timing.CurTick = originalTick;
        }
    }

    [Test]
    public void RepeatedZLevelChunkDeletionProducesSingleDeltaEntry()
    {
        var keepAlive = new Vector2i(-4096, -4096);
        var zTile = new ZLevelTileIndices(12288, 12288, 5);
        var chunkIndex = SharedMapSystem.GetChunkIndices(new Vector2i(zTile.X, zTile.Y), _grid.Comp.ChunkSize);
        var originalTick = _timing.CurTick;

        _mapSys.SetTile(_grid.Owner, _grid.Comp, keepAlive, new Tile(1));
        _mapSys.SetZLevelTile(_grid.Owner, _grid.Comp, zTile, new Tile(2));

        try
        {
            _timing.CurTick += 1;
            var fromTick = _timing.CurTick;
            _mapSys.SetZLevelTile(_grid.Owner, _grid.Comp, zTile, Tile.Empty);

            _timing.CurTick += 1;
            _mapSys.SetZLevelTile(_grid.Owner, _grid.Comp, zTile, new Tile(2));

            _timing.CurTick += 1;
            _mapSys.SetZLevelTile(_grid.Owner, _grid.Comp, zTile, Tile.Empty);

            var getDeltaState = new ComponentGetState(null, fromTick);
            _entMan.EventBus.RaiseComponentEvent(_grid.Owner, _grid.Comp, ref getDeltaState);
            var deltaState = (MapGridComponentDeltaState) getDeltaState.State!;
            var chunkData = deltaState.ChunkData!;

            Assert.Multiple(() =>
            {
                Assert.That(chunkData, Contains.Key(chunkIndex));
                Assert.That(chunkData.Count(pair => pair.Key == chunkIndex), NUnit.Framework.Is.EqualTo(1));
                Assert.That(chunkData[chunkIndex].IsDeleted(), NUnit.Framework.Is.True);
            });
        }
        finally
        {
            _timing.CurTick = originalTick;
        }
    }

    [Test]
    public void ZLevelTileSetGetSupportsMultipleLayers()
    {
        var upper = new ZLevelTileIndices(1, 2, 1);
        var lower = new ZLevelTileIndices(1, 2, -2);

        _mapSys.SetZLevelTile(_grid.Owner, _grid.Comp, upper, new Tile(1));
        _mapSys.SetZLevelTile(_grid.Owner, _grid.Comp, lower, new Tile(2));

        Assert.Multiple(() =>
        {
            Assert.That(_mapSys.GetZLevelTileRef(_grid, upper).Tile.TypeId, NUnit.Framework.Is.EqualTo((ushort) 1));
            Assert.That(_mapSys.GetZLevelTileRef(_grid, lower).Tile.TypeId, NUnit.Framework.Is.EqualTo((ushort) 2));
            Assert.That(_mapSys.GetZLevelTileRef(_grid, new ZLevelTileIndices(1, 2, 0)).Tile.IsEmpty, NUnit.Framework.Is.True);
        });
    }

    [Test]
    public void ZLevelLegacy2DQueriesRemainOnBaseLayer()
    {
        var baseTile = new Vector2i(4, 4);
        var upper = new ZLevelTileIndices(baseTile.X, baseTile.Y, 3);

        _mapSys.SetTile(_grid.Owner, _grid.Comp, baseTile, new Tile(5));
        _mapSys.SetZLevelTile(_grid.Owner, _grid.Comp, upper, new Tile(9));

        Assert.Multiple(() =>
        {
            Assert.That(_mapSys.GetTileRef(_grid.Owner, _grid.Comp, baseTile).Tile.TypeId, NUnit.Framework.Is.EqualTo((ushort) 5));
            Assert.That(_mapSys.GetZLevelTileRef(_grid.Owner, _grid.Comp, upper).Tile.TypeId, NUnit.Framework.Is.EqualTo((ushort) 9));
            Assert.That(_mapSys.GetZLevelTileRef(_grid.Owner, _grid.Comp, new ZLevelTileIndices(baseTile.X, baseTile.Y, 0)).Tile.TypeId, NUnit.Framework.Is.EqualTo((ushort) 5));
        });
    }

    [Test]
    public void ZLevelCoordinateConversionsRoundTrip()
    {
        var indices = new ZLevelTileIndices(2, 3, 4);
        var center = _mapSys.ToZLevelCenterCoordinates(_grid.Owner, indices, _grid.Comp);
        var mapCoords = _xform.ToZLevelMapCoordinates(center);
        var converted = _mapSys.ZLevelTileIndicesFor(_grid.Owner, _grid.Comp, mapCoords);

        Assert.Multiple(() =>
        {
            Assert.That(converted, NUnit.Framework.Is.EqualTo(indices));
            Assert.That(mapCoords.Z, NUnit.Framework.Is.EqualTo(indices.Z));
        });
    }

    [Test]
    public void ZLevelNeighborsAndVerticalPassageFollowLayerRules()
    {
        var origin = new ZLevelTileIndices(8, 9, 2);
        var neighbors = _mapSys.GetZLevelNeighbors(origin).ToArray();

        _mapSys.SetZLevelTile(_grid.Owner, _grid.Comp, origin, new Tile(1));
        _mapSys.SetZLevelTile(_grid.Owner, _grid.Comp, new ZLevelTileIndices(origin.X, origin.Y, origin.Z + 1), new Tile(1));

        Assert.Multiple(() =>
        {
            Assert.That(neighbors.Length, NUnit.Framework.Is.EqualTo(6));
            Assert.That(neighbors, Does.Contain(new ZLevelTileIndices(9, 9, 2)));
            Assert.That(neighbors, Does.Contain(new ZLevelTileIndices(8, 9, 3)));
            Assert.That(_mapSys.IsZLevelVerticalPassageBlocked(_grid.Owner, _grid.Comp, new Vector2i(origin.X, origin.Y), origin.Z), NUnit.Framework.Is.True);
            Assert.That(_mapSys.CanTraverseZLevelBoundary(_grid.Owner, _grid.Comp, new Vector2i(origin.X, origin.Y), origin.Z, origin.Z + 1), NUnit.Framework.Is.False);
        });

        _mapSys.SetZLevelTile(_grid.Owner, _grid.Comp, new ZLevelTileIndices(origin.X, origin.Y, origin.Z + 1), Tile.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(_mapSys.IsZLevelVerticalPassageBlocked(_grid.Owner, _grid.Comp, new Vector2i(origin.X, origin.Y), origin.Z), NUnit.Framework.Is.False);
            Assert.That(_mapSys.CanTraverseZLevelBoundary(_grid.Owner, _grid.Comp, new Vector2i(origin.X, origin.Y), origin.Z, origin.Z + 1), NUnit.Framework.Is.True);
        });
    }

    [Test]
    public void ZLevelAdjacencyApiEnumeratesHorizontalAndVerticalNeighbors()
    {
        var origin = new ZLevelTileIndices(3, 7, 1);
        var above = new ZLevelTileIndices(origin.X, origin.Y, origin.Z + 1);
        var east = new ZLevelTileIndices(origin.X + 1, origin.Y, origin.Z);
        var below = new ZLevelTileIndices(origin.X, origin.Y, origin.Z - 1);

        _mapSys.SetZLevelTile(_grid.Owner, _grid.Comp, origin, new Tile(1));
        _mapSys.SetZLevelTile(_grid.Owner, _grid.Comp, above, Tile.Empty);
        _mapSys.SetZLevelTile(_grid.Owner, _grid.Comp, east, new Tile(2));

        var openAdjacencies = _mapSys.GetZLevelAdjacencies(_grid.Owner, _grid.Comp, origin).ToArray();
        var allAdjacencies = _mapSys.GetZLevelAdjacencies(_grid.Owner, _grid.Comp, origin, includeBlocked: true).ToArray();
        var up = allAdjacencies.Single(adj => adj.Direction == ZLevelAdjacencyDirection.Up);
        var eastAdjacency = allAdjacencies.Single(adj => adj.Direction == ZLevelAdjacencyDirection.East);
        var downAdjacency = allAdjacencies.Single(adj => adj.Direction == ZLevelAdjacencyDirection.Down);

        Assert.Multiple(() =>
        {
            Assert.That(openAdjacencies.Length, NUnit.Framework.Is.EqualTo(5));
            Assert.That(allAdjacencies.Length, NUnit.Framework.Is.EqualTo(6));
            Assert.That(up.Target, NUnit.Framework.Is.EqualTo(above));
            Assert.That(up.IsOpen, NUnit.Framework.Is.True);
            Assert.That(eastAdjacency.Target, NUnit.Framework.Is.EqualTo(east));
            Assert.That(eastAdjacency.IsOpen, NUnit.Framework.Is.True);
            Assert.That(downAdjacency.Target, NUnit.Framework.Is.EqualTo(below));
            Assert.That(downAdjacency.IsOpen, NUnit.Framework.Is.False);
            Assert.That(_mapSys.TryGetZLevelAdjacency(_grid.Owner, _grid.Comp, origin, ZLevelAdjacencyDirection.Down, out var down), NUnit.Framework.Is.True);
            Assert.That(down.Direction, NUnit.Framework.Is.EqualTo(ZLevelAdjacencyDirection.Down));
            Assert.That(down.IsOpen, NUnit.Framework.Is.False);
            Assert.That(ZLevelAdjacencyDirection.Up.Opposite(), NUnit.Framework.Is.EqualTo(ZLevelAdjacencyDirection.Down));
        });
    }

    [Test]
    public void ZLevelStackOpenRequiresEveryIntermediateBoundaryToBeOpen()
    {
        var xy = new Vector2i(5, 5);

        Assert.That(_mapSys.IsZLevelStackOpen(_grid.Owner, _grid.Comp, xy, 3, 1), NUnit.Framework.Is.True);

        _mapSys.SetZLevelTile(_grid.Owner, _grid.Comp, new ZLevelTileIndices(xy.X, xy.Y, 3), new Tile(1));
        Assert.That(_mapSys.IsZLevelStackOpen(_grid.Owner, _grid.Comp, xy, 3, 1), NUnit.Framework.Is.False);

        _mapSys.SetZLevelTile(_grid.Owner, _grid.Comp, new ZLevelTileIndices(xy.X, xy.Y, 3), Tile.Empty);
        _mapSys.SetZLevelTile(_grid.Owner, _grid.Comp, new ZLevelTileIndices(xy.X, xy.Y, 2), new Tile(1));
        Assert.That(_mapSys.IsZLevelStackOpen(_grid.Owner, _grid.Comp, xy, 3, 1), NUnit.Framework.Is.False);

        _mapSys.SetZLevelTile(_grid.Owner, _grid.Comp, new ZLevelTileIndices(xy.X, xy.Y, 2), Tile.Empty);
        Assert.That(_mapSys.IsZLevelStackOpen(_grid.Owner, _grid.Comp, xy, 3, 1), NUnit.Framework.Is.True);
        Assert.That(_mapSys.IsZLevelStackOpen(_grid.Owner, _grid.Comp, xy, 1, 3), NUnit.Framework.Is.True);
    }

    [Test]
    public void ZLevelSparseLayersAndEntityVerticalStateWork()
    {
        var layer = new ZLevelTileIndices(10, 10, 6);
        _mapSys.SetZLevelTile(_grid.Owner, _grid.Comp, layer, new Tile(1));
        CollectionAssert.Contains(_mapSys.GetExistingZLevelLayers(_grid.Owner, _grid.Comp).ToArray(), 6);

        _mapSys.SetZLevelTile(_grid.Owner, _grid.Comp, layer, Tile.Empty);
        CollectionAssert.DoesNotContain(_mapSys.GetExistingZLevelLayers(_grid.Owner, _grid.Comp).ToArray(), 6);

        var entityWithoutDz = _entMan.SpawnEntity(null, new EntityCoordinates(_grid.Owner, new Vector2(1.5f, 2.5f)));
        var noDzMap = _xform.GetZLevelMapCoordinates((entityWithoutDz, _entMan.GetComponent<TransformComponent>(entityWithoutDz), _entMan.GetComponentOrNull<ZLevelPositionComponent>(entityWithoutDz)));
        Assert.That(noDzMap.Z, NUnit.Framework.Is.EqualTo(0));

        var entityWithDz = _entMan.SpawnEntity(null, new EntityCoordinates(_grid.Owner, new Vector2(5.5f, 6.5f)));
        var dzComp = _entMan.EnsureComponent<ZLevelPositionComponent>(entityWithDz);
        dzComp.ZLevel = 7;
        dzComp.LocalZOffset = 0.25f;

        var dzMap = _xform.GetZLevelMapCoordinates((entityWithDz, _entMan.GetComponent<TransformComponent>(entityWithDz), dzComp));

        Assert.Multiple(() =>
        {
            Assert.That(dzMap.Z, NUnit.Framework.Is.EqualTo(7));
            Assert.That(_xform.GetZLevelWorldHeight((entityWithDz, _entMan.GetComponent<TransformComponent>(entityWithDz), dzComp)), NUnit.Framework.Is.EqualTo(7.25f));
        });

        var child = _entMan.SpawnEntity(null, new EntityCoordinates(entityWithDz, Vector2.Zero));
        var childXform = _entMan.GetComponent<TransformComponent>(child);
        var childMap = _xform.GetZLevelMapCoordinates((child, childXform, _entMan.GetComponentOrNull<ZLevelPositionComponent>(child)));

        Assert.Multiple(() =>
        {
            Assert.That(_xform.GetZLevel((child, childXform, _entMan.GetComponentOrNull<ZLevelPositionComponent>(child))), NUnit.Framework.Is.EqualTo(7));
            Assert.That(_xform.GetZLevelWorldHeight((child, childXform, _entMan.GetComponentOrNull<ZLevelPositionComponent>(child))), NUnit.Framework.Is.EqualTo(7.25f));
            Assert.That(childMap.Z, NUnit.Framework.Is.EqualTo(7));
        });
    }

    [Test]
    public void ZLevelBoundedQueriesReturnNearestSupportWithoutAllocatingEmptyLayers()
    {
        var xy = new Vector2i(14, 14);
        _mapSys.SetZLevelTile(_grid.Owner, _grid.Comp, new ZLevelTileIndices(xy.X, xy.Y, 0), new Tile(1));
        _mapSys.SetZLevelTile(_grid.Owner, _grid.Comp, new ZLevelTileIndices(xy.X, xy.Y, 3), new Tile(2));
        _mapSys.SetZLevelTile(_grid.Owner, _grid.Comp, new ZLevelTileIndices(xy.X, xy.Y, 5), new Tile(3));

        var existing = _mapSys.GetExistingZLevelLayersAt(_grid.Owner, _grid.Comp, xy, -2, 4).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(existing, NUnit.Framework.Is.EqualTo(new[] { 0, 3 }));
            Assert.That(_mapSys.TryFindNearestZLevelSolidBelow(_grid.Owner, _grid.Comp, xy, 5, 5, out var below), NUnit.Framework.Is.True);
            Assert.That(below.GridIndices.Z, NUnit.Framework.Is.EqualTo(5));
            Assert.That(_mapSys.TryFindNearestZLevelSolidBelow(_grid.Owner, _grid.Comp, xy, 4, 5, out below), NUnit.Framework.Is.True);
            Assert.That(below.GridIndices.Z, NUnit.Framework.Is.EqualTo(3));
            Assert.That(_mapSys.TryFindNearestZLevelSolidAbove(_grid.Owner, _grid.Comp, xy, 1, 5, out var above), NUnit.Framework.Is.True);
            Assert.That(above.GridIndices.Z, NUnit.Framework.Is.EqualTo(3));
            Assert.That(_mapSys.IsZLevelTileEmpty(_grid.Owner, _grid.Comp, new ZLevelTileIndices(xy.X, xy.Y, 4)), NUnit.Framework.Is.True);
        });

        var support = _mapSys.TryGetZLevelSupportTile(_grid.Owner, _grid.Comp, xy, 5, 5, out var supportTile);
        Assert.That(support, NUnit.Framework.Is.True);
        Assert.That(supportTile.GridIndices.Z, NUnit.Framework.Is.EqualTo(5));

        _mapSys.SetZLevelTile(_grid.Owner, _grid.Comp, new ZLevelTileIndices(xy.X, xy.Y, 5), Tile.Empty);
        support = _mapSys.TryGetZLevelSupportTile(_grid.Owner, _grid.Comp, xy, 5, 5, out supportTile);

        Assert.That(support, NUnit.Framework.Is.True);
        Assert.That(supportTile.GridIndices.Z, NUnit.Framework.Is.EqualTo(3));
    }
}
