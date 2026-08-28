using System;
using System.Numerics;
using System.Threading.Tasks;
using NUnit.Framework;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Robust.UnitTesting.Client.Graphics;

[TestFixture]
[TestOf(typeof(IClyde))]
public sealed class LightShadowMapApiTest : RobustIntegrationTest
{
    [Test]
    public async Task HeadlessClydeImplementsExternalShadowAtlasContract()
    {
        var client = StartClient(ZLevelClientIntegrationSetup.Create());
        await client.WaitIdleAsync();

        await client.WaitAssertion(() =>
        {
            var clyde = client.ResolveDependency<IClyde>();
            using var atlas = clyde.CreateLightShadowMap(4, "external-shadow-contract-test");
            using var viewport = clyde.CreateViewport((64, 64), "external-shadow-contract-viewport");
            LightShadowMapRequest[] requests =
            [
                new(new Vector2(1f, 1f), 3f, 2),
                new(new Vector2(2f, 1f), 3f, 2),
            ];

            var stats = clyde.RenderLightShadowMap(atlas, viewport, MapId.Nullspace, requests);

            Assert.Multiple(() =>
            {
                Assert.That(atlas.Size, Is.EqualTo(new Vector2i(LightShadowMap.Width, 4)));
                Assert.That(stats, Is.EqualTo(default(LightShadowMapRenderStats)));
                Assert.That(() => clyde.CreateLightShadowMap(0), Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        });
    }
}
