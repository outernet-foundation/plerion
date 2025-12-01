using System;
using UnityEngine;
using Object = UnityEngine.Object;
namespace vietlabs.fr2
{
    internal static partial class FR2_Lightmap
    {
        [Serializable]
        private struct EnlightenRendererInformation
        {
            public Object renderer;
            public Vector4 dynamicLightmapSTInSystem;
            public int systemId;
            public Hash128 instanceHash;
            public Hash128 geometryHash;
        }
    }
}
