using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static vietlabs.fr2.FR2_Scope;
using UnityObject = UnityEngine.Object;

namespace vietlabs.fr2
{
    internal partial class FR2_WindowAll : FR2_WindowBase, IHasCustomMenu
    {
        [SerializeField] internal PanelSettings settings = new PanelSettings();

        [MenuItem("Window/Find Reference 2")]
        private static void ShowWindow()
        {
            var _window = CreateInstance<FR2_WindowAll>();
            _window.InitIfNeeded();
            FR2_Unity.SetWindowTitle(_window, "FR2");
            _window.Show();
        }

        [NonSerialized] internal FR2_Bookmark bookmark;
        [NonSerialized] internal FR2_Selection selection;
        [NonSerialized] internal FR2_UsedInBuild UsedInBuild;
        [NonSerialized] internal FR2_DuplicateTree2 Duplicated;
        [NonSerialized] internal FR2_RefDrawer RefUnUse;
        [NonSerialized] internal FR2_MissingReference MissingReference;
        [NonSerialized] internal FR2_AssetOrganizer AssetOrganizer;
        [NonSerialized] internal FR2_DeleteEmptyFolder DeleteEmptyFolder;

        [NonSerialized] internal FR2_RefDrawer UsesDrawer; // [Selected Assets] are [USING] (depends on / contains reference to) ---> those assets
        [NonSerialized] internal FR2_RefDrawer UsedByDrawer; // [Selected Assets] are [USED BY] <---- those assets 
        [NonSerialized] internal FR2_RefDrawer SceneToAssetDrawer; // [Selected GameObjects in current Scene] are [USING] ---> those assets
        [NonSerialized] internal FR2_AddressableDrawer AddressableDrawer;


        [NonSerialized] internal FR2_RefDrawer RefInScene; // [Selected Assets] are [USED BY] <---- those components in current Scene 
        [NonSerialized] internal FR2_RefDrawer SceneUsesDrawer; // [Selected GameObjects] are [USING] ---> those components / GameObjects in current scene
        [NonSerialized] internal FR2_RefDrawer RefSceneInScene; // [Selected GameObjects] are [USED BY] <---- those components / GameObjects in current scene
        
        [NonSerialized] internal FR2_SmartLock smartLock = new FR2_SmartLock();
        [NonSerialized] private FR2_NavigationHistory navigationHistory = new FR2_NavigationHistory();

        // FR2_Theme singleton provides centralized UI constants
        [NonSerialized] internal FR2_Theme theme;

        // Simple flag to track selection sync status for UI highlighting
        [NonSerialized] internal bool isSelectionOutOfSync;
        
        
        // Cached contextual messages for drawers
        [NonSerialized] private string cachedUsesMessage;
        [NonSerialized] private string cachedUsedByMessage;
        [NonSerialized] private string cachedRefInSceneMessage;
        [NonSerialized] private string cachedSceneUsesMessage;
        [NonSerialized] private string cachedSceneToAssetMessage;
        [NonSerialized] private string cachedSceneInSceneMessage;
        [NonSerialized] private UnityObject[] lastCachedSelection;

        private string GetSceneContextInfo()
        {
#if UNITY_2021_2_OR_NEWER
            // Unity 2021.2+ moved PrefabStageUtility to UnityEditor.SceneManagement
            var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                string prefabName = System.IO.Path.GetFileNameWithoutExtension(prefabStage.assetPath);
                return $"current Prefab ({prefabName})";
            }
#elif UNITY_2018_3_OR_NEWER
            // Unity 2018.3 - 2021.1 had PrefabStageUtility in UnityEditor.Experimental.SceneManagement
            var prefabStage = UnityEditor.Experimental.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
#if UNITY_2020_1_OR_NEWER
                string prefabName = System.IO.Path.GetFileNameWithoutExtension(prefabStage.assetPath);
#else
                string prefabName = System.IO.Path.GetFileNameWithoutExtension(prefabStage.prefabAssetPath);
#endif
                return $"current Prefab ({prefabName})";
            }
#endif
            
            // Check scene count
            int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
            if (sceneCount == 0)
            {
                return "current scene";
            }
            else if (sceneCount == 1)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (scene.IsValid() && !string.IsNullOrEmpty(scene.name))
                {
                    return $"current scene ({scene.name})";
                }
                return "current scene";
            }
            else
            {
                return "current scenes"; // Multiple scenes
            }
        }

        public void Reload()
        {
            InitializeComponents();
        }

        private GUIContent GetScenePanelTitle()
        {
            string titleText = "Scene";
            string tooltip = "Scene references";

            // Check scene status for title text modifications
            if (FR2_SceneCache.Api != null)
            {
                switch (FR2_SceneCache.Api.Status)
                {
                    case SceneCacheStatus.Scanning:
                        int cur = FR2_SceneCache.Api.current;
                        int total = FR2_SceneCache.Api.total;
                        if (total > 0)
                        {
                            titleText += $" (scanning {cur}/{total})";
                            tooltip = $"Currently scanning scene objects: {cur} of {total}";
                        }
                        else
                        {
                            titleText += " (scanning...)";
                            tooltip = "Currently scanning scene objects";
                        }
                        break;
                    case SceneCacheStatus.None:
                        titleText += " (not ready)";
                        tooltip = "Scene cache is not initialized";
                        break;
                    case SceneCacheStatus.Changed:
                        tooltip = "Scene changed - results might be incomplete";
                        break;
                    case SceneCacheStatus.Ready:
                        tooltip = "Scene cache ready";
                        break;
                }
            }

            return FR2_GUIContent.From(titleText, FR2_Icon.Scene.image, tooltip);
        }

        private GUIContent GetAssetPanelTitle()
        {
            string titleText = "Assets";
            string tooltip = "Asset references";

            // Check asset status for title text modifications
            
            if (!FR2_Cache.isReady)
            {
                titleText += " (processing...)";
                tooltip = $"Processing assets: {(FR2_Cache.refreshProgress * 100):F0}%";
            }
            else if (FR2_Cache.hasDirtyAsset)
            {
                tooltip = "Assets changed - cache needs refresh";
            }
            // else if (HasUnscannedAssets())
            // {
            //     tooltip = FR2_SettingExt.isAutoRefreshEnabled 
            //         ? "Some assets not scanned yet - click on FR2 panel to trigger auto refresh"
            //         : "Some assets not scanned yet - refresh cache to scan";
            // }
            else
            {
                tooltip = "Asset cache ready";
            }
            
            return FR2_GUIContent.From(titleText, FR2_Icon.Asset.image, tooltip);
        }

        
        private bool ScenePanelHasContent()
        {
            if (!FR2_SceneCache.hasCache) return false;
            
            // Check if scene panel would have content based on current selection
            FR2_RefDrawer drawer = isFocusingUses
                ? IsSelectingAssets ? null : SceneUsesDrawer
                : IsSelectingAssets ? RefInScene : RefSceneInScene;
                
            return drawer != null && drawer.source != null && drawer.source.Length > 0;
        }
        
        private bool AssetPanelHasContent()
        {
            if (!FR2_Cache.isReady) return false;
            
            // Check if asset panel would have content based on current selection
            FR2_RefDrawer drawer = GetAssetDrawer();
            return drawer != null && drawer.source != null && drawer.source.Length > 0;
        }
        
        private bool BookmarkPanelHasContent()
        {
            return bookmark != null && FR2_Bookmark.Count > 0;
        }

        private bool IsScenePanelDirty()
        {
            if (FR2_SceneCache.Api == null) return false;
            
            // Show yellow title for various scene issues
            return FR2_SceneCache.Api.Status == SceneCacheStatus.Changed ||
                   FR2_SceneCache.Api.Status == SceneCacheStatus.None ||
                   FR2_SceneCache.Api.Status == SceneCacheStatus.Scanning;
        }

        private bool IsAssetPanelDirty()
        {
            // if (!FR2_Cache.isReady) return false;
            // Show yellow title when:
            // 1. Cache is not ready (still processing)
            // 2. Has pending changes that need refresh
            // 3. Has work in progress
            // 4. Has unscanned assets (never been processed)
            return !FR2_Cache.isReady || 
                   FR2_Cache.hasDirtyAsset; // HasUnscannedAssets()
        }

        // private bool HasUnscannedAssets()
        // {
        //     if (FR2_Cache._inst?._assets == null) return false;
        //     
        //     // Performance optimization: Use cached result if cache state hasn't changed
        //     int currentStamp = FR2_Cache._inst.timeStamp;
        //     bool isReady = FR2_Cache.isReady;
        //     
        //     if (_cachedHasUnscannedAssets.HasValue && 
        //         _lastCacheStamp == currentStamp && 
        //         isReady)
        //     {
        //         return _cachedHasUnscannedAssets.Value;
        //     }
        //     
        //     // Cache miss - recalculate only if cache is ready
        //     bool result = isReady && FR2_Cache._inst._assets.Any(asset => 
        //         asset.IsCriticalAsset() && !asset.hasBeenScanned);
        //     
        //     // Update cache
        //     _cachedHasUnscannedAssets = result;
        //     _lastCacheStamp = currentStamp;
        //     
        //     return result;
        // }
        
        // private void InvalidateUnscannedAssetsCache()
        // {
        //     _cachedHasUnscannedAssets = null;
        //     _lastCacheStamp = -1;
        // }

        private string GetSceneStatusMessage()
        {
            if (FR2_SceneCache.Api == null) return null;

            switch (FR2_SceneCache.Api.Status)
            {
                case SceneCacheStatus.Changed:
                    return "Scene changed - results might be incomplete";
                case SceneCacheStatus.Scanning:
                    return "Scanning scene objects...";
                case SceneCacheStatus.None:
                    return "Scene cache not ready";
                default:
                    return null;
            }
        }

        private string GetAssetStatusMessage()
        {
            if (FR2_Cache._inst == null) return null;

            if (FR2_Cache.hasDirtyAsset)
            {
                return "Assets changed - cache needs refresh";
            }
            else if (!FR2_Cache.isReady)
            {
                return "Asset cache not ready";
            }

            return null;
        }
        protected bool lockSelection => (selection != null) && selection.isLock;

        // Helper properties to access unified selection manager
        private bool IsSelectingAssets => selection?.isSelectingAsset ?? false;
        private bool IsSelectingSceneObjects => selection?.isSelectingSceneObject ?? false;

        private bool IsSelectionOutOfSync
        {
            get
            {
                if (selection == null) return false;
                
                var unitySelection = FR2_SelectionManager.Instance.GetUnitySelection();
                var fr2Selection = selection.GetUnityObjects();
                
                // Check count difference
                if (unitySelection.Length != fr2Selection.Length) return true;
                
                // Check content difference (order doesn't matter for warning)
                var unitySet = new HashSet<UnityObject>(unitySelection);
                var fr2Set = new HashSet<UnityObject>(fr2Selection);
                
                return !unitySet.SetEquals(fr2Set);
            }
        }
        
        private void RefreshContextualMessages()
        {
            var currentSelection = GetFR2Selection();
            
            // Only regenerate if selection actually changed
            if (AreSelectionsEqual(lastCachedSelection, currentSelection)) return;
            
            lastCachedSelection = currentSelection;
            
            cachedUsesMessage = GenerateContextualMessage(currentSelection, "USING");
            cachedUsedByMessage = GenerateContextualMessage(currentSelection, "USED BY");
            cachedRefInSceneMessage = GenerateContextualMessage(currentSelection, "USED BY", " any GameObjects in current scene");
            cachedSceneUsesMessage = GenerateContextualMessage(currentSelection, "USING", " any other objects");
            cachedSceneToAssetMessage = GenerateContextualMessage(currentSelection, "USING", " any assets");
            cachedSceneInSceneMessage = GenerateContextualMessage(currentSelection, "USED BY", " any other GameObjects");
        }

        private void ClearAllCachedUIElements()
        {
            // Clear cached contextual messages
            cachedUsesMessage = null;
            cachedUsedByMessage = null;
            cachedRefInSceneMessage = null;
            cachedSceneUsesMessage = null;
            cachedSceneToAssetMessage = null;
            cachedSceneInSceneMessage = null;
            lastCachedSelection = null;
            
            // Clear HasUnscannedAssets cache when UI elements are cleared
            // InvalidateUnscannedAssetsCache();
        }
        
        private string GenerateContextualMessage(UnityObject[] objects, string action, string suffix = " any other assets")
        {
            if (objects == null || objects.Length == 0)
                return $"Nothing selected is {action}{suffix}!";
            
            // Replace "current scene" with contextual info for scene-related messages
            if (suffix.Contains("current scene"))
            {
                suffix = suffix.Replace("current scene", GetSceneContextInfo());
            }
            
            // Check if any selected assets are ignored
            var ignoredAssets = new List<string>();
            var nonIgnoredAssets = new List<UnityObject>();
            
            foreach (var obj in objects)
            {
                if (AssetDatabase.Contains(obj))
                {
                    string assetPath = AssetDatabase.GetAssetPath(obj);
                    bool isIgnored = FR2_Setting.IgnoreAsset.Any(ignore =>
                        assetPath.Equals(ignore, StringComparison.OrdinalIgnoreCase) ||
                        assetPath.StartsWith(ignore + "/", StringComparison.OrdinalIgnoreCase));
                    
                    if (isIgnored)
                        ignoredAssets.Add(obj.name);
                    else
                        nonIgnoredAssets.Add(obj);
                }
                else
                {
                    nonIgnoredAssets.Add(obj);
                }
            }
            
            // If all selected assets are ignored, show special message
            if (ignoredAssets.Count > 0 && nonIgnoredAssets.Count == 0)
            {
                if (objects.Length == 1)
                {
                    return $"{objects[0].name} is in the ignore list and won't show references!";
                }
                else
                {
                    return $"All {objects.Length} selected assets are in the ignore list and won't show references!";
                }
            }
            
            // If some are ignored, show mixed message
            if (ignoredAssets.Count > 0)
            {
                string baseMessage = GenerateBasicMessage(nonIgnoredAssets.ToArray(), action, suffix);
                return $"{baseMessage} ({ignoredAssets.Count} ignored asset{(ignoredAssets.Count > 1 ? "s" : "")} not shown)";
            }
            
            // Normal case - no ignored assets
            return GenerateBasicMessage(objects, action, suffix);
        }
        
        private string GenerateBasicMessage(UnityObject[] objects, string action, string suffix)
        {
            if (objects == null || objects.Length == 0)
                return $"Nothing selected is {action}{suffix}!";
            
            // Check if this is a scene-related message - skip asset scan status for scene references
            bool isSceneRelated = suffix.Contains("GameObjects") || suffix.Contains("other objects");
            
            if (objects.Length == 1)
            {
                var obj = objects[0];
                if (obj == null) return "Object destroyed!";
                
                string name = obj.name;
                string typeName = GetFriendlyTypeName(obj);
                
                bool isAsset = AssetDatabase.Contains(obj);
                if (isAsset)
                {
                    string assetPath = AssetDatabase.GetAssetPath(obj);
                    if (Directory.Exists(assetPath))
                    {
                        return $"{name} does not use any other assets!";
                    }
                    
                    // Check if this asset is non-critical (ignored, packages, built-in)
                    string guid = AssetDatabase.AssetPathToGUID(assetPath);
                    var asset = FR2_Cache.GetAsset(guid);
                    
                    // bool isPackageAsset = assetPath.StartsWith("Packages/");
                    bool isIgnoredAsset = FR2_Setting.IgnoreAsset.Any(ignore =>
                        assetPath.Equals(ignore, StringComparison.OrdinalIgnoreCase) ||
                        assetPath.StartsWith(ignore + "/", StringComparison.OrdinalIgnoreCase));
                    bool isBuiltInAsset = asset != null && FR2_Asset.BUILT_IN_ASSETS.Contains(asset.guid);
                    bool isNonCritical = asset != null && !asset.IsCriticalAsset();
                    
                    // For "USING" tab - show special message for non-critical assets
                    if (action == "USING" && (isIgnoredAsset || isBuiltInAsset || isNonCritical))
                    {
                        // if (isPackageAsset)
                        //     return $"{name} usage is skipped (Package asset)";
                        if (isIgnoredAsset)
                            return $"{name} usage is skipped (Ignored asset)";
                        if (isBuiltInAsset)
                            return $"{name} usage is skipped (Built-in asset)";
                        return $"{name} usage is skipped (Non-critical asset)";
                    }
                    
                    // For "USED BY" tab - never show content scan messages, only show if truly no references
                    if (action == "USED BY")
                    {
                        return $"{name} is not {action}{suffix}!";
                    }
                    
                    // Skip asset scan status checks for scene-related messages
                    if (!isSceneRelated)
                    {
                        var assetStatus = GetAssetScanStatus(asset);
                        if (assetStatus.isNonScannable)
                        {
                            return $"{name} does not use any other assets!";
                        }
                        if (assetStatus.needsScanning)
                        {
                            return FR2_SettingExt.isAutoRefreshEnabled 
                                ? $"{name} not scanned yet - click on FR2 panel to trigger auto refresh!"
                                : $"{name} not scanned yet - hit Refresh for complete results!";
                        }
                        if (assetStatus.isDirty)
                        {
                            return FR2_SettingExt.isAutoRefreshEnabled 
                                ? $"{name} content changed - click on FR2 panel to trigger auto refresh!"
                                : $"{name} content changed - hit Refresh for complete results!";
                        }
                    }
                    
                    return $"{name} is not {action}{suffix}!";
                }
                else
                {
                    return $"{typeName} '{name}' is not {action}{suffix}!";
                }
            }
            else
            {
                string selectionSummary = GenerateSelectionSummary(objects);
                
                // For "USED BY" tab with multiple assets - never show content scan messages
                if (action == "USED BY")
                {
                    return $"{selectionSummary} are not {action}{suffix}!";
                }
                
                // Skip asset scan status checks for scene-related messages
                if (!isSceneRelated)
                {
                    var scanStatus = GetMultipleAssetsScanStatus(objects);
                    
                    if (scanStatus.allUnscanned)
                    {
                        return FR2_SettingExt.isAutoRefreshEnabled 
                            ? $"{selectionSummary} not scanned yet - click on FR2 panel to trigger auto refresh!"
                            : $"{selectionSummary} not scanned yet - hit Refresh for complete results!";
                    }
                    else if (scanStatus.allDirty)
                    {
                        return FR2_SettingExt.isAutoRefreshEnabled 
                            ? $"{selectionSummary} content changed - click on FR2 panel to trigger auto refresh!"
                            : $"{selectionSummary} content changed - hit Refresh for complete results!";
                    }
                    else if (scanStatus.hasMixed)
                    {
                        return FR2_SettingExt.isAutoRefreshEnabled 
                            ? $"{selectionSummary} need scanning - click on FR2 panel to trigger auto refresh!"
                            : $"{selectionSummary} need scanning - hit Refresh for complete results!";
                    }
                }
                
                return $"{selectionSummary} are not {action}{suffix}!";
            }
        }
        
        private string GenerateSelectionSummary(UnityObject[] objects)
        {
            var typeCounts = new Dictionary<string, int>();
            
            foreach (var obj in objects)
            {
                string typeName = GetFriendlyTypeName(obj);
                if (typeCounts.ContainsKey(typeName))
                    typeCounts[typeName]++;
                else
                    typeCounts[typeName] = 1;
            }
            
            // Sort by count (descending) then by name for consistent ordering
            var sortedTypes = typeCounts.OrderByDescending(kvp => kvp.Value)
                                       .ThenBy(kvp => kvp.Key)
                                       .ToList();
            
            if (sortedTypes.Count == 1)
            {
                var kvp = sortedTypes[0];
                return $"{kvp.Value} selected {GetPluralTypeName(kvp.Key, kvp.Value)}";
            }
            else if (sortedTypes.Count <= 3)
            {
                // Show up to 3 types: "2 Materials, 1 Texture2D, 3 GameObjects"
                var parts = sortedTypes.Select(kvp => $"{kvp.Value} {GetPluralTypeName(kvp.Key, kvp.Value)}");
                return string.Join(", ", parts);
            }
            else
            {
                // Too many types, show total count: "15 selected objects"
                return $"{objects.Length} selected objects";
            }
        }
        
        private string GetFriendlyTypeName(UnityObject obj)
        {
            if (obj == null) return "Unknown";
            
            // Special cases for better names
            var type = obj.GetType();
            string typeName = type.Name;
            
            // Handle common Unity types with better names
            switch (typeName)
            {
                case "Texture2D": return "Texture2D";
                case "Material": return "Material";
                case "AudioClip": return "Audio Clip";
                case "GameObject": return "GameObject";
                case "MonoScript": return "Script";
                case "DefaultAsset":
                    // For folders and other default assets
                    if (AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(obj)))
                        return "Folder";
                    return "Asset";
                case "Sprite": return "Sprite";
                case "Mesh": return "Mesh";
                case "Shader": return "Shader";
                case "AnimationClip": return "Animation";
                case "Cubemap": return "Cubemap";
                case "Font": return "Font";
                case "TextAsset": return "Text Asset";
                case "ScriptableObject": return "Scriptable Object";
                case "Prefab": return "Prefab";
                default:
                    // Clean up namespace if present
                    if (typeName.Contains('.'))
                        typeName = typeName.Substring(typeName.LastIndexOf('.') + 1);
                    return typeName;
            }
        }
        
        private string GetPluralTypeName(string typeName, int count)
        {
            if (count <= 1) return typeName;
            
            // Handle specific pluralization rules
            switch (typeName.ToLower())
            {
                case "gameobject": return "GameObjects";
                case "material": return "Materials";
                case "texture2d": return "Texture2Ds";
                case "audio clip": return "Audio Clips";
                case "script": return "Scripts";
                case "folder": return "Folders";
                case "sprite": return "Sprites";
                case "mesh": return "Meshes";
                case "shader": return "Shaders";
                case "animation": return "Animations";
                case "cubemap": return "Cubemaps";
                case "font": return "Fonts";
                case "text asset": return "Text Assets";
                case "scriptable object": return "Scriptable Objects";
                case "prefab": return "Prefabs";
                default:
                    // Generic pluralization - just add 's'
                    return typeName + "s";
            }
        }

        private (bool isNonScannable, bool needsScanning, bool isDirty) GetAssetScanStatus(FR2_Asset asset)
        {
            if (asset == null) return (false, false, false);
            var isNonScannable = asset.type == FR2_Asset.AssetType.DLL ||
                                 asset.type == FR2_Asset.AssetType.SCRIPT ||
                                 asset.type == FR2_Asset.AssetType.NON_READABLE ||
                                 !asset.IsCriticalAsset();

			#if FR2_DEBUG
			{
            	if (asset.isDirty) FR2_LOG.Log($"GetAssetScanStatus: {asset.assetPath} -->\n isNonScannable: {isNonScannable} | needScanning: {!asset.hasBeenScanned} | isDirty = {asset.isDirty}");
            }
            #endif
			
            return (isNonScannable, !asset.hasBeenScanned && !isNonScannable, asset.isDirty && !isNonScannable);
        }
        
        private (bool allUnscanned, bool allDirty, bool hasMixed) GetMultipleAssetsScanStatus(UnityObject[] objects)
        {
            if (!FR2_Cache.isReady || FR2_Cache._inst == null)
                return (false, false, false);
            
			bool hasUnscannedAssets = false;
			bool hasDirtyAssets = false;
			bool hasRegularAssets = false;
            
            foreach (var obj in objects)
            {
                if (!AssetDatabase.Contains(obj)) continue;
                
                string assetPath = AssetDatabase.GetAssetPath(obj);
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                var asset = FR2_Cache.GetAsset(guid, true);
                if (asset == null) continue;
				
				bool isNonScannable = asset.type == FR2_Asset.AssetType.DLL ||
								  asset.type == FR2_Asset.AssetType.SCRIPT ||
								  asset.type == FR2_Asset.AssetType.NON_READABLE ||
								  !asset.IsCriticalAsset();
				
				if (isNonScannable)
				{
					hasRegularAssets = true;
					continue;
				}
				
				bool neverScanned = !asset.hasBeenScanned;
				bool isDirty = asset.isDirty;
				
				if (neverScanned)
					hasUnscannedAssets = true;
				else if (isDirty)
					hasDirtyAssets = true;
				else
					hasRegularAssets = true;
            }
            
            bool allUnscanned = hasUnscannedAssets && !hasDirtyAssets && !hasRegularAssets;
            bool allDirty = hasDirtyAssets && !hasUnscannedAssets && !hasRegularAssets;
            bool hasMixed = (hasUnscannedAssets || hasDirtyAssets) && hasRegularAssets;
            
            return (allUnscanned, allDirty, hasMixed);
        }
        
        private static bool AreSelectionsEqual(UnityObject[] selection1, UnityObject[] selection2)
        {
            if (selection1 == null && selection2 == null) return true;
            if (selection1 == null || selection2 == null) return false;
            if (selection1.Length != selection2.Length) return false;
            
            var set1 = new HashSet<UnityObject>(selection1);
            var set2 = new HashSet<UnityObject>(selection2);
            return set1.SetEquals(set2);
        }

        private void OnEnable()
        {
            FR2_Unity.RefreshEditorStatus();
            wantsMouseMove = true;

            // Initialize theme based on current Unity skin (needed because it's NonSerialized)
            theme = EditorGUIUtility.isProSkin ? FR2_Theme.Dark : FR2_Theme.Light;

            // Initialize selection manager early
            InitializeSelectionManager();
            
            // Invalidate cache on enable to ensure fresh state
            // InvalidateUnscannedAssetsCache();

            RegisterSceneCacheCallbacks();
            FR2_Cache.onReady -= OnAssetCacheReady;
            FR2_Cache.onReady += OnAssetCacheReady;
            UpdateSceneCacheAutoRefresh();
            Repaint();
        }

        private void OnDisable()
        {
            UnregisterSceneCacheCallbacks();
            OnDisableSelectionManager();
            
            // Cleanup selection event subscription
            if (selection != null)
            {
                selection.OnSelectionChanged -= OnLocalSelectionChanged;
            }

            FR2_Cache.onReady -= OnAssetCacheReady;
        }

        private void OnFocus()
        {
            FR2_Unity.RefreshEditorStatus();
        }

        private void RegisterSceneCacheCallbacks()
        {
            FR2_SceneCache.onReady -= OnSceneCacheReady;
            FR2_SceneCache.onReady += OnSceneCacheReady;
        }

        private void UnregisterSceneCacheCallbacks()
        {
            FR2_SceneCache.onReady -= OnSceneCacheReady;
        }

        private void OnSceneCacheReady()
        {
            WillRepaint = true;

            // Clear cached UI elements when scene cache is refreshed
            ClearAllCachedUIElements();

            // Always refresh FR2 panels when scene cache finishes scanning
            // This ensures Uses/UsedBy panels show updated results for current selection
            RefreshFR2View();
            
            // If selection was out of sync due to cache not being ready, sync now
            if (isSelectionOutOfSync && selection != null && !selection.isLock)
            {
                selection.SyncFromGlobalSelection();
                RefreshFR2View();
                isSelectionOutOfSync = false;
            }
        }

        private void OnAssetCacheReady()
        {
            WillRepaint = true;
            ClearAllCachedUIElements();
            // InvalidateUnscannedAssetsCache(); // Invalidate cache when assets are processed
            RefreshFR2View();
        }


        private void UpdateSceneCacheAutoRefresh()
        {
            if (FR2_SceneCache.Api != null) FR2_SceneCache.Api.AutoRefresh = FR2_SettingExt.isAutoRefreshEnabled;
        }

        protected void InitIfNeeded()
        {
            if (UsesDrawer != null) return;
            InitializeComponents();
        }

        private bool ValidateLockedSelection()
        {
            if (!lockSelection) return true;
            
            var currentFR2Selection = GetFR2Selection();
            if (currentFR2Selection == null || currentFR2Selection.Length == 0)
            {
                UnlockAndSyncSelection();
                return false;
            }

            var validObjects = currentFR2Selection.Where(obj => obj != null).ToArray();

            if (validObjects.Length == 0)
            {
                UnlockAndSyncSelection();
                RefreshFR2View();
                return false;
            }

            if (validObjects.Length != currentFR2Selection.Length)
            {
                SetFR2Selection(validObjects);
                return true;
            }
            
            return true;
        }

        private void UnlockAndSyncSelection()
        {
            selection.isLock = false;
            selection.SyncFromGlobalSelection();
            isSelectionOutOfSync = false;
        }
        
        private bool isScenePanelVisible
        {
            get
            {
                if (isFocusingAddressable) return false;

                if (IsSelectingAssets && isFocusingUses) return false;
                if (!IsSelectingAssets && isFocusingUsedBy) return true;

                return settings.scene;
            }
        }
        
        private bool isAssetPanelVisible
        {
            get
            {
                if (isFocusingAddressable) return false;

                if (IsSelectingAssets && isFocusingUses) return true;
                if (!IsSelectingAssets && isFocusingUsedBy) return false;

                return settings.asset;
            }
        }


        [NonSerialized] public FR2_SplitView sp1; // container : Selection / sp2 / Bookmark 
        [NonSerialized] public FR2_SplitView sp2; // Scene / Assets
        
        [NonSerialized] private FR2_TabView tabs;
        [NonSerialized] private FR2_TabView toolTabs;
        [NonSerialized] private FR2_TabView bottomTabs;
        [NonSerialized] private FR2_SearchView search;

        private void DrawScene(Rect rect)
        {
            DrawScenePanel(rect);
        }
    
        private void DrawAsset(Rect rect)
        {
            DrawAssetPanel(rect);
        }

        private void DrawSearch()
        {
            if (search == null) search = new FR2_SearchView();
            search.DrawLayout();
        }

        private void DrawSelectionPanel(Rect rect)
        {
            if (selection == null) return;
            selection.Draw(rect);
        }

        private void DrawDetailsPanel(Rect rect)
        {
            var drawer = GetDetailDrawerSource();
            if (drawer != null)
            {
                drawer.DrawDetails(rect);
            }
            else
            {
                EditorGUI.HelpBox(rect, "No details available - select an item in the main panel to see details", MessageType.Info);
            }
        }

        private FR2_RefDrawer GetDetailDrawerSource()
        {
            if (isFocusingUses)
            {
                return IsSelectingAssets ? UsesDrawer : SceneToAssetDrawer;
            }
            else if (isFocusingUsedBy)
            {
                return IsSelectingAssets ? UsedByDrawer : RefSceneInScene;
            }
            return null;
        }

        protected override void OnGUI()
        {
            OnGUI2();
        }


        internal void ToggleDetailsPanel()
        {
            settings.details = !settings.details;
            if (sp1 != null && sp1.splits != null && sp1.splits.Count > 2)
            {
                sp1.SetSplitVisible(2, settings.details);
            }
            Repaint();
        }



        public bool isFocusingUses => tabs?.IsFocusing(0) ?? false;
        public bool isFocusingUsedBy => tabs?.IsFocusing(1) ?? false;
        public bool isFocusingAddressable => tabs?.IsFocusing(2) ?? false;

        // 
        public bool isFocusingDuplicate => toolTabs?.IsFocusing(0) ?? false;
        public bool isFocusingGUIDs => toolTabs?.IsFocusing(1) ?? false;
        public bool isFocusingUnused => toolTabs?.IsFocusing(2) ?? false;
        public bool isFocusingUsedInBuild => toolTabs?.IsFocusing(3) ?? false;
        public bool isFocusingOthers => toolTabs?.IsFocusing(4) ?? false;

        private static readonly HashSet<FR2_RefDrawer.Mode> allowedModes = new HashSet<FR2_RefDrawer.Mode>
        {
            FR2_RefDrawer.Mode.Type,
            FR2_RefDrawer.Mode.Extension,
            FR2_RefDrawer.Mode.Folder
        };

        private void OnTabChange()
        {
            if (deleteUnused != null) deleteUnused.hasConfirm = false;
            if (UsedInBuild != null) UsedInBuild.SetDirty();

            // Fix: Refresh panel visibility when switching between Uses/Used By tabs
            RefreshPanelVisible();
        }


        protected bool DrawFooter()
        {
            bottomTabs.DrawLayout();
            var bottomBar = GUILayoutUtility.GetLastRect();
            bottomBar = bottomBar.LPad(theme.FooterButtonsOffset); // offset for left buttons

            var (fullPathRect, flex1) = bottomBar.ExtractLeft(theme.IconButtonSize);
            var (fileSizeRect, flex2) = flex1.ExtractLeft(theme.IconButtonSize);
            var (extensionRect, flex3) = flex2.ExtractLeft(theme.IconButtonSize);
            var (buttonRect, flex4) = flex3.ExtractRight(theme.IconButtonSize);

            var viewModeRect = flex4.RPad(theme.IconButtonSize).SetWidth(theme.ViewModeSelectorWidthValue);
            viewModeRect.x = flex4.xMax - 224f;

            DrawViewModes(viewModeRect);
            DrawButton(buttonRect, ref settings.toolMode, FR2_Icon.CustomTool);
            if (DrawButton(fullPathRect, ref settings.showFullPath, FR2_Icon.FullPath)) RefreshShowFullPath();

            if (DrawButton(fileSizeRect, ref settings.showFileSize, FR2_Icon.Filesize)) RefreshShowFileSize();

            if (DrawButton(extensionRect, ref settings.showFileExtension, FR2_Icon.FileExtension))
                RefreshShowFileExtension();

            return false;
        }




        // Save status to temp variable so the result will be consistent between Layout & Repaint
        internal static int delayRepaint;
        internal static bool checkDrawImportResult;


        protected void OnGUI2()
        {
            if (Event.current.type == EventType.Layout)
            {
                FR2_Unity.RefreshEditorStatus();
                ValidateLockedSelection();
            }

            if (Event.current.type == EventType.MouseMove || Event.current.type == EventType.ScrollWheel || Event.current.type == EventType.MouseLeaveWindow || Event.current.type == EventType.MouseEnterWindow)
            {
                WillRepaint = true;
            }

            if (FR2_SettingExt.disable)
            {
                DrawEnable();
                return;
            }

            if (sp1 == null) InitPanes();
            if (FR2_SettingExt.isAutoRefreshEnabled && !FR2_SceneCache.hasInit) FR2_SceneCache.Api.RefreshCache(true);

            UpdateSceneCacheAutoRefresh();

            if (!FR2_CacheHelper.inited) FR2_CacheHelper.InitHelper();

            if (FR2_Cache._inst == null) FR2_Cache.Search4Cache();
            #if FR2_DEBUG
            GUILayout.BeginHorizontal();
            {
                EditorGUILayout.ObjectField(FR2_Cache._inst, typeof(FR2_Cache), false);
                EditorGUILayout.EnumPopup(FR2_Cache.status);
            }
            GUILayout.EndHorizontal();
            #endif
            
            var result = CheckDrawImport();
            if (Event.current.type == EventType.Layout) checkDrawImportResult = result;

            if (!checkDrawImportResult) return;

            if (settings.toolMode)
            {
                if (!FR2_SettingExt.hideToolsWarning)
                {
                    using (FR2_Scope.HzLayout())
                    {
                        EditorGUILayout.HelpBox(FR2_GUIContent.From(
                            "Tools are POWERFUL & DANGEROUS! Only use if you know what you are doing!!!",
                            FR2_Icon.Warning.image));
                        if (GUILayout.Button("  x", EditorStyles.label, theme.CloseButtonWidth, theme.WarningCloseButtonHeight))
                            FR2_SettingExt.hideToolsWarning = true;
                    }
                }

                DrawGitWarningPanel();

                toolTabs.DrawLayout();
                DrawTools();
            }
            else
            {
                DrawGitWarningPanel();

                tabs.DrawLayout();
                sp1.DrawLayout();
            }

            DrawSettings();
            DrawFooter();

            if (!WillRepaint) return;
            WillRepaint = false;
            Repaint();
        }
    }
}