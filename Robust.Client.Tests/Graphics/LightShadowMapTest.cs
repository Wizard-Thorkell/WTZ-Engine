using System;
using System.Numerics;
using NUnit.Framework;
using Robust.Client.Graphics;

namespace Robust.Client.Tests.Graphics;

[TestFixture]
[TestOf(typeof(LightShadowMap))]
internal sealed class LightShadowMapTest
{
    [Test]
    public void ContractRetainsRowsAndCountsContiguousFloorGroups()
    {
        LightShadowMapRequest[] requests =
        [
            new(new Vector2(1f, 2f), 4f, 3),
            new(new Vector2(2f, 2f), 5f, 3),
            new(new Vector2(3f, 2f), 6f, 1),
            new(new Vector2(4f, 2f), 7f, 3),
        ];

        var stats = LightShadowMapContract.Validate((LightShadowMap.Width, 8), requests);

        Assert.Multiple(() =>
        {
            Assert.That(stats.Lights, Is.EqualTo(4));
            Assert.That(stats.FloorGroups, Is.EqualTo(3));
        });
    }

    [Test]
    public void ContractRejectsWrongAtlasCapacity()
    {
        var request = new LightShadowMapRequest(Vector2.Zero, 1f, 0);

        Assert.Multiple(() =>
        {
            Assert.That(
                () => LightShadowMapContract.Validate((LightShadowMap.Width - 1, 1), [request]),
                Throws.TypeOf<ArgumentException>());
            Assert.That(
                () => LightShadowMapContract.Validate((LightShadowMap.Width, 0), [request]),
                Throws.TypeOf<ArgumentException>());
        });
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void ContractRejectsInvalidRadius(float radius)
    {
        var request = new LightShadowMapRequest(Vector2.Zero, radius, 0);

        Assert.That(
            () => LightShadowMapContract.Validate((LightShadowMap.Width, 1), [request]),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void ContractRejectsNonFinitePosition()
    {
        var request = new LightShadowMapRequest(new Vector2(float.NaN, 0f), 1f, 0);

        Assert.That(
            () => LightShadowMapContract.Validate((LightShadowMap.Width, 1), [request]),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }
}
