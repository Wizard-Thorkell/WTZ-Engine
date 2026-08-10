using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Robust.Client.GameStates;
using Robust.Server.GameStates;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Robust.UnitTesting.Server.GameStates;

public sealed class PvsSessionCullingTest : RobustIntegrationTest
{
    [Test]
    public async Task SessionCullingDetachesAndRestoresEntity()
    {
        var server = StartServer();
        var client = StartClient();

        await Task.WhenAll(client.WaitIdleAsync(), server.WaitIdleAsync());

        var sEntMan = server.EntMan;
        var cEntMan = client.EntMan;
        var stateMan = (ClientGameStateManager) client.ResolveDependency<IClientGameStateManager>();
        var netMan = client.ResolveDependency<IClientNetManager>();

        Assert.DoesNotThrow(() => client.SetConnectTarget(server));
        client.Post(() => netMan.ClientConnect(null!, 0, null!));
        server.Post(() => server.CfgMan.SetCVar(CVars.NetPVS, true));

        async Task RunTicks()
        {
            for (var i = 0; i < 10; i++)
            {
                await server.WaitRunTicks(1);
                await client.WaitRunTicks(1);
            }
        }

        await RunTicks();

        EntityUid entity = default;
        EntityUid child = default;
        EntityUid overridden = default;
        NetEntity netEntity = default;
        NetEntity netChild = default;
        NetEntity netOverridden = default;
        ICommonSession session = default!;
        await server.WaitPost(() =>
        {
            var map = server.System<SharedMapSystem>().CreateMap();
            var coordinates = new EntityCoordinates(map, default);
            var player = sEntMan.SpawnEntity(null, coordinates);
            entity = sEntMan.SpawnEntity(null, coordinates);
            child = sEntMan.SpawnEntity(null, coordinates);
            overridden = sEntMan.SpawnEntity(null, coordinates);
            server.System<SharedTransformSystem>().SetParent(child, entity);
            netEntity = sEntMan.GetNetEntity(entity);
            netChild = sEntMan.GetNetEntity(child);
            netOverridden = sEntMan.GetNetEntity(overridden);

            session = server.PlayerMan.Sessions.First();
            server.PlayerMan.SetAttachedEntity(session, player);
            server.PlayerMan.JoinGame(session);
        });

        await RunTicks();

        Assert.That(cEntMan.TryGetEntity(netEntity, out var clientEntity), Is.True);
        Assert.That(cEntMan.TryGetEntity(netChild, out var clientChild), Is.True);
        Assert.That(cEntMan.TryGetEntity(netOverridden, out var clientOverridden), Is.True);
        Assert.That(client.MetaData(clientEntity!.Value).Flags.HasFlag(MetaDataFlags.Detached), Is.False);

        await server.WaitPost(() =>
        {
            var pvs = server.System<Robust.Server.GameStates.PvsOverrideSystem>();
            pvs.AddSessionOverride(overridden, session);
            pvs.ReplaceSessionCulling(session, new[] { entity, overridden });
        });
        await RunTicks();

        Assert.Multiple(() =>
        {
            Assert.That(client.MetaData(clientEntity.Value).Flags.HasFlag(MetaDataFlags.Detached), Is.True);
            Assert.That(client.MetaData(clientChild!.Value).Flags.HasFlag(MetaDataFlags.Detached), Is.True,
                "Culling a parent must cull its transform descendants.");
            Assert.That(client.MetaData(clientOverridden!.Value).Flags.HasFlag(MetaDataFlags.Detached), Is.False,
                "Explicit session overrides must take precedence over normal PVS culling.");
            Assert.That(stateMan.IsQueuedForDetach(netEntity), Is.False);
        });

        await server.WaitPost(() => server.System<Robust.Server.GameStates.PvsOverrideSystem>()
            .ClearSessionCulling(session));
        await RunTicks();

        Assert.Multiple(() =>
        {
            Assert.That(client.MetaData(clientEntity.Value).Flags.HasFlag(MetaDataFlags.Detached), Is.False);
            Assert.That(client.MetaData(clientChild!.Value).Flags.HasFlag(MetaDataFlags.Detached), Is.False);
            Assert.That(stateMan.IsQueuedForDetach(netEntity), Is.False);
        });

        await client.WaitPost(() => netMan.ClientDisconnect(""));
        await server.WaitRunTicks(5);
        await client.WaitRunTicks(5);
    }
}
