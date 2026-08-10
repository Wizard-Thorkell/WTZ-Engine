using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Robust.UnitTesting.Server.GameStates;

public sealed class ZLevelChunkReplicationTest : RobustIntegrationTest
{
    [Test]
    public async Task ZLevelOnlyChunkReplicatesCreationAndDeletion()
    {
        var server = StartServer();
        var client = StartClient();

        await Task.WhenAll(client.WaitIdleAsync(), server.WaitIdleAsync());

        var mapMan = server.ResolveDependency<IMapManager>();
        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sMap = sEntMan.System<SharedMapSystem>();
        var sTransform = sEntMan.System<SharedTransformSystem>();
        var confMan = server.ResolveDependency<IConfigurationManager>();
        var sPlayerMan = server.ResolveDependency<ISharedPlayerManager>();

        var cEntMan = client.ResolveDependency<IEntityManager>();
        var netMan = client.ResolveDependency<IClientNetManager>();

        Assert.DoesNotThrow(() => client.SetConnectTarget(server));
        client.Post(() => netMan.ClientConnect(null!, 0, null!));
        server.Post(() => confMan.SetCVar(CVars.NetPVS, true));

        for (var i = 0; i < 10; i++)
        {
            await server.WaitRunTicks(1);
            await client.WaitRunTicks(1);
        }

        var serverTick = (int) server.Timing.CurTick.Value;
        var clientTick = (int) client.Timing.CurTick.Value;
        var tickDelta = clientTick - serverTick;
        if (tickDelta > 1)
            await server.WaitRunTicks(tickDelta - 1);
        else if (tickDelta < 1)
            await client.WaitRunTicks(1 - tickDelta);

        EntityUid gridUid = default;
        var zTile = new ZLevelTileIndices(32, 0, 3);
        await server.WaitPost(() =>
        {
            sMap.CreateMap(out var mapId);
            var grid = mapMan.CreateGridEntity(mapId);
            gridUid = grid.Owner;

            Assert.That(sTransform.SetZLevelFrameOrigin(gridUid, 3), Is.True);
            sMap.SetTile(grid.Owner, grid.Comp, Vector2i.Zero, new Tile(1));
            sMap.SetZLevelTile(grid.Owner, grid.Comp, zTile, new Tile(2));

            var player = sEntMan.SpawnEntity(null, new EntityCoordinates(grid.Owner, 0.5f, 0.5f));
            var session = sPlayerMan.Sessions.First();
            server.PlayerMan.SetAttachedEntity(session, player);
            sPlayerMan.JoinGame(session);
        });

        for (var i = 0; i < 10; i++)
        {
            await server.WaitRunTicks(1);
            await client.WaitRunTicks(1);
        }

        var netGrid = sEntMan.GetNetEntity(gridUid);
        var cMap = client.System<Robust.Client.GameObjects.MapSystem>();
        Assert.That(cEntMan.TryGetEntity(netGrid, out var clientGridUid), Is.True);
        var clientGridEntity = clientGridUid!.Value;
        var clientGrid = cEntMan.GetComponent<MapGridComponent>(clientGridEntity);
        var chunkIndex = SharedMapSystem.GetChunkIndices(new Vector2i(zTile.X, zTile.Y), clientGrid.ChunkSize);

        Assert.Multiple(() =>
        {
            Assert.That(clientGrid.HasChunk(chunkIndex), Is.True);
            Assert.That(cMap.GetZLevelTileRef(clientGridEntity, clientGrid, zTile).Tile.TypeId, Is.EqualTo((ushort) 2));
            Assert.That(cEntMan.GetComponent<ZLevelFrameComponent>(clientGridEntity).Origin, Is.EqualTo(3));
        });

        await server.WaitPost(() => Assert.That(sTransform.SetZLevelFrameOrigin(gridUid, 6), Is.True));
        for (var i = 0; i < 5; i++)
        {
            await server.WaitRunTicks(1);
            await client.WaitRunTicks(1);
        }

        Assert.That(cEntMan.GetComponent<ZLevelFrameComponent>(clientGridEntity).Origin, Is.EqualTo(6));

        await server.WaitPost(() =>
        {
            var serverGrid = sEntMan.GetComponent<MapGridComponent>(gridUid);
            sMap.SetZLevelTile(gridUid, serverGrid, zTile, Tile.Empty);
        });

        for (var i = 0; i < 10; i++)
        {
            await server.WaitRunTicks(1);
            await client.WaitRunTicks(1);
        }

        Assert.Multiple(() =>
        {
            Assert.That(clientGrid.HasChunk(chunkIndex), Is.False);
            Assert.That(cMap.GetZLevelTileRef(clientGridEntity, clientGrid, zTile).Tile.IsEmpty, Is.True);
        });

        await client.WaitPost(() => netMan.ClientDisconnect(""));
        await server.WaitRunTicks(5);
        await client.WaitRunTicks(5);
    }
}
