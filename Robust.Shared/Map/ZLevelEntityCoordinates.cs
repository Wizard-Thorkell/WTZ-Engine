// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Numerics;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Utility;

namespace Robust.Shared.Map;

/// <summary>
/// Entity-local coordinates whose Z value is relative to the reference entity's inherited world layer.
/// </summary>
[PublicAPI, DataRecord]
public readonly partial record struct ZLevelEntityCoordinates(EntityUid EntityId, Vector2 Position, int Z) : ISpanFormattable
{
    public static readonly ZLevelEntityCoordinates Invalid = new(EntityUid.Invalid, Vector2.Zero, 0);

    public float X => Position.X;
    public float Y => Position.Y;

    public ZLevelEntityCoordinates(EntityUid entityId, float x, float y, int z) : this(entityId, new Vector2(x, y), z)
    {
    }

    public EntityCoordinates ToEntityCoordinates()
    {
        return new EntityCoordinates(EntityId, Position);
    }

    public ZLevelEntityCoordinates Offset(float x, float y, int zOffset = 0)
    {
        return new ZLevelEntityCoordinates(EntityId, Position + new Vector2(x, y), Z + zOffset);
    }

    public override string ToString()
    {
        return $"Entity={EntityId}, X={X:N2}, Y={Y:N2}, Z={Z}";
    }

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return ToString();
    }

    public bool TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider)
    {
        return FormatHelpers.TryFormatInto(
            destination,
            out charsWritten,
            $"Entity={EntityId}, X={X:N2}, Y={Y:N2}, Z={Z}");
    }
}
