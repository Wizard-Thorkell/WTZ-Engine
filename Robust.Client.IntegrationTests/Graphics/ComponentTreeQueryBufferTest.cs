using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using NUnit.Framework;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Shared.ComponentTrees;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;

namespace Robust.UnitTesting.Client.Graphics;

[TestFixture]
[TestOf(typeof(LightTreeSystem))]
public sealed class ComponentTreeQueryBufferTest : RobustIntegrationTest
{
    private static readonly DynamicTree<ComponentTreeEntry<PointLightComponent>>.QueryCallbackDelegate<int>
        CountLightCallback = CountLight;

    [Test]
    public async Task CallerOwnedTreeBufferCanBeReusedWithoutPerQueryAllocation()
    {
        var client = StartClient(ZLevelClientIntegrationSetup.Create());
        await client.WaitIdleAsync();

        await client.WaitAssertion(() =>
        {
            var entities = client.EntMan;
            var mapManager = client.ResolveDependency<IMapManager>();
            ZLevelClientIntegrationSetup.StartEntitySystems(entities, mapManager);
            var mapSystem = client.System<SharedMapSystem>();
            var lightTree = client.System<LightTreeSystem>();
            mapSystem.CreateMap(out var mapId);
            var gridUid = mapManager.CreateGridEntity(mapId);
            var grid = entities.GetComponent<MapGridComponent>(gridUid);
            mapSystem.SetTile(gridUid, grid, Vector2i.Zero, new Tile(1));

            var light = entities.SpawnEntity(null, new EntityCoordinates(gridUid, new Vector2(0.5f, 0.5f)));
            entities.EnsureComponent<PointLightComponent>(light);
            lightTree.FrameUpdate(0f);

            var bounds = Box2.CenteredAround(new Vector2(0.5f, 0.5f), new Vector2(4f, 4f));
            var treeBuffer = new List<(EntityUid Uid, LightTreeComponent Comp)>(2);
            Assert.That(QueryRepeated(lightTree, mapId, bounds, treeBuffer, 1), Is.EqualTo(1));

            QueryRepeated(lightTree, mapId, bounds, treeBuffer, 512);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var hits = QueryRepeated(lightTree, mapId, bounds, treeBuffer, 100);

            Assert.Multiple(() =>
            {
                Assert.That(hits, Is.EqualTo(100));
                Assert.That(
                    GC.GetAllocatedBytesForCurrentThread() - before,
                    Is.LessThanOrEqualTo(512),
                    "A caller-owned tree buffer must avoid allocation proportional to query count.");
            });
        });
    }

    private static int QueryRepeated(
        LightTreeSystem lightTree,
        MapId mapId,
        Box2 bounds,
        List<(EntityUid Uid, LightTreeComponent Comp)> treeBuffer,
        int count)
    {
        var hits = 0;
        for (var i = 0; i < count; i++)
        {
            treeBuffer.Clear();
            var queryHits = 0;
            lightTree.QueryAabb(
                ref queryHits,
                CountLightCallback,
                mapId,
                bounds,
                treeBuffer);
            hits += queryHits;
        }

        return hits;
    }

    private static bool CountLight(ref int count, in ComponentTreeEntry<PointLightComponent> entry)
    {
        count++;
        return true;
    }
}
