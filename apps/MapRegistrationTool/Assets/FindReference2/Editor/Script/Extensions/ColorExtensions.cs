using UnityEngine;

namespace vietlabs.fr2
{
    internal static class ColorExtensions
    {
        internal static Color Alpha(this Color c, float alpha)
        {
            return new Color(c.r, c.g, c.b, alpha);
        }
    }
} 