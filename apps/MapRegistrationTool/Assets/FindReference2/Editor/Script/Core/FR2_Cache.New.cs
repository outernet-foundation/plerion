using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace vietlabs.fr2
{
    internal enum FR2_CacheStatus
    {
        None,
        NotExist,
        Found,
        
        Created,
        Incompatible,
        UsingCache, // not yet perform first refresh
        
        PendingChanges, // dirty, already perform first refresh
        Clean // clean, already perform first refresh
    }

    internal enum FR2_Status
    {
        None,
        Initialized,
        Search4Cache,
        
        Wait4CacheCreate, // In case cache not exist
        ValidateCache, 
        
        Wait4Refresh, // In case cache is Incompatible
        InitCacheMap, // build AssetMap from _assets
        
        RefreshDB,
        ReadAsset, // Incremental changes (asset import / moved / deleted)
        BuildUsedByMap, 
        Ready
    }

    partial class FR2_Cache
    {
        internal static bool isReady => _status == FR2_Status.Ready;
        internal static bool hasCache => _cacheStatus >= FR2_CacheStatus.Found;
        internal static bool hasDirtyAsset => _dirtyAssets.Count > 0;
        internal static FR2_Setting settings => _inst != null ? _inst._setting : null;
        public static FR2_Status status => _status;
        public static FR2_CacheStatus cacheStatus => _cacheStatus;
        
        [SerializeField] private int _timeStamp;
        
        internal static float refreshProgress
        {
            get
            {
                if (readTS == null) return 0f;
                var c = readTS.currentIndex;
                var total =  _reading.Count;
                return c / (float)total;
            }
        }
        
        internal static (int current, int total, string assetName) GetReadFileProgress()
        {
            if (readTS == null) return default;
            var c = readTS.currentIndex;
            var total =  _inst._assets.Count;

            if (c >= total)
            {
                FR2_LOG.LogWarning($"Read file progress out of range: {c}/{total}");
                c = total - 1;
            }
            
            var assetName = _inst._assets[c].assetName;
            return (c, total, assetName);
        }
        
        internal static int delayRefreshCounter;
        internal static void IncrementalRefresh()
        {
            if (_status <= FR2_Status.Wait4Refresh)
            {
                // Debug.LogWarning("Invalid status: Cache not ready!");
                return;
            }
            
            FR2_LOG.Log(nameof(IncrementalRefresh));
            delayRefreshCounter = 3;
            EditorApplication.update -= CheckDelayRefresh;
            EditorApplication.update += CheckDelayRefresh;
        }

        internal static void CheckDelayRefresh()
        {
            if (_status <= FR2_Status.Wait4Refresh)
            {
                EditorApplication.update -= CheckDelayRefresh;
                FR2_LOG.LogWarning("Invalid status: Cache not ready!");
                return;
            }
            
            if (delayRefreshCounter-- > 0) return;
            EditorApplication.update -= CheckDelayRefresh;
            ReadAssetContent();
        }
        
        internal static void MarkDirty()
        {
            if (_inst != null) EditorUtility.SetDirty(_inst);
        }

        internal static void MarkAssetPathContentDirty(params string[] assetPaths)
        {
            for (var i = 0; i < assetPaths.Length; i++)
            {
                var guid = AssetDatabase.AssetPathToGUID(assetPaths[i]);
                if (guid == _instGUID) continue;
                _dirtyAssets.Add(guid);
                
                var asset = GetAssetByGUID(guid, false);
                asset?.MarkAsDirty();
            }
        }
        
        internal static void MarkAssetPathDeleted(params string[] assetPaths)
        {
            for (var i = 0; i < assetPaths.Length; i++)
            {
                var guid = AssetDatabase.AssetPathToGUID(assetPaths[i]);
                if (guid == _instGUID) continue;
                var asset = GetAssetByGUID(guid, false);
                asset?.MarkAsDeleted();
            }
            
            MarkDirty();
        }
        
        internal static void MarkAssetPathChanged(params string[] assetPaths)
        {
            for (var i = 0; i < assetPaths.Length; i++)
            {
                var guid = AssetDatabase.AssetPathToGUID(assetPaths[i]);
                if (guid == _instGUID) continue;
                var asset = GetAssetByGUID(guid, false);
                asset?.MarkAsMoved();
            }
            
            MarkDirty();
        }
        
        
        internal static void Reload()
        {
            FR2_LOG.Log(nameof(Reload));
        }

        public static FR2_Asset GetAsset(string guid, bool autoNew = false)
        {
            if (_status != FR2_Status.Ready)
            {
                FR2_LOG.LogWarning("External call GetAsset() when cache is not Ready!");
            }
            
            return GetAssetByGUID(guid, autoNew);
        }
        
        public static void RefreshAsset(string guid, bool force)
        {
            FR2_LOG.Log($" {nameof(RefreshAsset)} : {guid}, {force}");
            MarkAssetPathContentDirty(AssetDatabase.GUIDToAssetPath(guid));
        }
        
        public static void RefreshSelection()
        {
            FR2_LOG.Log($" {nameof(RefreshSelection)}");
            MarkAssetPathContentDirty(Selection.assetGUIDs
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToArray());
        }

        public static void ClearCacheCompletely()
        {
            FR2_LOG.Log(nameof(ClearCacheCompletely));
        }

        public static void Check4Changes(bool force = false)
        {
            FR2_LOG.Log(nameof(Check4Changes));
            RefreshDB();
        }
        
        public static void DelayCheck4Changes()
        {
            FR2_LOG.Log(nameof(DelayCheck4Changes));
        }

        public static void RefreshAsset(FR2_Asset asset, bool force)
        {
            FR2_LOG.Log($" {nameof(RefreshAsset)} | {asset.assetPath}, {force}");
            // asset.MarkAsDirty(true, force);
            //
            // // If we're currently processing and this asset isn't already in the queue, add it
            // if (currentState != ProcessingState.Idle && !queueLoadContent.Contains(asset))
            // {
            //     workCount++;
            //     queueLoadContent.Add(asset);
            // }
            //
            // DelayCheck4Changes();
        }
    }
    
    
    
    internal partial class FR2_Cache : ScriptableObject
    {
        private const string CACHE_VERSION = "2.6.4";
        
        [FormerlySerializedAs("setting")] 
        [SerializeField] internal FR2_Setting _setting = new FR2_Setting();
        
        [FormerlySerializedAs("AssetList")]
        [SerializeField] internal List<FR2_Asset> _assets = new List<FR2_Asset>();
        
        [FormerlySerializedAs("_curCacheVersion")]
        [SerializeField] private string _cacheVersion;
        
        [NonSerialized] private static FR2_Status _status = FR2_Status.None;
        [NonSerialized] private static FR2_CacheStatus _cacheStatus = FR2_CacheStatus.None;
        
        [NonSerialized] internal static FR2_Cache _inst;
        [NonSerialized] internal static string _instGUID;
        [NonSerialized] internal static readonly Dictionary<string, FR2_Asset> _map = new Dictionary<string, FR2_Asset>();
        [NonSerialized] internal static readonly HashSet<string> _dirtyAssets = new HashSet<string>();
        [NonSerialized] internal static readonly HashSet<string> missingGUIDs = new HashSet<string>();
        
        private static FR2_Asset GetAssetByGUID(string guid, bool autoNew = false)
        {
            if (_instGUID == guid) return null;
            if (_map.TryGetValue(guid, out var result)) return result;
            if (missingGUIDs.Contains(guid)) return null;
            if (!autoNew) return null;
            
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath))
            {
                // FR2_LOG.Log($"Missing: {guid} ???");
                missingGUIDs.Add(guid);
                return null;
            }
            
            var asset = new FR2_Asset(guid);
            asset.LoadPathInfo();
            asset.refreshStamp = cacheStamp;
            _map.Add(guid, asset);
            if (!asset.IsCriticalAsset()) return asset;
            _inst._assets.Add(asset);
            _dirtyAssets.Add(guid);
            asset.MarkAsDirty();
            
            FR2_LOG.Log($"Add critical asset: {asset.guid} --> {assetPath}");
            // Critical assets (even if ignored) should be added to AssetList
            MarkDirty();
            
            return asset;
        }
        
        [HideInCallstack]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool MatchesStatus(string funcName, params FR2_Status[] expected)
        {
#if FR2_DEV
            {
                for (var i = 0; i < expected.Length; i++)
                {
                    if (_status != expected[i]) continue;
                    FR2_LOG.Log(funcName);
                    return true;
                }
                
                var str = string.Join(", ", expected.Select(item=>item.ToString()).ToArray());
                FR2_LOG.LogWarning($"{funcName}() : Invalid status {_status}, expected {str}");
                return false;    
            }
#else
            return true;
#endif
        }

        [HideInCallstack]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void If(bool cond, Action onTrue, Action onFalse)
        {
            if (cond)
            {
                onTrue();
            }
            else
            {
                onFalse();
            }
        }
        
        
        [InitializeOnLoadMethod]
        internal static void Initialize()
        {
            EditorApplication.delayCall += DelayInit;
        }

        static void DelayInit()
        {
            if (!MatchesStatus(nameof(Initialize), FR2_Status.None)) return;
            _status = FR2_Status.Initialized;
            Search4Cache();
        }

        internal static void Search4Cache()
        {
            if (_inst != null)
            {
                FR2_LOG.LogWarning($"Something wrong? _inst existed | status = {_status}");
            }

            { // user may delete cache manually - DO CLEAR EVERYTHING
                _dirtyAssets.Clear();
                _reading.Clear();
                _map.Clear();
                missingGUIDs.Clear();
                _instGUID = string.Empty;
                _cacheStatus = FR2_CacheStatus.None;
                _status = FR2_Status.Search4Cache;    
            }
            
            var guids = AssetDatabase.FindAssets("t:fr2_cache");
            if (guids.Length > 0)
            {
                for (var i = 0; i < guids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    _inst = AssetDatabase.LoadAssetAtPath<FR2_Cache>(path);
                    if (_inst != null) break;
                }
            }
            
            if (_inst == null) // Wait 4 Cache
            {
                _cacheStatus = FR2_CacheStatus.NotExist;
                _status = FR2_Status.Wait4CacheCreate;
                FR2_LOG.LogWarning("FR2_Cache not found - wait for create!");
                return;
            }
            
            _cacheStatus = FR2_CacheStatus.Found;
            _instGUID = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_inst));
            ValidateCacheVersion();
        }
        
        internal static void ValidateCacheVersion()
        {
            if (!MatchesStatus(nameof(ValidateCacheVersion), FR2_Status.Search4Cache)) return;
            
            _status = FR2_Status.ValidateCache;
            
            // check version   
            if (_inst._cacheVersion != CACHE_VERSION) // Wait 4 Refresh
            {
                _cacheStatus = FR2_CacheStatus.Incompatible;
                _status = FR2_Status.Wait4Refresh;
                FR2_LOG.LogWarning("Incompatible cache version - Waiting for Refresh!");
                return;
            }
            
            InitCacheMap();
        }

        internal static void CreateCache()
        {
            if (!MatchesStatus(nameof(CreateCache), FR2_Status.Wait4CacheCreate, FR2_Status.Wait4Refresh)) return;
            _inst = CreateInstance<FR2_Cache>();
            _inst._cacheVersion = CACHE_VERSION;
            AssetDatabase.CreateAsset(_inst, DEFAULT_CACHE_PATH);
            EditorUtility.SetDirty(_inst);
            
            _cacheStatus = FR2_CacheStatus.Created;
            _instGUID = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_inst));
            RefreshDB();
        }
        
        internal static void DeleteCache()
        {
            if (_inst == null) return;
            try
            {
                var path = AssetDatabase.GetAssetPath(_inst);
                _inst._assets.Clear();
                _inst = null;
                _map.Clear();

                if (readTS != null)
                {
                    readTS.Stop();
                    readTS = null;
                }
                
                AssetDatabase.DeleteAsset(path);
            }
            catch (Exception e)
            {
                FR2_LOG.Log(e);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        
        
        private static void InitCacheMap()
        {
            if (!MatchesStatus( nameof(InitCacheMap), FR2_Status.ValidateCache)) return;
            
            _status = FR2_Status.InitCacheMap;
            _cacheStatus = FR2_CacheStatus.UsingCache;
            _map.Clear();
            
            var arr = _inst._assets;
            for (var i = arr.Count-1; i >= 0; i--)
            {
                var asset = arr[i];
                if (_map.TryAdd(asset.guid, asset))
                {
                    if (asset.isDirty) _dirtyAssets.Add(asset.guid);
                    continue;
                }
                
                FR2_LOG.LogWarning($"Duplicated asset found <{asset.guid}>!");
                arr.RemoveAt(i);
            }
            
            MarkDirty();
            var refresh = autoRefresh || _inst._assets.Count == 0;
            If(refresh, RefreshDB, BuildUsedByMap);
        }
        
        private static void RefreshDB()
        {
            if (!MatchesStatus(nameof(RefreshDB)
                    , FR2_Status.Ready, FR2_Status.InitCacheMap, FR2_Status.Wait4CacheCreate)) return;
            
            _status = FR2_Status.RefreshDB;
            _cacheStatus = FR2_CacheStatus.PendingChanges;

            var ts = ++_inst._timeStamp;
            var allAssetPaths = AssetDatabase.GetAllAssetPaths();
            foreach (var path in allAssetPaths)
            {
                if (path.Contains("FindReference2") || path.Contains("FR2_Cache")) continue;
                if (AssetDatabase.IsValidFolder(path)) continue;
                if (!FR2_Parser.IsReadable(path))
                {
                    #if FR2_DEBUG
                    {
                        var guid1 = AssetDatabase.AssetPathToGUID(path);
                        var asset1 = GetAssetByGUID(guid1, true);
                        if (asset1.IsCriticalAsset()) Debug.LogWarning($"Asset isCritical() found but why it's not readable???\n{path}");
                    }
                    #endif       
                    continue;
                }
                
                var guid = AssetDatabase.AssetPathToGUID(path);
                var asset = GetAssetByGUID(guid, true);
                if (asset != null) asset.refreshStamp = ts;
            }
            
            // Mark assets as removed
            var arr = _inst._assets;
            for (var i = arr.Count-1; i >= 0; i--)
            {
                var asset = arr[i];
                if (asset.refreshStamp == ts) continue;
                asset.MarkAsDeleted();
                arr.RemoveAt(i);
                
                FR2_LOG.Log($"Asset removed at {i}? {asset.assetPath} | {asset.IsCriticalAsset()} | {asset.IsMissing}");
            }
            
            MarkDirty();
            If(hasDirtyAsset, ReadAssetContent, BuildUsedByMap);
        }
        
        private static FR2_TimeSlice readTS;
        private static int _totalRead;
        private static readonly List<FR2_Asset> _reading = new List<FR2_Asset>();
        
        internal static void ReadAssetContent()
        {
            if (!MatchesStatus(nameof(ReadAssetContent), FR2_Status.RefreshDB, FR2_Status.Ready))
            {
                FR2_LOG.LogWarning($"Refresh content is already in progress at {readTS.currentIndex}");
                return;
            }

            if (_reading.Count > 0)
            {
                FR2_LOG.Log($"Reading before: {_reading.Count}");
                for (var i = 0; i < _reading.Count; i++)
                {
                    _dirtyAssets.Add(_reading[i].guid);
                }
                
                _reading.Clear();
            }
            
            _status = FR2_Status.ReadAsset;
            _totalRead = 0;
            foreach (var guid in _dirtyAssets)
            {
                if (guid == _instGUID) continue;
                var asset = GetAssetByGUID(guid, true);
                
                if (asset == null)
                {
                    FR2_LOG.LogWarning($"Asset not found <{guid}>!");
                    continue;
                }
                
                asset.MarkAsDirty();
                _reading.Add(asset);
            }
            
            _dirtyAssets.Clear();
            readTS = new FR2_TimeSlice(()=> _reading.Count, (idx) =>
            {
                var asset = _reading[idx];
                asset.LoadFileInfo();
                
                if (!asset.isDirty) return;
                asset.LoadContentFast();
                
#if FR2_DEBUG
                if (asset.isDirty)
                {
                    FR2_LOG.LogWarning($"Dirty right after read? {asset.assetPath}\n{asset.isDirty} | {asset.fileInfoDirty} | {asset.fileContentDirty} | {asset.hasBeenScanned} | {asset.type}");
                }
#endif
                
                _totalRead++;
            },
            ReadAssetComplete);
            
            readTS.Start();
        }

        private static void ReadAssetComplete()
        {
            if (!MatchesStatus(nameof(ReadAssetComplete), FR2_Status.ReadAsset)) return;
            _reading.Clear();
            MarkDirty(); //save what we have just scan
            FR2_LOG.Log($"LastContentRead: {_totalRead}");
            
            If(autoRefresh && hasDirtyAsset, IncrementalRefresh, BuildUsedByMap);
        }
        
        private static void ClearUsedBy()
        {
            FR2_LOG.Log(nameof(ClearUsedBy));
            
            foreach (var asset in _inst._assets)
            {
                if (asset == null)
                {
                    FR2_LOG.LogWarning($"Asset is NULL???");
                    continue;
                }
                
                asset.UsedByMap?.Clear();
            }
            
            foreach (var kvp in _map)
            {
                kvp.Value.UsedByMap?.Clear();
            }
        }
        
        private static void BuildUsedByMap()
        {
            if (!MatchesStatus(nameof(BuildUsedByMap), FR2_Status.InitCacheMap, FR2_Status.ReadAsset)) return;
            
            ClearUsedBy();
            _status = FR2_Status.BuildUsedByMap;
            
            var arr = _inst._assets;
            for (var i = 0; i < arr.Count; i++)
            {
                var asset = arr[i];
                if (asset.IsMissing) 
                {
                    // FR2_LOG.Log($"Skip missing asset at {i} <{asset.guid}>");
                    continue; // do not add missing asset's references
                }
                
                foreach (var kvp in asset.UseGUIDs)
                {
                    var toGUID = kvp.Key;
                    // var toFileIds =  kvp.Value;
                    var toAsset = GetAssetByGUID(toGUID, true);
                    if (toAsset == null) continue;
                    toAsset.AddUsedBy(asset.guid, asset);
                }
            }
            
            SetAsReady();
        }

        private static void SetAsReady()
        {
            if (!MatchesStatus(nameof(SetAsReady), FR2_Status.InitCacheMap)) return;
            
            _status = FR2_Status.Ready;
            _cacheStatus = hasDirtyAsset ? FR2_CacheStatus.PendingChanges : FR2_CacheStatus.Clean;
            onReady?.Invoke();

            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            for (var i = 0; i < windows.Length; i++)
            {
                var w = windows[i];
                if (w == null) continue;
                if (w is FR2_WindowAll || w.GetType().Name.Contains("ProjectBrowser"))
                {
                    w.Repaint();
                }
            }
        }
    }
}