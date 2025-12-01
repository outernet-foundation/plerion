using System;
namespace vietlabs.fr2
{
    internal static partial class FR2_Lightmap
    {
        [Serializable]
        private struct EnlightenTerrainChunksInformation
        {
            public int firstSystemId;
            public int numChunksInX;
            public int numChunksInY;
        }
    }
}
