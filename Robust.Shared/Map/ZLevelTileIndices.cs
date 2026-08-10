// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Robust.Shared.Map;

/// <summary>
/// ZLevel experimental tile indices with a discrete vertical layer.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct ZLevelTileIndices(int X, int Y, int Z) : ISpanFormattable
{
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
        return FormatHelpers.TryFormatInto(destination, out charsWritten, $"{X},{Y},{Z}");
    }
}
