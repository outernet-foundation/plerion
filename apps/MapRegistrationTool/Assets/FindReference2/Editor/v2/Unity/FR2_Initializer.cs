using UnityEditor;
using UnityEngine;

namespace vietlabs.fr2
{
    public static class FR2_Initializer
    {
        [InitializeOnLoadMethod]
        static void Init()
        {
            EditorApplication.update -= DelayInit;
            EditorApplication.update += DelayInit;
            
            AssemblyReloadEvents.afterAssemblyReload  -= Reload;
            AssemblyReloadEvents.afterAssemblyReload  += Reload;
            
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged; 
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    break;
                case PlayModeStateChange.EnteredEditMode:  
                {
                    Reload();
                    FR2_Cache.IncrementalRefresh();
                    break;
                }
            }
        }
        
        static void Reload()
        {
            FR2_Addressable.Scan();
            FR2_Cache.Reload();
        }
        
        
        static void DelayInit()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                FR2_LOG.Log("Keep waiting...");
                return;
            }
            
            EditorApplication.update -= DelayInit;
            
            // Simple type search scoped to Assets/ only
            string[] cache = AssetDatabase.FindAssets("t:FR2_CacheAsset", new[] { "Assets" });
            if (cache.Length == 0) 
            {
                return; // No cache found
            }
            
            // Try to load the first valid cache asset
            for (int i = 0; i < cache.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(cache[i]);
                if (string.IsNullOrEmpty(assetPath)) continue;
                
                var cache0 = AssetDatabase.LoadAssetAtPath<FR2_CacheAsset>(assetPath);
                if (cache0 != null)
                {
                    FR2_CacheAsset.Init(cache0);
                    return;
                }
            }
            
            FR2_LOG.LogWarning("FR2: Cache assets found but all failed to load!");
        }
    }
}
