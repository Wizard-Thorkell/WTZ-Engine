using System.Numerics;
using NUnit.Framework;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.UnitTesting;

namespace Robust.Client.Tests.Graphics
{
    [TestFixture, Parallelizable, TestOf(typeof(Eye))]
    internal sealed class EyeTest
    {
        [Test]
        public void Constructor_DefaultZoom_isTwo()
        {
            var eye = new Eye();

            Assert.That(eye.Zoom, Is.Approximately(Vector2.One*2f));
        }

        [Test]
        public void Constructor_DefaultPosition_isZero()
        {
            var eye = new Eye();

            Assert.That(eye.Position, Is.EqualTo(MapCoordinates.Nullspace));
        }

        [Test]
        public void Constructor_DefaultDrawFov_isTrue()
        {
            var eye = new Eye();

            Assert.That(eye.DrawFov, Is.True);
        }

        [Test]
        public void WorldZLevel_DefaultsToZeroAndCanBeSet()
        {
            var eye = new Eye();

            Assert.That(eye.WorldZLevel, Is.Zero);

            eye.WorldZLevel = 7;
            Assert.That(eye.WorldZLevel, Is.EqualTo(7));
        }
    }
}
