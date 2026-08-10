// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using Robust.Shared.Map;

namespace Robust.Shared.GameObjects;

public abstract partial class SharedTransformSystem
{
    public int GetZLevel(Entity<TransformComponent?, ZLevelPositionComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp1, logMissing: false))
            return 0;

        return TryGetInheritedZLevel((entity.Owner, entity.Comp1), out var zLevel, out _)
            ? zLevel
            : 0;
    }

    public float GetZLevelWorldHeight(Entity<TransformComponent?, ZLevelPositionComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp1, logMissing: false))
            return 0f;

        return TryGetInheritedZLevel((entity.Owner, entity.Comp1), out var zLevel, out var localOffset)
            ? zLevel + localOffset
            : 0f;
    }

    public ZLevelMapCoordinates GetZLevelMapCoordinates(Entity<TransformComponent?, ZLevelPositionComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp1, logMissing: false))
            return ZLevelMapCoordinates.Nullspace;

        var mapCoords = GetMapCoordinates((entity.Owner, entity.Comp1));
        var z = GetZLevel(entity);
        return new ZLevelMapCoordinates(mapCoords.Position, z, mapCoords.MapId);
    }

    public ZLevelMapCoordinates ToZLevelMapCoordinates(ZLevelEntityCoordinates coordinates, bool logError = true)
    {
        var mapCoords = ToMapCoordinates(coordinates.ToEntityCoordinates(), logError);

        if (mapCoords.MapId == MapId.Nullspace)
            return ZLevelMapCoordinates.Nullspace;

        var baseZ = 0;
        if (TryComp<TransformComponent>(coordinates.EntityId, out var xform))
            baseZ = GetZLevel((coordinates.EntityId, xform, CompOrNull<ZLevelPositionComponent>(coordinates.EntityId)));

        return new ZLevelMapCoordinates(mapCoords.Position, baseZ + coordinates.Z, mapCoords.MapId);
    }

    public ZLevelEntityCoordinates ToZLevelCoordinates(Entity<TransformComponent?> entity, ZLevelMapCoordinates coordinates)
    {
        var entityCoords = ToCoordinates(entity, new MapCoordinates(coordinates.Position, coordinates.MapId));
        var baseZ = Resolve(entity, ref entity.Comp, logMissing: false)
            ? GetZLevel((entity.Owner, entity.Comp, CompOrNull<ZLevelPositionComponent>(entity.Owner)))
            : 0;

        return new ZLevelEntityCoordinates(entity, entityCoords.Position, coordinates.Z - baseZ);
    }

    private bool TryGetInheritedZLevel(Entity<TransformComponent> entity, out int zLevel, out float localOffset)
    {
        zLevel = 0;
        localOffset = 0f;

        var currentUid = entity.Owner;
        var currentXform = entity.Comp;

        while (true)
        {
            if (TryComp<ZLevelPositionComponent>(currentUid, out var zComp))
            {
                zLevel = zComp.ZLevel;
                localOffset = zComp.LocalZOffset;
                return true;
            }

            var parent = currentXform.ParentUid;
            if (parent == EntityUid.Invalid || !TryComp(parent, out currentXform))
                return false;

            currentUid = parent;
        }
    }
}
