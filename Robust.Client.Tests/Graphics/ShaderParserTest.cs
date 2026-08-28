using NUnit.Framework;
using Robust.Client.Graphics;

namespace Robust.Client.Tests.Graphics
{
    [TestFixture]
    internal sealed class ShaderParserTest
    {
        [Test]
        public void LightAddBlendModeParses()
        {
            const string source = """
                light_mode unshaded;
                blend_mode light_add;

                void fragment()
                {
                    COLOR = vec4(1.0);
                }
                """;

            var shader = ShaderParser.Parse(source, null!);

            Assert.That(shader.BlendMode, Is.EqualTo(ShaderBlendMode.LightAdd));
        }
    }
}
