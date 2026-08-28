using System.Numerics;
using System.Threading.Tasks;
using NUnit.Framework;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using static Robust.Shared.GameObjects.OccluderComponent;

namespace Robust.UnitTesting.Client.Graphics;

[TestFixture]
[TestOf(typeof(OccluderSystem))]
public sealed class ZLevelOccluderTest : RobustIntegrationTest
{
    [Test]
    public async Task AnchoredOccluderNeighborsMustShareWorldZLevel()
    {
        var client = StartClient(ZLevelClientIntegrationSetup.Create());
        await client.WaitIdleAsync();

        await client.WaitAssertion(() =>
        {
            var entities = client.EntMan;
            var mapManager = client.ResolveDependency<IMapManager>();
            ZLevelClientIntegrationSetup.StartEntitySystems(entities, mapManager);
            var mapSystem = client.System<SharedMapSystem>();
            var transform = client.System<SharedTransformSystem>();
            var occluderSystem = client.System<OccluderSystem>();
            mapSystem.CreateMap(out var mapId);
            var gridUid = mapManager.CreateGridEntity(mapId);
            var grid = entities.GetComponent<Robust.Shared.Map.Components.MapGridComponent>(gridUid);

            mapSystem.SetTile(gridUid, grid, new Vector2i(0, 0), new Tile(1));
            mapSystem.SetTile(gridUid, grid, new Vector2i(1, 0), new Tile(1));

            var first = entities.SpawnEntity(null, new EntityCoordinates(gridUid, new Vector2(0.5f, 0.5f)));
            var second = entities.SpawnEntity(null, new EntityCoordinates(gridUid, new Vector2(1.5f, 0.5f)));
            entities.EnsureComponent<ZLevelPositionComponent>(first).ZLevel = 1;
            var secondZ = entities.EnsureComponent<ZLevelPositionComponent>(second);
            secondZ.ZLevel = 2;
            var firstOccluder = entities.EnsureComponent<OccluderComponent>(first);
            var secondOccluder = entities.EnsureComponent<OccluderComponent>(second);

            transform.AnchorEntity(first);
            transform.AnchorEntity(second);
            occluderSystem.FrameUpdate(0f);

            Assert.Multiple(() =>
            {
                Assert.That(firstOccluder.Occluding, Is.EqualTo(OccluderDir.None));
                Assert.That(secondOccluder.Occluding, Is.EqualTo(OccluderDir.None));
            });

            secondZ.ZLevel = 1;
            var changed = new ZLevelPositionChangedEvent(2, 1);
            entities.EventBus.RaiseLocalEvent(second, ref changed, true);
            occluderSystem.FrameUpdate(0f);

            Assert.Multiple(() =>
            {
                Assert.That(firstOccluder.Occluding, Is.Not.EqualTo(OccluderDir.None));
                Assert.That(secondOccluder.Occluding, Is.Not.EqualTo(OccluderDir.None));
            });
        });
    }
}
