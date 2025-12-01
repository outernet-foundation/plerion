using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace vietlabs.fr2
{
    /// <summary>
    /// Unified Selection Manager for FindReference2
    /// 
    /// ARCHITECTURE:
    /// - Main coordinator with clean separation of concerns
    /// - Instance ID caching for optimal performance
    /// - Async processing for large selections (50+ objects)
    /// - Debounced notifications to prevent spam
    /// 
    /// COMPONENTS:
    /// - FR2_SceneSelection: GameObject/Component selection (int instanceIds)
    /// - FR2_AssetSelection: Asset selection (GUID/FileID pairs)
    /// - Instance ID caches: Fast lookup for repeated selections
    /// 
    /// PERFORMANCE FEATURES:
    /// - O(1) asset vs scene object detection via caching
    /// - O(1) GUID lookups for known objects
    /// - Time-sliced processing prevents Unity freezing
    /// - Persistent caches survive selection changes
    /// 
    /// USAGE:
    /// - Subscribe to FR2_SelectionManager.SelectionChanged
    /// - Access via Instance.SceneSelection or Instance.AssetSelection
    /// - Automatic Unity version compatibility (2018.1+ vs older)
    /// </summary>
    [InitializeOnLoad]
    internal class FR2_SelectionManager
    {
        public static event System.Action SelectionChanged;
        private static FR2_SelectionManager _instance;
        
        private static readonly Dictionary<int, string> instanceIdToGuidCache = new Dictionary<int, string>();
        private static readonly HashSet<int> sceneObjectInstanceIds = new HashSet<int>();
        
        private const double DEBOUNCE_TIME = 0.05;
        private const int ASYNC_THRESHOLD = 50;
        
        private double lastSelectionTime = 0;
        private bool pendingNotification = false;
        private int processingVersion = 0;
        private UnityObject[] currentBatch = null;
        private FR2_TimeSlice timeSlicer = null;

        static FR2_SelectionManager()
        {
            Initialize();
        }

        public static FR2_SelectionManager Instance
        {
            get
            {
                if (_instance != null) return _instance;
                Initialize();
                return _instance;
            }
        }

        private FR2_SceneSelection sceneSelection;
        private FR2_AssetSelection assetSelection;
        private UnityObject[] cachedUnitySelection = Array.Empty<UnityObject>();
        public FR2_SceneSelection SceneSelection => sceneSelection;
        public FR2_AssetSelection AssetSelection => assetSelection;

        public bool IsSelectingSceneObjects => sceneSelection?.Count > 0;
        public bool IsSelectingAssets => assetSelection?.Count > 0;
        public bool HasSelection => TotalCount > 0;
        public int TotalCount => (sceneSelection?.Count ?? 0) + (assetSelection?.Count ?? 0);

        private static void Initialize()
        {
            if (_instance != null)
            {
                FR2_LOG.LogWarning("FR2_SelectionManager already initialized - cleaning up first");
                Cleanup();
            }

            _instance = new FR2_SelectionManager();
            _instance.InitializeInstance();

            // Ensure cleanup happens on domain reload
#if UNITY_2019_1_OR_NEWER
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ClearInstanceIdCaches;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ClearInstanceIdCaches;
#endif
        }

        private void InitializeInstance()
        {
            // Initialize selections
            sceneSelection = new FR2_SceneSelection();
            assetSelection = new FR2_AssetSelection();

            // Create reusable time slicer (one instance for lifetime)
            timeSlicer = new FR2_TimeSlice(
                () => currentBatch?.Length ?? 0,
                ProcessSingleObject,
                OnProcessingComplete
            );

            // Subscribe to Unity events (idempotent)
            Selection.selectionChanged -= OnUnitySelectionChanged;
            Selection.selectionChanged += OnUnitySelectionChanged;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;

            // Initialize with current selection
            RefreshFromUnitySelection();
        }

        public UnityObject[] GetUnitySelection()
        {
            return cachedUnitySelection ?? Array.Empty<UnityObject>();
        }

        private void RefreshFromUnitySelection()
        {
            var newSelection = Selection.objects ?? Array.Empty<UnityObject>();
            if (AreSelectionsEqual(cachedUnitySelection, newSelection)) return;
            
            cachedUnitySelection = newSelection;
            processingVersion++;
            timeSlicer?.Stop();
            
            sceneSelection.Clear();
            assetSelection.Clear();
            
            if (newSelection.Length == 0)
            {
                ScheduleNotification();
                return;
            }
            
            if (newSelection.Length <= ASYNC_THRESHOLD)
            {
                ProcessBatchSync(newSelection);
                ScheduleNotification();
            }
            else
            {
                currentBatch = newSelection;
                timeSlicer.Start();
            }
        }
        
        private void OnEditorUpdate()
        {
            if (!pendingNotification) return;
            
            double elapsed = EditorApplication.timeSinceStartup - lastSelectionTime;
            if (elapsed < DEBOUNCE_TIME) return;
            
            pendingNotification = false;
            SelectionChanged?.Invoke();
        }
        
        private void ScheduleNotification()
        {
            pendingNotification = true;
            lastSelectionTime = EditorApplication.timeSinceStartup;
        }

        private static bool AreSelectionsEqual(UnityObject[] selection1, UnityObject[] selection2)
        {
            if (selection1 == null && selection2 == null) return true;
            if (selection1 == null || selection2 == null) return false;
            if (selection1.Length != selection2.Length) return false;

            // Order matters for selection comparison
            for (int i = 0; i < selection1.Length; i++)
            {
                if (selection1[i] != selection2[i]) return false;
            }

            return true;
        }

        private void ProcessBatchSync(UnityObject[] batch)
        {
            for (int i = 0; i < batch.Length; i++)
            {
                AddObjectToSelection(batch[i]);
            }
        }
        
        private void ProcessSingleObject(int index)
        {
            if (currentBatch == null || index >= currentBatch.Length) return;
            AddObjectToSelection(currentBatch[index]);
        }
        private void AddObjectToSelection(UnityObject obj)
        {
            if (obj == null) return;

            int instanceId = obj.GetInstanceID();
            
            if (TryAddCachedSceneObject(obj, instanceId)) return;
            if (TryAddCachedAsset(obj, instanceId)) return;
            
            AddNewObjectToSelection(obj, instanceId);
        }
        
        private bool TryAddCachedSceneObject(UnityObject obj, int instanceId)
        {
            if (!sceneObjectInstanceIds.Contains(instanceId)) return false;
            
            AddToSceneSelection(obj);
            return true;
        }
        
        private bool TryAddCachedAsset(UnityObject obj, int instanceId)
        {
            if (!instanceIdToGuidCache.ContainsKey(instanceId)) return false;
            
            assetSelection.AddAssetCached(obj, instanceId);
            return true;
        }
        
        private void AddNewObjectToSelection(UnityObject obj, int instanceId)
        {
            if (AssetDatabase.Contains(obj))
            {
                assetSelection.AddAssetCached(obj, instanceId);
            }
            else
            {
                sceneObjectInstanceIds.Add(instanceId);
                AddToSceneSelection(obj);
            }
        }
        
        private void AddToSceneSelection(UnityObject obj)
        {
            switch (obj)
            {
                case GameObject go:
                    sceneSelection.AddGameObject(go);
                    break;
                case Component comp when comp.gameObject != null:
                    sceneSelection.AddGameObject(comp.gameObject);
                    break;
            }
        }
        
        // Called when async processing completes
        private void OnProcessingComplete()
        {
            currentBatch = null;
            ScheduleNotification();
        }

        private void OnUnitySelectionChanged()
        {
            // FR2_LOG.Log("OnUnitySelectionChanged()");
            RefreshFromUnitySelection();
        }
        
        public static void Cleanup()
        {
            if (_instance == null) return;
            
            Selection.selectionChanged -= _instance.OnUnitySelectionChanged;
            EditorApplication.update -= _instance.OnEditorUpdate;
            _instance.timeSlicer?.Stop();
            _instance = null;
        }
        
        public static void ClearInstanceIdCaches()
        {
            instanceIdToGuidCache.Clear();
            sceneObjectInstanceIds.Clear();
        }
        
        public static (int guidCacheSize, int sceneObjectCacheSize) GetCacheStats()
        {
            return (instanceIdToGuidCache.Count, sceneObjectInstanceIds.Count);
        }

        internal class FR2_SceneSelection
        {
            private readonly HashSet<int> instanceIds = new HashSet<int>();
            private readonly Dictionary<int, GameObject> gameObjects = new Dictionary<int, GameObject>();

            // Cached array - updated only when collection changes
            private GameObject[] cachedGameObjectArray = Array.Empty<GameObject>();
            private bool arrayDirty = false;

            public int Count => instanceIds.Count;
            public IReadOnlyCollection<int> InstanceIds => instanceIds;
            public IReadOnlyCollection<GameObject> GameObjects => gameObjects.Values;

            public void AddGameObject(UnityObject obj)
            {
                if (obj == null) return;

                GameObject go = obj as GameObject ?? (obj as Component)?.gameObject;
                if (go == null) return;

                int instanceId = go.GetInstanceID();
                if (instanceIds.Add(instanceId))
                {
                    gameObjects[instanceId] = go;
                    arrayDirty = true;
                }
            }

            public void Remove(int instanceId)
            {
                if (instanceIds.Remove(instanceId))
                {
                    gameObjects.Remove(instanceId);
                    arrayDirty = true;
                }
            }

            public bool Contains(int instanceId)
            {
                return instanceIds.Contains(instanceId);
            }

            public bool Contains(GameObject go)
            {
                return go != null && instanceIds.Contains(go.GetInstanceID());
            }

            public void Clear()
            {
                instanceIds.Clear();
                gameObjects.Clear();
                arrayDirty = true;
            }

            public GameObject[] ToArray()
            {
                if (arrayDirty)
                {
                    cachedGameObjectArray = gameObjects.Values.Where(go => go != null).ToArray();
                    arrayDirty = false;
                }

                return cachedGameObjectArray;
            }
        }

        internal class FR2_AssetSelection
        {
            private readonly Dictionary<string, AssetEntry> assets = new Dictionary<string, AssetEntry>();

            // Cached arrays - updated only when collection changes
            private string[] cachedGuidsArray = Array.Empty<string>();
            private bool guidArrayDirty = false;

            public int Count => assets.Count;
            public IReadOnlyCollection<string> AssetGuids => assets.Keys;
            public IReadOnlyCollection<AssetEntry> AssetEntries => assets.Values;

            public struct AssetEntry
            {
                public string guid;
                public long fileId;
                public string assetPath;

                public AssetEntry(string guid, long fileId, string assetPath)
                {
                    this.guid = guid;
                    this.fileId = fileId;
                    this.assetPath = assetPath;
                }
            }

            public void AddAsset(UnityObject obj)
            {
                if (obj == null) return;
                AddAssetCached(obj, obj.GetInstanceID());
            }
            
            // Optimized version that uses instance ID caching
            public void AddAssetCached(UnityObject obj, int instanceId)
            {
                if (obj == null) return;

                // Check if we already have GUID cached
                if (instanceIdToGuidCache.TryGetValue(instanceId, out string cachedGuid))
                {
                    if (!assets.ContainsKey(cachedGuid))
                    {
                        string cachedAssetPath = GetAssetPath(cachedGuid);
                        long cachedFileId = GetFileId(obj);
                        AddAssetEntry(cachedGuid, cachedFileId, cachedAssetPath);
                    }
                    return;
                }

                var (guid, fileId) = GetGuidAndFileId(obj);
                if (string.IsNullOrEmpty(guid)) return;
                
                instanceIdToGuidCache[instanceId] = guid;
                
                string assetPath = GetAssetPath(guid);
                AddAssetEntry(guid, fileId, assetPath);
            }
            
            private (string guid, long fileId) GetGuidAndFileId(UnityObject obj)
            {
#if UNITY_2018_1_OR_NEWER
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string resultGuid, out long resultFileId))
                {
                    return (resultGuid, resultFileId);
                }
#else
                string objAssetPath = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(objAssetPath)) return (null, -1);
                
                string objGuid = AssetDatabase.AssetPathToGUID(objAssetPath);
                if (string.IsNullOrEmpty(objGuid)) return (null, -1);
                
                long objFileId = GetFileId(obj);
                return (objGuid, objFileId);
#endif
                return (null, -1);
            }
            
            private string GetAssetPath(string guid)
            {
                // Try FR2_Cache first (faster)
                if (FR2_Cache.isReady)
                {
                    var asset = FR2_Cache.GetAsset(guid, false);
                    if (asset?.assetPath != null) return asset.assetPath;
                }
                
                // Fallback to AssetDatabase
                return AssetDatabase.GUIDToAssetPath(guid);
            }
            
            private void AddAssetEntry(string guid, long fileId, string assetPath)
            {
                if (!assets.ContainsKey(guid))
                {
                    guidArrayDirty = true;
                }

                assets[guid] = new AssetEntry(guid, fileId, assetPath);
            }

            private static long GetFileId(UnityObject obj)
            {
#if UNITY_2018_1_OR_NEWER
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out _, out long fileId))
                {
                    return fileId;
                }
#else
            try
            {
                var serializedObject = new SerializedObject(obj);
                var inspectorModeInfo = typeof(SerializedObject).GetProperty("inspectorMode", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                inspectorModeInfo?.SetValue(serializedObject, InspectorMode.Debug, null);
                
                var localIdProp = serializedObject.FindProperty("m_LocalIdentfierInFile");
                if (localIdProp != null)
                {
                    long localId = localIdProp.longValue;
                    if (localId <= 0) localId = localIdProp.intValue;
                    return localId;
                }
            }
            catch { }
#endif
                return -1;
            }

            public void Remove(string guid)
            {
                if (assets.Remove(guid))
                {
                    guidArrayDirty = true;
                }
            }

            public bool Contains(string guid)
            {
                return assets.ContainsKey(guid);
            }

            public void Clear()
            {
                assets.Clear();
                guidArrayDirty = true;
            }

            public string[] GetGuids()
            {
                if (guidArrayDirty)
                {
                    cachedGuidsArray = assets.Keys.ToArray();
                    guidArrayDirty = false;
                }

                return cachedGuidsArray;
            }

            public AssetEntry? GetAssetEntry(string guid)
            {
                if (assets.TryGetValue(guid, out var entry)) return entry;
                return null;
            }
        }
    }
}