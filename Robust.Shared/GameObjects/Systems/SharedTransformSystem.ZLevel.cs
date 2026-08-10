// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

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
            ? GetZLevelFrameOrigin((entity.Owner, entity.Comp1)) + zLevel + localOffset
            : GetZLevelFrameOrigin((entity.Owner, entity.Comp1));
    }

    /// <summary>
    /// Gets the world-space Z layer occupied by an entity's effective local layer.
    /// </summary>
    public int GetWorldZLevel(Entity<TransformComponent?, ZLevelPositionComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp1, logMissing: false))
            return 0;

        return GetZLevelFrameOrigin((entity.Owner, entity.Comp1)) + GetZLevel(entity);
    }

    /// <summary>
    /// Gets the world-space origin of the grid frame containing an entity.
    /// </summary>
    public int GetZLevelFrameOrigin(Entity<TransformComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp, logMissing: false))
            return 0;

        if (_gridQuery.HasComp(entity.Owner) && TryComp<ZLevelFrameComponent>(entity.Owner, out var ownFrame))
            return ownFrame.Origin;

        var parent = entity.Comp.ParentUid;
        while (parent != EntityUid.Invalid)
        {
            if (TryComp<ZLevelFrameComponent>(parent, out var parentFrame))
                return parentFrame.Origin;

            if (!XformQuery.TryComp(parent, out var parentTransform))
                break;

            parent = parentTransform.ParentUid;
        }

        if (entity.Comp.GridUid is { } gridUid && TryComp<ZLevelFrameComponent>(gridUid, out var gridFrame))
            return gridFrame.Origin;

        return 0;
    }

    /// <summary>
    /// Converts a grid-local Z layer into the shared world Z coordinate of its map.
    /// </summary>
    public int LocalToWorldZLevel(EntityUid gridUid, int localZ)
    {
        return localZ + (TryComp<ZLevelFrameComponent>(gridUid, out var frame) ? frame.Origin : 0);
    }

    /// <summary>
    /// Converts a shared world Z coordinate into a grid-local layer.
    /// </summary>
    public int WorldToLocalZLevel(EntityUid gridUid, int worldZ)
    {
        return worldZ - (TryComp<ZLevelFrameComponent>(gridUid, out var frame) ? frame.Origin : 0);
    }

    /// <summary>
    /// Sets a grid's world-space Z origin and refreshes contacts for bodies in that frame.
    /// </summary>
    public bool SetZLevelFrameOrigin(EntityUid gridUid, int origin, ZLevelFrameComponent? frame = null)
    {
        if (!_gridQuery.HasComp(gridUid))
            return false;

        frame ??= EnsureComp<ZLevelFrameComponent>(gridUid);
        if (frame.Origin == origin)
            return false;

        var oldOrigin = frame.Origin;
        frame.Origin = origin;
        Dirty(gridUid, frame);

        var ev = new ZLevelFrameChangedEvent(gridUid, oldOrigin, origin);
        RaiseLocalEvent(gridUid, ref ev, true);

        var query = EntityQueryEnumerator<PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var physics, out var transform))
        {
            if (uid != gridUid && transform.GridUid != gridUid)
                continue;

            _physics.RegenerateContacts((uid, physics));
        }

        return true;
    }

    public ZLevelMapCoordinates GetZLevelMapCoordinates(Entity<TransformComponent?, ZLevelPositionComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp1, logMissing: false))
            return ZLevelMapCoordinates.Nullspace;

        var mapCoords = GetMapCoordinates((entity.Owner, entity.Comp1));
        var z = GetWorldZLevel(entity);
        return new ZLevelMapCoordinates(mapCoords.Position, z, mapCoords.MapId);
    }

    public ZLevelMapCoordinates ToZLevelMapCoordinates(ZLevelEntityCoordinates coordinates, bool logError = true)
    {
        var mapCoords = ToMapCoordinates(coordinates.ToEntityCoordinates(), logError);

        if (mapCoords.MapId == MapId.Nullspace)
            return ZLevelMapCoordinates.Nullspace;

        var baseZ = 0;
        if (TryComp<TransformComponent>(coordinates.EntityId, out var xform))
            baseZ = GetWorldZLevel((coordinates.EntityId, xform, CompOrNull<ZLevelPositionComponent>(coordinates.EntityId)));

        return new ZLevelMapCoordinates(mapCoords.Position, baseZ + coordinates.Z, mapCoords.MapId);
    }

    public ZLevelEntityCoordinates ToZLevelCoordinates(Entity<TransformComponent?> entity, ZLevelMapCoordinates coordinates)
    {
        var entityCoords = ToCoordinates(entity, new MapCoordinates(coordinates.Position, coordinates.MapId));
        var baseZ = Resolve(entity, ref entity.Comp, logMissing: false)
            ? GetWorldZLevel((entity.Owner, entity.Comp, CompOrNull<ZLevelPositionComponent>(entity.Owner)))
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
