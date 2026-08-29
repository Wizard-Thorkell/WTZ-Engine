using System.Threading.Tasks;
using NUnit.Framework;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Map.Events;
using Robust.Shared.Reflection;
using Robust.Shared.Utility;
using static Robust.UnitTesting.Shared.EntitySerialization.EntitySaveTestComponent;

namespace Robust.UnitTesting.Shared.EntitySerialization;

[TestFixture]
internal sealed partial class EntityFilterSerializationTest : RobustIntegrationTest
{
    private const string PersistentPrototype = "EntityFilterSerializationPersistent";

    private const string Prototypes = """
        - type: entity
          id: EntityFilterSerializationPersistent
          components:
          - type: EntitySaveTest
          - type: EntityFilterTest
            value: 42
        """;

    [Test]
    public async Task OperationLocalFilterExcludesSubtreesAndReferencedEntities()
    {
        var serverOptions = new ServerIntegrationOptions
        {
            Pool = false,
            ExtraPrototypes = Prototypes,
        };
        serverOptions.BeforeStart += () =>
        {
            var systemManager = IoCManager.Resolve<IEntitySystemManager>();
            systemManager.LoadExtraSystemType<SerializationEventProbeSystem>();
        };
        var server = StartServer(serverOptions);
        await server.WaitIdleAsync();

        var entMan = server.EntMan;
        var loader = server.System<MapLoaderSystem>();
        var mapSystem = server.System<SharedMapSystem>();
        var eventProbe = server.System<SerializationEventProbeSystem>();
        var filteredPath = new ResPath($"{nameof(EntityFilterSerializationTest)}_filtered.yml");
        var unfilteredPath = new ResPath($"{nameof(EntityFilterSerializationTest)}_unfiltered.yml");

        Entity<TransformComponent, EntitySaveTestComponent> map = default;
        Entity<TransformComponent, EntitySaveTestComponent> persistent = default;
        Entity<TransformComponent, EntitySaveTestComponent> transient = default;
        Entity<TransformComponent, EntitySaveTestComponent> transientChild = default;
        Entity<TransformComponent, EntitySaveTestComponent> referenced = default;

        await server.WaitPost(() =>
        {
            var mapUid = mapSystem.CreateMap(out var mapId);
            map = Get(mapUid, entMan);
            persistent = Get(entMan.SpawnEntity(PersistentPrototype, new MapCoordinates(1, 1, mapId)), entMan);
            transient = Get(entMan.SpawnEntity(null, new MapCoordinates(2, 2, mapId)), entMan);
            transientChild = Get(entMan.SpawnEntity(null,
                new EntityCoordinates(transient.Owner, 0.5f, 0.5f)), entMan);
            referenced = Get(entMan.SpawnEntity(null, MapCoordinates.Nullspace), entMan);

            map.Comp2.Id = nameof(map);
            persistent.Comp2.Id = nameof(persistent);
            transient.Comp2.Id = nameof(transient);
            transientChild.Comp2.Id = nameof(transientChild);
            referenced.Comp2.Id = nameof(referenced);
            persistent.Comp2.Entity = referenced.Owner;
        });

        var options = SerializationOptions.Default with
        {
            MissingEntityBehaviour = MissingEntityBehaviour.Ignore,
            EntityFilter = entity => entity.Owner != transient.Owner && entity.Owner != referenced.Owner,
            ComponentFilter = (_, component) => component is not EntityFilterTestComponent,
            SuppressMapSerializationEvents = true,
        };
        Assert.Multiple(() =>
        {
            Assert.That(loader.TrySaveMap(map.Owner, filteredPath, options), Is.True);
            Assert.That(loader.TrySaveMap(map.Owner, unfilteredPath), Is.True,
                "The operation-local filter must not leak into subsequent saves.");
            Assert.That(eventProbe.BeforeCount, Is.EqualTo(1),
                "Only the ordinary save should raise the before-serialization event.");
            Assert.That(eventProbe.AfterCount, Is.EqualTo(1),
                "Only the ordinary save should raise the after-serialization event.");
        });

        await server.WaitPost(() =>
        {
            mapSystem.DeleteMap(map.Comp1!.MapID);
            entMan.DeleteEntity(referenced);
        });
        Assert.That(entMan.Count<EntitySaveTestComponent>(), Is.Zero);

        var loadOptions = MapLoadOptions.Default;
        loadOptions.ExpectedCategory = FileCategory.Map;
        loadOptions.DeserializationOptions.LogInvalidEntities = false;
        LoadResult? filteredResult = null;
        await server.WaitAssertion(() =>
            Assert.That(loader.TryLoadGeneric(filteredPath, out filteredResult, loadOptions), Is.True));
        Assert.That(filteredResult, Is.Not.Null);
        var loadedFilteredResult = filteredResult!;

        Assert.Multiple(() =>
        {
            Assert.That(entMan.Count<EntitySaveTestComponent>(), Is.EqualTo(2));
            Assert.That(Find(nameof(persistent), entMan).Comp2.Entity, Is.EqualTo(EntityUid.Invalid));
            Assert.That(entMan.HasComponent<EntityFilterTestComponent>(Find(nameof(persistent), entMan)), Is.False);
            Assert.That(loadedFilteredResult.InvalidEntityReferences, Has.Count.EqualTo(1));
            Assert.That(loadedFilteredResult.InvalidEntityReferences[0].Component, Is.EqualTo("EntitySaveTest"));
            Assert.That(loadedFilteredResult.InvalidEntityReferences[0].SerializedValue, Is.EqualTo("invalid"));
        });
        await server.WaitPost(() => loader.Delete(loadedFilteredResult));

        LoadResult? unfilteredResult = null;
        await server.WaitAssertion(() =>
            Assert.That(loader.TryLoadGeneric(unfilteredPath, out unfilteredResult, loadOptions), Is.True));
        Assert.That(unfilteredResult, Is.Not.Null);
        var loadedUnfilteredResult = unfilteredResult!;

        var loadedPersistent = Find(nameof(persistent), entMan);
        var loadedTransient = Find(nameof(transient), entMan);
        var loadedTransientChild = Find(nameof(transientChild), entMan);
        var loadedReferenced = Find(nameof(referenced), entMan);
        Assert.Multiple(() =>
        {
            Assert.That(entMan.Count<EntitySaveTestComponent>(), Is.EqualTo(5));
            Assert.That(loadedPersistent.Comp2.Entity, Is.EqualTo(loadedReferenced.Owner));
            Assert.That(loadedTransientChild.Comp1.ParentUid, Is.EqualTo(loadedTransient.Owner));
            Assert.That(entMan.GetComponent<EntityFilterTestComponent>(loadedPersistent).Value, Is.EqualTo(42));
            Assert.That(loadedUnfilteredResult.InvalidEntityReferences, Is.Empty);
        });
        await server.WaitPost(() => loader.Delete(loadedUnfilteredResult));
    }

    [Reflect(false)]
    private sealed class SerializationEventProbeSystem : EntitySystem
    {
        public int BeforeCount { get; private set; }
        public int AfterCount { get; private set; }

        public override void Initialize()
        {
            SubscribeLocalEvent<BeforeSerializationEvent>(_ => BeforeCount++);
            SubscribeLocalEvent<AfterSerializationEvent>(_ => AfterCount++);
        }
    }
}
