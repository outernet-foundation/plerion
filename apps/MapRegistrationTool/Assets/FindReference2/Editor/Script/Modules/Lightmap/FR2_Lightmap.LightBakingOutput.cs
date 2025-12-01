using System;
using UnityEngine;
namespace vietlabs.fr2
{
    internal static partial class FR2_Lightmap
    {
        [Serializable]
        private struct LightBakingOutput
        {
            public int serializedVersion;
            public int probeOcclusionLightIndex;
            public int occlusionMaskChannel;
            public LightmapBakeMode lightmapBakeMode;
            public bool isBaked;

            [Serializable]
            public struct LightmapBakeMode
            {
                public LightmapBakeType lightmapBakeType;
                public MixedLightingMode mixedLightingMode;
            }
        }
    }
}
