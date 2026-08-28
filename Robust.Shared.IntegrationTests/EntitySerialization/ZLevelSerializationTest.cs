using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using NUnit.Framework;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using Robust.UnitTesting;
using Robust.UnitTesting.Shared.EntitySerialization;

namespace Robust.Shared.IntegrationTests.EntitySerialization;

[TestFixture]
internal sealed class ZLevelSerializationTest : RobustIntegrationTest
{
    private const string TestPrototypes = """
- type: testTileDef
  id: space

- type: testTileDef
  id: a

- type: testTileDef
  id: b
""";

    [Test]
    public async Task ZLevelFrameFollowsGridAcrossMapMovement()
    {
        var server = StartServer(new() { Pool = false, ExtraPrototypes = TestPrototypes });
        await server.WaitIdleAsync();

        var entMan = server.EntMan;
        var mapSys = server.System<SharedMapSystem>();
        var transform = server.System<SharedTransformSystem>();
        var mapMan = server.ResolveDependency<IMapManager>();
        var tileMan = server.ResolveDependency<ITileDefinitionManager>();

        SerializationTestHelper.LoadTileDefs(server.ProtoMan, tileMan, "space");
        var upperTile = server.ProtoMan.Index<TileDef>("b");

        await server.WaitAssertion(() =>
        {
            mapSys.CreateMap(out var sourceMap);
            var targetMapUid = mapSys.CreateMap(out var targetMap);
            var grid = mapMan.CreateGridEntity(sourceMap);
            var upperIndices = new ZLevelTileIndices(1, 2, 3);
            mapSys.SetTile(grid.Owner, grid.Comp, new Vector2i(1, 2), new Tile(upperTile.TileId));
            mapSys.SetZLevelTile(grid.Owner, grid.Comp, upperIndices, new Tile(upperTile.TileId));

            var passenger = entMan.SpawnEntity(null, new EntityCoordinates(grid.Owner, 1.5f, 2.5f));
            var position = entMan.AddComponent<ZLevelPositionComponent>(passenger);
            position.ZLevel = 3;
            position.LocalZOffset = 0.25f;
            Assert.That(transform.SetZLevelFrameOrigin(grid.Owner, 7), Is.True);

            var gridTransform = entMan.GetComponent<TransformComponent>(grid.Owner);
            transform.SetCoordinates(
                grid.Owner,
                gridTransform,
                new EntityCoordinates(targetMapUid, new Vector2(20f, -8f)),
                rotation: Angle.FromDegrees(90));

            var tileMapCoordinates = mapSys.GridTileToZLevelMap(grid.Owner, grid.Comp, upperIndices);
            var convertedIndices = mapSys.ZLevelTileIndicesFor(grid.Owner, grid.Comp, tileMapCoordinates);

            Assert.Multiple(() =>
            {
                Assert.That(gridTransform.MapID, Is.EqualTo(targetMap));
                Assert.That(transform.GetZLevel((passenger, Transform(passenger), position)), Is.EqualTo(3));
                Assert.That(transform.GetWorldZLevel((passenger, Transform(passenger), position)), Is.EqualTo(10));
                Assert.That(transform.GetZLevelWorldHeight((passenger, Transform(passenger), position)), Is.EqualTo(10.25f));
                Assert.That(transform.LocalToWorldZLevel(grid.Owner, 3), Is.EqualTo(10));
                Assert.That(transform.WorldToLocalZLevel(grid.Owner, 10), Is.EqualTo(3));
                Assert.That(tileMapCoordinates.Z, Is.EqualTo(10));
                Assert.That(convertedIndices, Is.EqualTo(upperIndices));
                Assert.That(mapSys.TryGetZLevelSupportTile(tileMapCoordinates, 0, out var support), Is.True);
                Assert.That(support.GridIndices, Is.EqualTo(upperIndices));
                Assert.That(mapSys.GetZLevelTileRef(grid.Owner, grid.Comp, upperIndices).Tile.TypeId, Is.EqualTo(upperTile.TileId));
            });

            TransformComponent Transform(EntityUid uid) => entMan.GetComponent<TransformComponent>(uid);
        });
    }

    [Test]
    public async Task ZLevelTilesRoundTripThroughMapSaveLoad()
    {
        var server = StartServer(new() { Pool = false, ExtraPrototypes = TestPrototypes });
        await server.WaitIdleAsync();

        var entMan = server.EntMan;
        var loader = server.System<MapLoaderSystem>();
        var mapSys = server.System<SharedMapSystem>();
        var transform = server.System<SharedTransformSystem>();
        var mapMan = server.ResolveDependency<IMapManager>();
        var tileMan = server.ResolveDependency<ITileDefinitionManager>();
        var mapPath = new ResPath($"{nameof(ZLevelSerializationTest)}_map.yml");

        SerializationTestHelper.LoadTileDefs(server.ProtoMan, tileMan, "space");
        var baseTile = server.ProtoMan.Index<TileDef>("a");
        var upperTile = server.ProtoMan.Index<TileDef>("b");

        MapId mapId = default;
        EntityUid gridUid = default;

        await server.WaitPost(() =>
        {
            var mapUid = mapSys.CreateMap(out mapId);
            gridUid = mapMan.CreateGridEntity(mapId);
            var grid = entMan.GetComponent<MapGridComponent>(gridUid);

            Assert.That(transform.SetZLevelFrameOrigin(gridUid, 4), Is.True);
            mapSys.SetTile(gridUid, grid, Vector2i.Zero, new Tile(baseTile.TileId));
            mapSys.SetZLevelTile(gridUid, grid, new ZLevelTileIndices(0, 0, 1), new Tile(upperTile.TileId));
            mapSys.SetZLevelTile(gridUid, grid, new ZLevelTileIndices(0, 0, -1), new Tile(baseTile.TileId));
        });

        await server.WaitAssertion(() => Assert.That(loader.TrySaveMap(mapId, mapPath)));
        await server.WaitPost(() => mapSys.DeleteMap(mapId));

        Entity<MapComponent>? loadedMap = null;
        HashSet<Entity<MapGridComponent>>? loadedGrids = null;
        await server.WaitAssertion(() => Assert.That(loader.TryLoadMap(mapPath, out loadedMap, out loadedGrids)));

        Assert.That(loadedGrids, Has.Count.EqualTo(1));
        var loadedGrid = loadedGrids!.Single();

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(mapSys.GetTileRef(loadedGrid.Owner, loadedGrid.Comp, Vector2i.Zero).Tile.TypeId, Is.EqualTo(baseTile.TileId));
                Assert.That(mapSys.GetZLevelTileRef(loadedGrid.Owner, loadedGrid.Comp, new ZLevelTileIndices(0, 0, 1)).Tile.TypeId, Is.EqualTo(upperTile.TileId));
                Assert.That(mapSys.GetZLevelTileRef(loadedGrid.Owner, loadedGrid.Comp, new ZLevelTileIndices(0, 0, -1)).Tile.TypeId, Is.EqualTo(baseTile.TileId));
                Assert.That(mapSys.GetExistingZLevelLayers(loadedGrid.Owner, loadedGrid.Comp), Is.EquivalentTo(new[] { -1, 0, 1 }));
                Assert.That(entMan.GetComponent<ZLevelFrameComponent>(loadedGrid.Owner).Origin, Is.EqualTo(4));
            });
        });
    }

    [Test]
    public async Task ZLevelOnlyChunksRoundTripThroughMapSaveLoad()
    {
        var server = StartServer(new() { Pool = false, ExtraPrototypes = TestPrototypes });
        await server.WaitIdleAsync();

        var entMan = server.EntMan;
        var loader = server.System<MapLoaderSystem>();
        var mapSys = server.System<SharedMapSystem>();
        var mapMan = server.ResolveDependency<IMapManager>();
        var tileMan = server.ResolveDependency<ITileDefinitionManager>();
        var mapPath = new ResPath($"{nameof(ZLevelSerializationTest)}_z_only_map.yml");

        SerializationTestHelper.LoadTileDefs(server.ProtoMan, tileMan, "space");
        var upperTile = server.ProtoMan.Index<TileDef>("b");
        var zOnlyTile = new ZLevelTileIndices(32, 0, 2);
        var transientTile = new ZLevelTileIndices(47, 15, 3);

        MapId mapId = default;

        await server.WaitPost(() =>
        {
            var mapUid = mapSys.CreateMap(out mapId);
            var gridUid = mapMan.CreateGridEntity(mapId);
            var grid = entMan.GetComponent<MapGridComponent>(gridUid);

            mapSys.SetZLevelTile(gridUid, grid, zOnlyTile, new Tile(upperTile.TileId));
            Assert.That(mapSys.GetTileRef(gridUid, grid, new Vector2i(zOnlyTile.X, zOnlyTile.Y)).Tile.IsEmpty, Is.True);
            Assert.That(grid.LocalAABB.Contains(new Vector2(zOnlyTile.X + 0.5f, zOnlyTile.Y + 0.5f)), Is.True);

            mapSys.SetZLevelTile(gridUid, grid, transientTile, new Tile(upperTile.TileId));
            Assert.That(grid.LocalAABB.Contains(new Vector2(transientTile.X + 0.5f, transientTile.Y + 0.5f)), Is.True);

            mapSys.SetZLevelTile(gridUid, grid, transientTile, Tile.Empty);
            Assert.Multiple(() =>
            {
                Assert.That(grid.LocalAABB.Contains(new Vector2(zOnlyTile.X + 0.5f, zOnlyTile.Y + 0.5f)), Is.True);
                Assert.That(grid.LocalAABB.Contains(new Vector2(transientTile.X + 0.5f, transientTile.Y + 0.5f)), Is.False);
            });
        });

        await server.WaitAssertion(() => Assert.That(loader.TrySaveMap(mapId, mapPath)));
        await server.WaitPost(() => mapSys.DeleteMap(mapId));

        Entity<MapComponent>? loadedMap = null;
        HashSet<Entity<MapGridComponent>>? loadedGrids = null;
        await server.WaitAssertion(() => Assert.That(loader.TryLoadMap(mapPath, out loadedMap, out loadedGrids)));

        Assert.That(loadedGrids, Has.Count.EqualTo(1));
        var loadedGrid = loadedGrids!.Single();

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(mapSys.GetTileRef(loadedGrid.Owner, loadedGrid.Comp, new Vector2i(zOnlyTile.X, zOnlyTile.Y)).Tile.IsEmpty, Is.True);
                Assert.That(mapSys.GetZLevelTileRef(loadedGrid.Owner, loadedGrid.Comp, zOnlyTile).Tile.TypeId, Is.EqualTo(upperTile.TileId));
                Assert.That(mapSys.GetExistingZLevelLayers(loadedGrid.Owner, loadedGrid.Comp), Is.EquivalentTo(new[] { 2 }));
                Assert.That(loadedGrid.Comp.LocalAABB.Contains(new Vector2(zOnlyTile.X + 0.5f, zOnlyTile.Y + 0.5f)), Is.True);
            });
        });
    }

    [Test]
    public async Task ZLevelTileRegionsCanBeCopiedAndCleared()
    {
        var server = StartServer(new() { Pool = false, ExtraPrototypes = TestPrototypes });
        await server.WaitIdleAsync();

        var entMan = server.EntMan;
        var mapSys = server.System<SharedMapSystem>();
        var mapMan = server.ResolveDependency<IMapManager>();
        var tileMan = server.ResolveDependency<ITileDefinitionManager>();

        SerializationTestHelper.LoadTileDefs(server.ProtoMan, tileMan, "space");
        var tileA = server.ProtoMan.Index<TileDef>("a");
        var tileB = server.ProtoMan.Index<TileDef>("b");

        await server.WaitAssertion(() =>
        {
            mapSys.CreateMap(out var mapId);
            var gridUid = mapMan.CreateGridEntity(mapId);
            var grid = entMan.GetComponent<MapGridComponent>(gridUid);

            mapSys.SetZLevelTile(gridUid, grid, new ZLevelTileIndices(0, 0, 1), new Tile(tileA.TileId));
            mapSys.SetZLevelTile(gridUid, grid, new ZLevelTileIndices(1, 0, 1), new Tile(tileB.TileId));
            mapSys.SetZLevelTile(gridUid, grid, new ZLevelTileIndices(2, 0, 2), new Tile(tileB.TileId));

            var copied = mapSys.CopyZLevelTileRegion(gridUid, grid, new Vector2i(1, 0), new Vector2i(0, 0), 1, 2);
            Assert.That(copied, Is.EqualTo(2));
            Assert.That(mapSys.GetZLevelTileRef(gridUid, grid, new ZLevelTileIndices(0, 0, 2)).Tile.TypeId, Is.EqualTo(tileA.TileId));
            Assert.That(mapSys.GetZLevelTileRef(gridUid, grid, new ZLevelTileIndices(1, 0, 2)).Tile.TypeId, Is.EqualTo(tileB.TileId));
            Assert.That(mapSys.GetZLevelTileRef(gridUid, grid, new ZLevelTileIndices(2, 0, 2)).Tile.TypeId, Is.EqualTo(tileB.TileId));

            var cleared = mapSys.ClearZLevelTileRegion(gridUid, grid, new Vector2i(0, 0), new Vector2i(1, 0), 2);
            Assert.That(cleared, Is.EqualTo(2));
            Assert.That(mapSys.GetZLevelTileRef(gridUid, grid, new ZLevelTileIndices(0, 0, 2)).Tile.IsEmpty, Is.True);
            Assert.That(mapSys.GetZLevelTileRef(gridUid, grid, new ZLevelTileIndices(1, 0, 2)).Tile.IsEmpty, Is.True);
            Assert.That(mapSys.GetZLevelTileRef(gridUid, grid, new ZLevelTileIndices(2, 0, 2)).Tile.TypeId, Is.EqualTo(tileB.TileId));
        });
    }
}
