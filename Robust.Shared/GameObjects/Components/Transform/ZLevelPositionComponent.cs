// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Robust.Shared.GameStates;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Robust.Shared.GameObjects;

/// <summary>
/// ZLevel experimental additive vertical state for an entity.
/// XY and hierarchy remain owned by <see cref="TransformComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class ZLevelPositionComponent : Component
{
    /// <summary>
    /// Discrete ZLevel layer for the entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int ZLevel;

    /// <summary>
    /// Optional local offset within the current ZLevel layer.
    /// This is stored for future systems but is not yet consumed by core map queries.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float LocalZOffset;
}

/// <summary>
/// Raised after an entity's effective discrete Z-level changes.
/// </summary>
[ByRefEvent]
public readonly record struct ZLevelPositionChangedEvent(int OldZLevel, int NewZLevel);
