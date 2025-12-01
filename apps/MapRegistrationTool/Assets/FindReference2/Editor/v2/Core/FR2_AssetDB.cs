using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace vietlabs.fr2
{
    [Serializable] internal class FR2_AssetDB
    {
        [SerializeField] internal List<FR2_AssetFile> files = new List<FR2_AssetFile>();
        [SerializeField] internal List<FR2_IDRef> refs = new List<FR2_IDRef>();
        
        [NonSerialized] internal readonly Dictionary<string, FR2_AssetFile> guidMap = new Dictionary<string, FR2_AssetFile>();
        [NonSerialized] internal bool isReady;
        [NonSerialized] internal FR2_TimeSlice readContentTS;

        internal FR2_AssetFile GetAssetByGUID(string guid)
        {
            return guidMap.GetValueOrDefault(guid);
        }
        private FR2_AssetFile AddAsset(string guid)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath)) return null;
            var assetFile = new FR2_AssetFile(guid, files.Count);
            guidMap.Add(guid, assetFile);
            files.Add(assetFile);
            return assetFile;
        }

        internal FR2_AssetFile GetAsset(FR2_ID id)
        {
            return files[id.AssetIndex];
        }
        
        internal FR2_AssetDB Clear()
        {
            files.Clear();
            refs.Clear();
            guidMap.Clear();
            return this;
        }

        internal void Scan(bool force = false)
        {
            if (force) Clear();
            string[] allAssetPaths = AssetDatabase.GetAllAssetPaths();
            foreach (string path in allAssetPaths)
            {
                if (path.Contains("FindReference2") || path.Contains("FR2_Cache")) continue;
                
                string guid = AssetDatabase.AssetPathToGUID(path);
                // if (path.StartsWith("Packages/", StringComparison.InvariantCulture))
                // {
                //     FR2_LOG.Log($"Skip assets in Packages: {guid} --> {path}");
                //     continue;
                // }
                
                if (AssetDatabase.IsValidFolder(path))
                {
                    FR2_LOG.Log($"Skip Folder: {guid} --> {path}");
                    continue;
                }
                if (FR2_Parser.IsReadable(path)) AddAsset(guid);
            }
            
            ReadContent();
        }
        
        internal void ReadContent()
        {
            int count = files.Count;
            if (readContentTS == null) readContentTS = new FR2_TimeSlice(() => count, TS_ReadFileContent, FinishReadContent);
            readContentTS.Start();
        }

        void FinishReadContent()
        {
            // Save refs
            refs.Clear();
            guidMap.Clear();
            
            for (var i =0;i < files.Count; i++)
            {
                FR2_AssetFile assetFile = files[i];
                guidMap.Add(assetFile.guid, assetFile);
                refs.AddRange(assetFile.usage);
            }
            isReady = true;
        }

        internal void BuildCache()
        {
            guidMap.Clear();
            for (var i = 0; i < files.Count; i++)
            {
                FR2_AssetFile assetFile = files[i];
                if (assetFile == null) continue;
                
                assetFile.fileIdMap.Clear();
                foreach (long fileId in assetFile.fileIds.Distinct())
                {
                    assetFile.fileIdMap.Add(fileId, assetFile.fileIds.IndexOf(fileId));
                }
                
                guidMap.Add(assetFile.guid, assetFile);
                assetFile.usage.Clear();
                assetFile.usedBy.Clear();
            }

            for (var i = 0; i < refs.Count; i++)
            {
                FR2_IDRef r = refs[i];
                FR2_AssetFile from = GetAsset(r.fromId.WithoutSubAssetIndex());
                FR2_AssetFile to = GetAsset(r.toId.WithoutSubAssetIndex());
                
                if (from == null || to == null)
                {
                    Debug.LogWarning($"Invalid reference (asset not found???): {r.fromId} --> {r.toId}");
                    continue;
                }
                
                from.usage.Add(r);
                to.usedBy.Add(r);
            }
            
            isReady = true;
        }
        
        internal void TS_ReadFileContent(int index)
        {
            FR2_AssetFile sourceFile = files[index];
            string assetPath = AssetDatabase.GUIDToAssetPath(sourceFile.guid);
            if (string.IsNullOrEmpty(assetPath)) return;

            var usage = new HashSet<FR2_ID>();
            FR2_Parser.ReadContent(assetPath, (guid, fileId) =>
            {
                if (guid == sourceFile.guid) return; // Skip self reference
                FR2_AssetFile destFile = GetAssetByGUID(guid) ?? AddAsset(guid);
                if (destFile == null) return; // Invalid or missing GUID???

                int subAssetIndex = destFile.Get(fileId);
                if (subAssetIndex == -1) subAssetIndex = destFile.Add(fileId);
                
                FR2_ID toFR2Id = destFile.fr2Id.WithSubAssetIndex(subAssetIndex);
                if (!usage.Add(toFR2Id)) return; // Already added

                var r = new FR2_IDRef() { fromId = sourceFile.fr2Id, toId = toFR2Id };
                sourceFile.usage.Add(r);
                destFile.usedBy.Add(r);
            });
        }
    }
}
