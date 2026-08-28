using System;
using System.Numerics;
using System.Threading.Tasks;
using NUnit.Framework;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Robust.UnitTesting.Client.Graphics
{
    [TestFixture]
    [TestOf(typeof(IEyeManager))]
    public sealed class EyeManagerTest : RobustIntegrationTest
    {
        [Test]
        public async Task TestViewportRotation()
        {
            // At cardinal rotations with a square viewport these should all be the same.
            var client = StartClient();
            await client.WaitIdleAsync();

            var eyeManager = client.ResolveDependency<IEyeManager>();

            await client.WaitAssertion(() =>
            {
                // At this stage integration tests aren't pooled so no way I'm making each of these a new test for now.
                foreach (var angle in new[]
                {
                    Angle.Zero,
                    new Angle(Math.PI / 4),
                    new Angle(Math.PI / 2),
                    new Angle(Math.PI),
                    new Angle(-Math.PI / 4),
                    new Angle(-Math.PI / 2),
                    new Angle(-Math.PI)
                })
                {

                    eyeManager.CurrentEye.Rotation = angle;

                    var worldAABB = eyeManager.GetWorldViewport();
                    var worldPort = eyeManager.GetWorldViewbounds();
                    Assert.That(worldAABB.EqualsApprox(worldPort.CalcBoundingBox()),
                        $"Invalid EyeRotation bounds found for {angle}: Expected {worldAABB} and received {worldPort.CalcBoundingBox()}");
                }
            });
        }

        [Test]
        public async Task EntityEyeTracksTargetWorldZLevel()
        {
            var client = StartClient(ZLevelClientIntegrationSetup.Create());
            await client.WaitIdleAsync();

            await client.WaitAssertion(() =>
            {
                var entities = client.EntMan;
                var mapManager = client.ResolveDependency<IMapManager>();
                ZLevelClientIntegrationSetup.StartEntitySystems(entities, mapManager);
                var eyeSystem = client.System<EyeSystem>();
                var mapSystem = client.System<SharedMapSystem>();
                var transform = client.System<SharedTransformSystem>();
                mapSystem.CreateMap(out var mapId);
                var grid = mapManager.CreateGridEntity(mapId);
                var gridComponent = entities.GetComponent<Robust.Shared.Map.Components.MapGridComponent>(grid);
                mapSystem.SetTile(grid, gridComponent, Vector2i.Zero, new Tile(1));

                Assert.That(transform.SetZLevelFrameOrigin(grid, 5), Is.True);

                var owner = entities.SpawnEntity(null, new EntityCoordinates(grid, new Vector2(0.25f, 0.25f)));
                var target = entities.SpawnEntity(null, new EntityCoordinates(grid, new Vector2(0.75f, 0.75f)));
                entities.EnsureComponent<ZLevelPositionComponent>(target).ZLevel = 2;

                var eye = entities.EnsureComponent<EyeComponent>(owner);
                eyeSystem.SetTarget(owner, target, eye);
                eyeSystem.FrameUpdate(0f);

                Assert.Multiple(() =>
                {
                    Assert.That(entities.GetComponent<TransformComponent>(target).GridUid, Is.EqualTo((EntityUid?) grid));
                    Assert.That(eye.Eye.Position.MapId, Is.EqualTo(mapId));
                    Assert.That(eye.Eye.WorldZLevel, Is.EqualTo(7));
                });
            });
        }
    }
}
