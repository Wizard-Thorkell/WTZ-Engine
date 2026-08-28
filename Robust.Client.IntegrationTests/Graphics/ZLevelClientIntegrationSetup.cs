using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using static Robust.UnitTesting.RobustIntegrationTest;

namespace Robust.UnitTesting.Client.Graphics;

internal static class ZLevelClientIntegrationSetup
{
    public static ClientIntegrationOptions Create()
    {
        return new ClientIntegrationOptions
        {
            Pool = false,
        };
    }

    public static void StartEntitySystems(IEntityManager entities, IMapManager maps)
    {
        entities.Startup();
        maps.Startup();
    }
}
