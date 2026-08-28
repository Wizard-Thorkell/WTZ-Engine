using System;
using System.Numerics;
using OpenToolkit.Graphics.OpenGL4;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Robust.Client.Graphics.Clyde;

internal partial class Clyde
{
    public LightShadowMapRenderStats RenderLightShadowMap(
        IRenderTexture shadowMap,
        IClydeViewport viewport,
        MapId mapId,
        ReadOnlySpan<LightShadowMapRequest> requests)
    {
        var stats = LightShadowMapContract.Validate(shadowMap.Size, requests);
        if (requests.IsEmpty || mapId == MapId.Nullspace || viewport.Eye is not { } eye)
            return default;

        if (shadowMap is not RenderTexture renderTexture)
            throw new ArgumentException("The point-light shadow atlas must be owned by this Clyde instance.", nameof(shadowMap));

        var loadedTarget = RtToLoaded(renderTexture);
        if (loadedTarget.DepthStencilHandle == default)
            throw new ArgumentException("The point-light shadow atlas requires a depth buffer.", nameof(shadowMap));

        FlushRenderQueue();
        var restoreGeometry = _occlusionGeometryState;
        var state = PushRenderStateFull();
        var depthPrepared = false;

        try
        {
            PrepareDepthDraw(loadedTarget);
            depthPrepared = true;
            GL.CullFace(CullFaceMode.Back);
            CheckGlError();

            eye.GetViewMatrixNoOffset(out var eyeTransform, eye.Scale);
            var first = 0;
            while (first < requests.Length)
            {
                var worldZ = requests[first].WorldZ;
                var end = first + 1;
                while (end < requests.Length && requests[end].WorldZ == worldZ)
                    end++;

                var bounds = GetRequestBounds(requests[first]);
                for (var i = first + 1; i < end; i++)
                    bounds = bounds.Union(GetRequestBounds(requests[i]));

                UpdateOcclusionGeometry(mapId, bounds, eyeTransform, worldZ);
                BindVertexArray(_occlusionVao.Handle);
                CheckGlError();

                for (var i = first; i < end; i++)
                {
                    ref readonly var request = ref requests[i];
                    DrawOcclusionDepth(request.WorldPosition, LightShadowMap.Width, request.Radius, i);
                }

                first = end;
            }

            _debugStats.ShadowLights += requests.Length;
            return stats;
        }
        finally
        {
            try
            {
                if (depthPrepared)
                    FinalizeDepthDraw();

                if (restoreGeometry is { } restore)
                {
                    // Wall bleed runs after content overlays and still consumes the active-floor mask geometry.
                    UpdateOcclusionGeometry(
                        restore.MapId,
                        restore.WorldBounds,
                        restore.EyeTransform,
                        restore.WorldZ);
                }
            }
            finally
            {
                PopRenderStateFull(state);
            }
        }
    }

    private static Box2 GetRequestBounds(in LightShadowMapRequest request)
    {
        var radius = new Vector2(request.Radius);
        return new Box2(request.WorldPosition - radius, request.WorldPosition + radius);
    }
}
