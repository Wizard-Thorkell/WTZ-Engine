// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Numerics;
using JetBrains.Annotations;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Utility;

namespace Robust.Shared.Map;

/// <summary>
/// Map coordinates whose Z value is a discrete world layer shared by every grid on the map.
/// </summary>
[PublicAPI, DataRecord]
[Serializable, NetSerializable]
public readonly partial record struct ZLevelMapCoordinates(Vector2 Position, int Z, MapId MapId) : ISpanFormattable
{
    public static readonly ZLevelMapCoordinates Nullspace = new(Vector2.Zero, 0, MapId.Nullspace);

    public float X => Position.X;
    public float Y => Position.Y;

    public ZLevelMapCoordinates(float x, float y, int z, MapId mapId) : this(new Vector2(x, y), z, mapId)
    {
    }

    public ZLevelMapCoordinates Offset(float x, float y, int zOffset = 0)
    {
        return new ZLevelMapCoordinates(Position + new Vector2(x, y), Z + zOffset, MapId);
    }

    public override string ToString()
    {
        return $"Map={MapId}, X={X:N2}, Y={Y:N2}, Z={Z}";
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
            $"Map={MapId}, X={X:N2}, Y={Y:N2}, Z={Z}");
    }
}
