using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace vietlabs.fr2
{
    public enum Dependency
    {
        All,
        Direct,
        Indirect
    }

    public enum DepthFilter
    {
        All,
        Equal,
        NotEqual,
        Less,
        LessEqual,
        Greater,
        GreaterEqual
    }

    public enum Sorting
    {
        None,
        Type,
        Path,
        Size
    }

    [Serializable]
    public class FR2AssetInfo
    {
        public string guid;
        public string assetPath;
        public string fileName;
        public string extension;
        public System.Type assetType;
        public int usageCount;
        public int usedByCount;
        public bool isInBuild;
        public long fileSize;
        public bool isFolder;
        public bool isBuiltin;

        public FR2AssetInfo(string guid)
        {
            this.guid = guid;
            RefreshInfo();
        }

        internal void RefreshInfo()
        {
            if (string.IsNullOrEmpty(guid)) return;

            assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath))
            {
                fileName = "Missing";
                extension = "";
                assetType = null;
                fileSize = 0;
                isFolder = false;
                isBuiltin = false;
                return;
            }

            fileName = Path.GetFileName(assetPath);
            extension = Path.GetExtension(assetPath);
            
            // Check if it's a folder
            isFolder = AssetDatabase.IsValidFolder(assetPath);
            
            // Check if it's builtin using the proper constant
            isBuiltin = FR2_Asset.BUILT_IN_ASSETS.Contains(guid);

            if (!isFolder)
            {
                UnityObject obj = AssetDatabase.LoadAssetAtPath<UnityObject>(assetPath);
                assetType = obj?.GetType();

                // Get file size
                try
                {
                    if (File.Exists(assetPath))
                    {
                        var fileInfo = new FileInfo(assetPath);
                        fileSize = fileInfo.Length;
                    }
                }
                catch
                {
                    fileSize = 0;
                }
            }
            else
            {
                assetType = typeof(DefaultAsset);
                fileSize = 0;
            }

            // Get usage counts from FR2_Asset if available
            if (FR2_Cache.isReady)
            {
                FR2_Asset asset = FR2_Cache.GetAsset(guid);
                if (asset != null)
                {
                    usageCount = asset.UseGUIDsCount;
                    usedByCount = asset.UsedByMap.Count;
                }
            }
        }

        internal void UpdateBuildStatus(HashSet<string> buildGuids)
        {
            isInBuild = buildGuids != null && buildGuids.Contains(guid);
        }
    }

    public static class FR2
    {
        public static bool IsReady => FR2_Cache.isReady;

        public static void ScanProject()
        {
            FR2_Cache.DeleteCache();
            FR2_Cache.CreateCache();
        }

        public static void Refresh()
        {
            if (!FR2_Cache.hasCache)
            {
                FR2_LOG.LogWarning("FR2 cache not found. Use FR2.ScanProject() first.");
                return;
            }

            FR2_Cache.Check4Changes(true);
        }

        public static List<FR2AssetInfo> GetUses(string[] guids, Dependency dep = Dependency.All, int depth = 0, DepthFilter filter = DepthFilter.All, Sorting sort = Sorting.None)
        {
            if (!IsReady)
            {
                FR2_LOG.LogWarning("FR2 cache not ready. Use FR2.ScanProject() first.");
                return new List<FR2AssetInfo>();
            }

            if (guids == null || guids.Length == 0) return new List<FR2AssetInfo>();

            Dictionary<string, FR2_Ref> refs = FR2_Ref.FindUsage(guids);
            return ProcessResults(refs, dep, depth, filter, sort);
        }

        public static List<FR2AssetInfo> GetUsedBy(string[] guids, Dependency dep = Dependency.All, int depth = 0, DepthFilter filter = DepthFilter.All, Sorting sort = Sorting.None)
        {
            if (!IsReady)
            {
                FR2_LOG.LogWarning("FR2 cache not ready. Use FR2.ScanProject() first.");
                return new List<FR2AssetInfo>();
            }

            if (guids == null || guids.Length == 0) return new List<FR2AssetInfo>();

            Dictionary<string, FR2_Ref> refs = FR2_Ref.FindUsedBy(guids);
            return ProcessResults(refs, dep, depth, filter, sort);
        }

        public static List<FR2AssetInfo> GetUnused(Sorting sort = Sorting.None)
        {
            if (!IsReady)
            {
                FR2_LOG.LogWarning("FR2 cache not ready. Use FR2.ScanProject() first.");
                return new List<FR2AssetInfo>();
            }

            List<FR2_Asset> unusedAssets = FR2_Cache._inst.ScanUnused(true);
            return ProcessAssetResults(unusedAssets, sort);
        }

        public static List<FR2AssetInfo> GetInBuild(Sorting sort = Sorting.None)
        {
            if (!IsReady)
            {
                FR2_LOG.LogWarning("FR2 cache not ready. Use FR2.ScanProject() first.");
                return new List<FR2AssetInfo>();
            }

            var usedInBuild = new FR2_UsedInBuild(null, () => ConvertSorting(sort), () => FR2_RefDrawer.Mode.Type);
            usedInBuild.RefreshView();

            if (usedInBuild.refs == null) return new List<FR2AssetInfo>();

            var buildGuids = new HashSet<string>(usedInBuild.refs.Keys);
            var assets = usedInBuild.refs.Values.Select(r => r.asset).ToList();
            var results = ProcessAssetResults(assets, sort);
            
            // Set isInBuild flag for all returned assets
            foreach (var assetInfo in results)
            {
                assetInfo.UpdateBuildStatus(buildGuids);
            }
            
            return results;
        }

        public static Dictionary<string, int> GetUsesCount(string[] guids)
        {
            var result = new Dictionary<string, int>();
            
            if (!IsReady)
            {
                FR2_LOG.LogWarning("FR2 cache not ready. Use FR2.ScanProject() first.");
                return result;
            }

            if (guids == null || guids.Length == 0) return result;

            foreach (string guid in guids)
            {
                if (string.IsNullOrEmpty(guid)) continue;
                
                FR2_Asset asset = FR2_Cache.GetAsset(guid);
                result[guid] = asset?.UseGUIDsCount ?? 0;
            }

            return result;
        }

        public static Dictionary<string, int> GetUsedByCount(string[] guids)
        {
            var result = new Dictionary<string, int>();
            
            if (!IsReady)
            {
                FR2_LOG.LogWarning("FR2 cache not ready. Use FR2.ScanProject() first.");
                return result;
            }

            if (guids == null || guids.Length == 0) return result;

            foreach (string guid in guids)
            {
                if (string.IsNullOrEmpty(guid)) continue;
                
                FR2_Asset asset = FR2_Cache.GetAsset(guid);
                result[guid] = asset?.UsedByMap.Count ?? 0;
            }

            return result;
        }

        public static bool IsUses(string[] guids)
        {
            if (!IsReady || guids == null || guids.Length == 0) return false;

            foreach (string guid in guids)
            {
                if (string.IsNullOrEmpty(guid)) continue;
                
                FR2_Asset asset = FR2_Cache.GetAsset(guid);
                if (asset != null && asset.UseGUIDsCount > 0) return true;
            }

            return false;
        }

        public static bool IsUsedBy(string[] guids)
        {
            if (!IsReady || guids == null || guids.Length == 0) return false;

            foreach (string guid in guids)
            {
                if (string.IsNullOrEmpty(guid)) continue;
                
                FR2_Asset asset = FR2_Cache.GetAsset(guid);
                if (asset != null && asset.UsedByMap.Count > 0) return true;
            }

            return false;
        }

        public static bool IsInBuild(string[] guids)
        {
            if (!IsReady)
            {
                FR2_LOG.LogWarning("FR2 cache not ready. Use FR2.ScanProject() first.");
                return false;
            }

            if (guids == null || guids.Length == 0) return false;

            var usedInBuild = new FR2_UsedInBuild(null, () => FR2_RefDrawer.Sort.Type, () => FR2_RefDrawer.Mode.Type);
            usedInBuild.RefreshView();

            if (usedInBuild.refs == null) return false;

            var buildGuids = new HashSet<string>(usedInBuild.refs.Keys);

            foreach (string guid in guids)
            {
                if (buildGuids.Contains(guid)) return true;
            }

            return false;
        }



        private static List<FR2AssetInfo> ProcessAssetResults(List<FR2_Asset> assets, Sorting sorting)
        {
            if (assets == null) return new List<FR2AssetInfo>();

            var results = new List<FR2AssetInfo>();

            foreach (FR2_Asset asset in assets)
            {
                if (asset == null) continue;
                
                var assetInfo = new FR2AssetInfo(asset.guid);
                results.Add(assetInfo);
            }

            return ApplySorting(results, sorting);
        }

        private static List<FR2AssetInfo> ProcessResults(Dictionary<string, FR2_Ref> refs, Dependency dependency, int depth, DepthFilter filter, Sorting sorting)
        {
            if (refs == null) return new List<FR2AssetInfo>();

            var results = new List<FR2AssetInfo>();
            var processedGuids = new HashSet<string>();

            foreach (KeyValuePair<string, FR2_Ref> kvp in refs)
            {
                FR2_Ref refItem = kvp.Value;
                
                // Apply dependency filter
                if (dependency == Dependency.Direct && refItem.depth > 1) continue;
                if (dependency == Dependency.Indirect && refItem.depth <= 1) continue;
                
                // Apply depth filter with mathematically correct comparisons
                if (!MatchesDepthFilter(refItem.depth, depth, filter)) continue;

                if (processedGuids.Contains(refItem.asset.guid)) continue;
                processedGuids.Add(refItem.asset.guid);

                var assetInfo = new FR2AssetInfo(refItem.asset.guid);
                results.Add(assetInfo);
            }

            return ApplySorting(results, sorting);
        }



        private static bool MatchesDepthFilter(int itemDepth, int targetDepth, DepthFilter filter)
        {
            switch (filter)
            {
                case DepthFilter.All:
                    return true;
                case DepthFilter.Equal:
                    return itemDepth == targetDepth;
                case DepthFilter.NotEqual:
                    return itemDepth != targetDepth;
                case DepthFilter.Less:
                    return itemDepth < targetDepth;
                case DepthFilter.LessEqual:
                    return itemDepth <= targetDepth;
                case DepthFilter.Greater:
                    return itemDepth > targetDepth;
                case DepthFilter.GreaterEqual:
                    return itemDepth >= targetDepth;
                default:
                    return true;
            }
        }



        private static List<FR2AssetInfo> ApplySorting(List<FR2AssetInfo> assetInfos, Sorting sorting)
        {
            switch (sorting)
            {
                case Sorting.Type:
                    return assetInfos.OrderBy(info => info.assetType?.Name ?? "")
                                    .ThenBy(info => info.assetPath)
                                    .ToList();
                
                case Sorting.Path:
                    return assetInfos.OrderBy(info => info.assetPath)
                                    .ThenBy(info => info.assetType?.Name ?? "")
                                    .ToList();
                
                case Sorting.Size:
                    return assetInfos.OrderByDescending(info => info.fileSize)
                                    .ThenBy(info => info.assetPath)
                                    .ToList();
                
                default:
                    return assetInfos;
            }
        }

        private static FR2_RefDrawer.Sort ConvertSorting(Sorting sorting)
        {
            switch (sorting)
            {
                case Sorting.Type: return FR2_RefDrawer.Sort.Type;
                case Sorting.Path: return FR2_RefDrawer.Sort.Path;
                case Sorting.Size: return FR2_RefDrawer.Sort.Size;
                default: return FR2_RefDrawer.Sort.Type;
            }
        }
    }
} 