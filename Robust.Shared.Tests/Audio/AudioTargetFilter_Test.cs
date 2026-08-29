using System.Collections.Generic;
using NUnit.Framework;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;

namespace Robust.Shared.Tests.Audio;

[TestFixture]
public sealed class AudioTargetFilter_Test
{
    [Test]
    public void AppliesIncludedAndExcludedEntityFilters()
    {
        var included = new EntityUid(1);
        var excluded = new EntityUid(2);
        var other = new EntityUid(3);
        var component = new AudioComponent();
        // These server-owned filter fields are intentionally write-protected outside SharedAudioSystem.
        typeof(AudioComponent).GetField(nameof(AudioComponent.IncludedEntities))!
            .SetValue(component, new HashSet<EntityUid> { included });
        typeof(AudioComponent).GetField(nameof(AudioComponent.ExcludedEntity))!
            .SetValue(component, excluded);

        Assert.Multiple(() =>
        {
            Assert.That(SharedAudioSystem.IsAudioTargetAllowed(component, included), Is.True);
            Assert.That(SharedAudioSystem.IsAudioTargetAllowed(component, excluded), Is.False);
            Assert.That(SharedAudioSystem.IsAudioTargetAllowed(component, other), Is.False);
            Assert.That(SharedAudioSystem.IsAudioTargetAllowed(component, null), Is.True);
        });
    }
}
