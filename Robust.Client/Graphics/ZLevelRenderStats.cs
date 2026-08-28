namespace Robust.Client.Graphics;

/// <summary>
/// Per-frame Z-level rendering counters plus the current grid-layer cache size.
/// </summary>
public readonly record struct ZLevelRenderStats(
    int GridLayersDrawn,
    int GridChunksDrawn,
    int GridChunkCacheHits,
    int GridChunkCacheMisses,
    int CachedGridChunkLayers,
    int LightsRejectedByZ,
    int OccludersRejectedByZ)
{
    public int GridChunkCacheQueries => GridChunkCacheHits + GridChunkCacheMisses;

    public float GridChunkCacheHitPercent => GridChunkCacheQueries == 0
        ? 0f
        : GridChunkCacheHits * 100f / GridChunkCacheQueries;
}
