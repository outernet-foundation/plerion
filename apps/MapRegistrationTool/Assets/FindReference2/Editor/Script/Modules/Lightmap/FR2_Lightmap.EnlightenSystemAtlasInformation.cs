using System;
using UnityEngine;
namespace vietlabs.fr2
{
    internal static partial class FR2_Lightmap
    {
        [Serializable]
        private struct EnlightenSystemAtlasInformation
        {
            public int atlasSize;
            public Hash128 atlasHash;
            public int firstSystemId;
        }
    }
}
