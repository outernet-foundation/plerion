using System;
using UnityEngine;
namespace vietlabs.fr2
{
    internal static partial class FR2_Lightmap
    {
        [Serializable]
        private struct EnlightenSystemInformation
        {
            public int rendererIndex;
            public int rendererSize;
            public int atlasIndex;
            public int atlasOffsetX;
            public int atlasOffsetY;
            public Hash128 inputSystemHash;
            public Hash128 radiositySystemHash;
        }
    }
}
