using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
namespace vietlabs.fr2
{
    [CreateAssetMenu]
    internal class FR2_CacheAsset : ScriptableObject
    {
        // static APIs
        private static FR2_CacheAsset _api;
        public static bool isReady => _api != null && _api.db.isReady;

        internal static void MarkAsDirty()
        {
            if (_api == null) return;
            EditorUtility.SetDirty(_api);
        }
        
        public static FR2_AssetFile GetFile(string guid) => _api.db.GetAssetByGUID(guid);
        
        public static List<FR2_IDRef> CollectUsage(string guid, List<FR2_IDRef> result = null)
        {
            if (result == null) result = new List<FR2_IDRef>();
            if (!isReady)
            {
                FR2_LOG.Log($"CacheAsset is not ready!");
                return result;
            }

            FR2_AssetFile assetFile = GetFile(guid);
            if (assetFile == null)
            {
                Debug.Log($"Asset not found in cache: {guid} : {AssetDatabase.GUIDToAssetPath(guid)}");
                return result;
            }
            
            result.AddRange(assetFile.usage);
            return result;
        }
        public static List<FR2_IDRef> CollectUsedBy(string guid, long fileId = -1, List<FR2_IDRef> result = null) // -1 = all
        {
            if (result == null) result = new List<FR2_IDRef>();
            if (!isReady)
            {
                FR2_LOG.Log($"CacheAsset is not ready!");
                return result;
            }
            
            FR2_AssetFile assetFile = _api.db.GetAssetByGUID(guid);
            if (assetFile == null)
            {
                Debug.Log($"Asset not found in cache: {guid} : {AssetDatabase.GUIDToAssetPath(guid)}");
                return result;
            }
            
            int subAssetIndex = fileId < 0 ? -1 : assetFile.Get(fileId);
            // Debug.Log($"CollectUsedBy: {guid}:{fileId} ({subAssetIndex} --> {AssetDatabase.GUIDToAssetPath(guid)} | Count = {assetFile.usedBy.Count}");
            if (fileId <= 0 || subAssetIndex <= 0)
            {
                result.AddRange(assetFile.usedBy);
            } else
            {
                result.AddRange(assetFile.usedBy.Where(a=> a.toId.SubAssetIndex == subAssetIndex));
            }
            
            return result;
        }
        
        public static (string guid, long fileId) GetGuidAndFileId(FR2_ID fr2ID)
        {
            if (!isReady) return (null, -1);
            
            FR2_AssetFile assetFile = _api.db.GetAsset(fr2ID);
            if (assetFile == null)
            {
                FR2_LOG.Log($"Asset not found in cache: {fr2ID}");
                return (null, -1);
            }

            return (assetFile.guid, assetFile.fileIds[fr2ID.SubAssetIndex]);
        }
        
        
        
        
        // Serializable
        [FormerlySerializedAs("sampleAsset")] [SerializeField] private FR2_IDRef sampleID = new FR2_IDRef();
        [SerializeField] internal FR2_AssetDB db = new FR2_AssetDB();
        [SerializeField] internal List<string> dirtyAssets = new List<string>();
        
        internal static void Init(FR2_CacheAsset cache)
        {
            _api = cache;
            _api.db.isReady = false;

            if (_api.dirtyAssets.Count > 0)
            {
                // Do check for changes
            }
            
            _api.db.BuildCache();
        }
        
        // Scan
        [ContextMenu("Scan")]
        internal void Scan()
        {
            db.isReady = false;
            db.Scan(true);
            EditorUtility.SetDirty(this);
        }
        
        [ContextMenu("BuildCache")]
        internal void BuildCache()
        {
            db.isReady = false;
            db.BuildCache();
            EditorUtility.SetDirty(this);
        }

        [ContextMenu("Start Debug")]
        internal void StartDebug()
        {
            FR2_USelection.StartDebugReference();
        }
    }
}
