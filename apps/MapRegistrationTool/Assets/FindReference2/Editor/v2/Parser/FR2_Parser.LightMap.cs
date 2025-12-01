using UnityEditor;
using AddUsageCB = System.Action<string, long>;

namespace vietlabs.fr2
{
    internal static partial class FR2_Parser // LightMap
    {
        // BWCompatible
        internal static void LoadLightingData(this FR2_Asset asset, LightingDataAsset data)
        {
            Read_LightMap(data, (guid, fileId) => asset.AddUseGUID(guid, fileId));
        }
        
        private static void Read_LightMap(LightingDataAsset asset, AddUsageCB callback)
        {
            if (asset == null) return;
            foreach (var texture in FR2_Lightmap.Read(asset))
            {
                AddObjectUsage(texture, callback);
            }
        }
    }
}
