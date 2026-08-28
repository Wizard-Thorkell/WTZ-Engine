using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Robust.Shared.Tests.Networking;

[TestFixture]
[Parallelizable(ParallelScope.All)]
[TestOf(typeof(FullInputCmdMessage))]
internal sealed class InputCmdMessageTest
{
    [Test]
    public void CoordinateLayerDefaultsToNull()
    {
        var message = new FullInputCmdMessage(
            GameTick.Zero,
            0,
            default,
            BoundKeyState.Down,
            NetCoordinates.Invalid,
            default,
            NetEntity.Invalid);

        Assert.That(message.CoordinateLayer, Is.Null);
    }

    [Test]
    public void CoordinateLayerIsPreservedByBothMessageForms()
    {
        const int layer = -7;
        IFullInputCmdMessage network = new FullInputCmdMessage(
            GameTick.Zero,
            0,
            default,
            BoundKeyState.Down,
            NetCoordinates.Invalid,
            default,
            NetEntity.Invalid,
            layer);
        IFullInputCmdMessage local = new ClientFullInputCmdMessage(
            GameTick.Zero,
            0,
            default,
            EntityCoordinates.Invalid,
            default,
            BoundKeyState.Down,
            EntityUid.Invalid,
            layer);

        Assert.Multiple(() =>
        {
            Assert.That(network.CoordinateLayer, Is.EqualTo(layer));
            Assert.That(local.CoordinateLayer, Is.EqualTo(layer));
        });
    }
}
