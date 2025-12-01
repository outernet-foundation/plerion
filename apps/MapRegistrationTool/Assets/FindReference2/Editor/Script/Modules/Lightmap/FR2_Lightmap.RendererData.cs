using System;
using UnityEngine;
namespace vietlabs.fr2
{
    internal static partial class FR2_Lightmap
    {
        [Serializable]
        private struct RendererData
        {
            public Mesh uvMesh;
            public Vector4 terrainDynamicUVST;
            public Vector4 terrainChunkDynamicUVST;
            public Vector4 lightmapST;
            public Vector4 lightmapSTDynamic;
            public ushort lightmapIndex;
            public ushort lightmapIndexDynamic;
            public Hash128 explicitProbeSetHash;
        }
    }
}
