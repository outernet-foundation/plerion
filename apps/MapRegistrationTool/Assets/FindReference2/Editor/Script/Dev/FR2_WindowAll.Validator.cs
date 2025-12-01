using UnityEditor;
using UnityEngine;

namespace vietlabs.fr2
{
    internal partial class FR2_WindowAll
    {
        public override void AddToCustomMenu(GenericMenu menu)
        {
#if FR2_DEV
            menu.AddItem(new GUIContent("Refresh Cache"), false, () => FR2_Cache.Check4Changes(true));
            menu.AddItem(new GUIContent("Validate References vs Unity"), false, ()=>ValidateReferencesVsUnity());
            menu.AddItem(new GUIContent("Validate References (Export to File)"), false, () => ValidateReferencesVsUnity(true));
            menu.AddItem(new GUIContent("Debug Selected Assets"), false, DebugSelectedAssets);
#endif
        }

#if FR2_DEV
        private void ValidateReferencesVsUnity(bool exportToFile = false)
        {
            if (!FR2_Cache.isReady)
            {
                FR2_LOG.LogWarning("[FR2_VALIDATION] Cache not ready. Please wait for cache to finish loading.");
                return;
            }

            if (exportToFile)
            {
                FR2_LOG.Log("[FR2_VALIDATION] Starting validation with file export...");
            }
            else
            {
                FR2_LOG.Log("[FR2_VALIDATION] Starting comprehensive reference validation against Unity's GetDependencies...");
            }
            
            var validator = new FR2_ReferenceValidator();
            validator.ValidateAllReferences(exportToFile);
        }

        private void DebugSelectedAssets()
        {
            if (Selection.objects == null || Selection.objects.Length == 0)
            {
                FR2_LOG.LogWarning("[FR2_DEBUG] No objects selected for debugging");
                return;
            }

            foreach (var obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;

                string guid = AssetDatabase.AssetPathToGUID(path);
                Debug.Log($"[FR2_DEBUG] === {obj.name} ({guid}) ===");

                if (!FR2_Cache.isReady)
                {
                    FR2_LOG.LogWarning("[FR2_DEBUG] Cache not ready!");
                    continue;
                }

                FR2_Asset asset = FR2_Cache.GetAsset(guid);
                if (asset == null)
                {
                    FR2_LOG.LogWarning("[FR2_DEBUG] Asset not found in cache!");
                    continue;
                }

                Debug.Log($"Type: {asset.type} | Critical: {asset.IsCriticalAsset()} | Extension: {asset.extension}");
                FR2_LOG.Log($"Uses: {asset.UseGUIDs?.Count ?? 0} | UsedBy: {asset.UsedByMap?.Count ?? 0}");
                Debug.Log($"InAssetList: {FR2_Cache._inst.AssetList?.Contains(asset) ?? false} | Dirty: {asset.isDirty}");
            }
        }
#endif
    }
}