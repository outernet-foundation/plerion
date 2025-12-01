using System;
using UnityEngine;
namespace vietlabs.fr2
{
    internal static partial class FR2_Lightmap
    {
        [Serializable]
        private struct LightmapData
        {
            [SerializeField] private Texture2D m_Lightmap;

            [SerializeField] private Texture2D m_DirLightmap;

            [SerializeField] private Texture2D m_ShadowMask;

            public Texture2D lightmap
            {
                get => m_Lightmap;
                set => m_Lightmap = value;
            }

            public Texture2D dirLightmap
            {
                get => m_DirLightmap;
                set => m_DirLightmap = value;
            }

            public Texture2D shadowMask
            {
                get => m_ShadowMask;
                set => m_ShadowMask = value;
            }
        }
    }
}
