// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Robust.Shared.Map.Components;

/// <summary>
/// Defines the world-space Z origin of a grid's local vertical layers.
/// </summary>
/// <remarks>
/// Tiles and entities store grid-local Z values. Adding <see cref="Origin"/> produces the
/// comparable world Z used when separate grids overlap or dock on the same map.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ZLevelFrameComponent : Component
{
    /// <summary>
    /// World Z occupied by this grid's local layer zero.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Origin;
}

/// <summary>
/// Raised after a grid's world-space Z origin changes.
/// </summary>
[ByRefEvent]
public readonly record struct ZLevelFrameChangedEvent(EntityUid GridUid, int OldOrigin, int NewOrigin);
