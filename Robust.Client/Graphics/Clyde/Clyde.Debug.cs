namespace Robust.Client.Graphics.Clyde
{
    internal sealed partial class Clyde
    {
        private readonly ClydeDebugStats _debugStats = new();

        public ZLevelRenderStats ZLevelRenderStats => new(
            _debugStats.ZLevelGridLayersDrawn,
            _debugStats.ZLevelGridChunksDrawn,
            _debugStats.ZLevelGridChunkCacheHits,
            _debugStats.ZLevelGridChunkCacheMisses,
            GetCachedZLevelGridChunkLayerCount(),
            _debugStats.ZLevelLightsRejected,
            _debugStats.ZLevelOccludersRejected);

        private int GetCachedZLevelGridChunkLayerCount()
        {
            var count = 0;
            foreach (var chunks in _mapChunkData.Values)
            {
                count += chunks.Count;
            }

            return count;
        }

        private sealed record ClydeDebugInfo(
            OpenGLVersion OpenGLVersion,
            string Renderer,
            string Vendor,
            string VersionString,
            bool Overriding,
            string WindowingApi) : IClydeDebugInfo;

        private sealed class ClydeDebugStats : IClydeDebugStats
        {
            public int LastGLDrawCalls { get; set; }
            public int LastClydeDrawCalls { get; set; }
            public int LastBatches { get; set; }
            public (int vertices, int indices) LargestBatchSize => (LargestBatchVertices, LargestBatchIndices);
            public int LargestBatchVertices { get; set; }
            public int LargestBatchIndices { get; set; }
            public int TotalLights { get; set; }
            public int ShadowLights { get; set; }
            public int Occluders { get; set; }
            public int Entities { get; set; }
            public int ZLevelGridLayersDrawn { get; set; }
            public int ZLevelGridChunksDrawn { get; set; }
            public int ZLevelGridChunkCacheHits { get; set; }
            public int ZLevelGridChunkCacheMisses { get; set; }
            public int ZLevelLightsRejected { get; set; }
            public int ZLevelOccludersRejected { get; set; }

            public void Reset()
            {
                LastGLDrawCalls = 0;
                LastClydeDrawCalls = 0;
                LastBatches = 0;
                LargestBatchVertices = 0;
                LargestBatchIndices = 0;
                TotalLights = 0;
                ShadowLights = 0;
                Occluders = 0;
                Entities = 0;
                ZLevelGridLayersDrawn = 0;
                ZLevelGridChunksDrawn = 0;
                ZLevelGridChunkCacheHits = 0;
                ZLevelGridChunkCacheMisses = 0;
                ZLevelLightsRejected = 0;
                ZLevelOccludersRejected = 0;
            }
        }
    }
}
