using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace vietlabs.fr2
{
    [Serializable] internal class FR2_AssetFile
    {
        [SerializeField] internal FR2_ID fr2Id;
        [SerializeField] internal string guid;
        [SerializeField] internal List<long> fileIds = new List<long> { 0 };

        // Some assets has detail, some does not
        [SerializeField] internal List<SubAssetDetail> subDetails = new List<SubAssetDetail>();

        // Cache
        [NonSerialized] internal readonly Dictionary<long, int> fileIdMap = new Dictionary<long, int>(); // map fileId => index
        [NonSerialized] internal readonly List<FR2_IDRef> usage = new List<FR2_IDRef>();
        [NonSerialized] internal readonly List<FR2_IDRef> usedBy = new List<FR2_IDRef>();
        
        public FR2_AssetFile()
        { }
        public FR2_AssetFile(string guid, int assetIndex)
        {
            this.guid = guid;
            fr2Id = new FR2_ID(assetIndex, 0, false);
        }
        
        internal int Get(long fileId)
        {
            return fileIdMap.GetValueOrDefault(fileId, -1);
        }
        internal int Add(long fileId)
        {
            int idx = fileIds.Count;
            fileIdMap.Add(fileId, idx);
            fileIds.Add(fileId);
            return idx;
        }

        internal SubAssetDetail GetSubDetail(long fileId)
        {
            return subDetails.Find(item=>item.fileId == fileId);
        }

        public void LoadAllSubAssetsIfNeeded()
        {
            if (subDetails != null && subDetails.Count > 0) return;
            if (fileIds.Count <= 1) return;
            LoadAllSubAssets();
        }
        
        private void LoadAllSubAssets()
        {
            subDetails.Clear();
            if (string.IsNullOrEmpty(guid))
            {
                FR2_LOG.LogWarning("Invalid asset GUID.");
                return;
            }

            // Convert GUID to Asset Path
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath))
            {
                FR2_LOG.LogWarning($"No sub asset found for GUID: {guid}");
                return;
            }
            
            UnityObject[] subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
            foreach (UnityObject subAsset in subAssets)
            {
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(subAsset, out _, out long localFileID))
                {
                    subDetails.Add(new SubAssetDetail(localFileID, subAsset));
                }
            }
            
            FR2_CacheAsset.MarkAsDirty();
        }
    }
}
