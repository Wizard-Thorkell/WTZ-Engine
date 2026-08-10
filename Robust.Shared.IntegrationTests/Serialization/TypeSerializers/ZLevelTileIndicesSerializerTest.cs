// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Linq;
using NUnit.Framework;
using Robust.Shared.Map;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Value;

namespace Robust.UnitTesting.Shared.Serialization.TypeSerializers;

[TestFixture]
internal sealed class ZLevelTileIndicesSerializerTest : OurSerializationTest
{
    [Test]
    public void RoundTripTest()
    {
        var node = new ValueDataNode("-3,4,8");
        var validation = Serialization.ValidateNode<ZLevelTileIndices>(node);
        Assert.That(validation.GetErrors(), Is.Empty);

        var value = Serialization.Read<ZLevelTileIndices>(node);
        Assert.That(value, Is.EqualTo(new ZLevelTileIndices(-3, 4, 8)));

        var written = Serialization.WriteValueAs<ValueDataNode>(value);
        Assert.That(written, Is.EqualTo(node));
    }

    [Test]
    public void RejectsMalformedValueTest()
    {
        var validation = Serialization.ValidateNode<ZLevelTileIndices>(new ValueDataNode("1,2"));
        Assert.That(validation.GetErrors().Any(), Is.True);
    }
}
