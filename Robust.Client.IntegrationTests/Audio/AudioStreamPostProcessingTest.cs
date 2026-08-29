using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Robust.Client.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Robust.UnitTesting.Client.Audio;

[TestFixture]
[TestOf(typeof(AudioSystem))]
public sealed class AudioStreamPostProcessingTest : RobustIntegrationTest
{
    [Test]
    public async Task PostProcessingRunsAfterDefaultEarlyMute()
    {
        var client = StartClient(new ClientIntegrationOptions
        {
            Pool = false,
        });
        await client.WaitIdleAsync();

        await client.WaitAssertion(() =>
        {
            var entities = client.EntMan;
            var maps = client.ResolveDependency<IMapManager>();
            entities.Startup();
            maps.Startup();

            var mapSystem = client.System<SharedMapSystem>();
            mapSystem.CreateMap(out var mapId);

            var audioUid = entities.CreateEntityUninitialized(null, new MapCoordinates(1f, 1f, mapId));
            var audio = entities.AddComponent<AudioComponent>(audioUid);
            // Keep the headless fixture on its default dummy source; no audio assets are loaded in this suite.
            typeof(AudioComponent).GetField(nameof(AudioComponent.Loaded))!.SetValue(audio, true);
            entities.InitializeAndStartEntity(audioUid);

            var audioSystem = client.System<AudioSystem>();
            var calls = 0;
            EntityUid? processed = null;

            void OnProcessed(EntityUid uid, AudioComponent _, TransformComponent __, MapCoordinates ___)
            {
                processed = uid;
                Interlocked.Increment(ref calls);
            }

            audioSystem.StreamProcessed += OnProcessed;
            try
            {
                // The default eye remains in nullspace, so native processing mutes this map sound first.
                audioSystem.FrameUpdate(0f);
            }
            finally
            {
                audioSystem.StreamProcessed -= OnProcessed;
            }

            Assert.Multiple(() =>
            {
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(processed, Is.EqualTo(audioUid));
                Assert.That(audio.Gain, Is.Zero);
            });
        });
    }
}
