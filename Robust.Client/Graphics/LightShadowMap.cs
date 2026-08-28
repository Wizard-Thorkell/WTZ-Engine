using System;
using System.Numerics;
using Robust.Shared.Maths;

namespace Robust.Client.Graphics;

/// <summary>
/// Contract for Clyde's polar point-light shadow maps.
/// </summary>
public static class LightShadowMap
{
    public const int Width = 512;
}

/// <summary>
/// One externally submitted point light and the world Z whose occluders cast its shadows.
/// Requests retain their input order, which is also their row order in the resulting atlas.
/// </summary>
public readonly record struct LightShadowMapRequest(
    Vector2 WorldPosition,
    float Radius,
    int WorldZ);

/// <summary>
/// Work accepted by one external shadow-atlas render.
/// </summary>
public readonly record struct LightShadowMapRenderStats(
    int Lights,
    int FloorGroups);

internal static class LightShadowMapContract
{
    public static LightShadowMapRenderStats Validate(
        Vector2i targetSize,
        ReadOnlySpan<LightShadowMapRequest> requests)
    {
        if (targetSize.X != LightShadowMap.Width)
        {
            throw new ArgumentException(
                $"A point-light shadow atlas must be {LightShadowMap.Width} pixels wide.",
                nameof(targetSize));
        }

        if (targetSize.Y < requests.Length)
        {
            throw new ArgumentException(
                "The point-light shadow atlas does not have enough rows for every request.",
                nameof(targetSize));
        }

        var floorGroups = 0;
        var previousWorldZ = 0;
        for (var i = 0; i < requests.Length; i++)
        {
            ref readonly var request = ref requests[i];
            if (!float.IsFinite(request.WorldPosition.X) ||
                !float.IsFinite(request.WorldPosition.Y))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requests),
                    "Point-light shadow positions must be finite.");
            }

            if (!float.IsFinite(request.Radius) || request.Radius <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requests),
                    "Point-light shadow radii must be finite and positive.");
            }

            if (i == 0 || request.WorldZ != previousWorldZ)
                floorGroups++;

            previousWorldZ = request.WorldZ;
        }

        return new LightShadowMapRenderStats(requests.Length, floorGroups);
    }
}
