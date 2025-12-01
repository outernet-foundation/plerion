using System.Text;
using UnityEditor;
using UnityEngine;
namespace vietlabs.fr2
{
    public static class FR2_USelection
    {
        private static readonly StringBuilder sb = new StringBuilder();

        internal static void StartDebugReference()
        {
            Selection.selectionChanged -= DebugAssetReference;
            Selection.selectionChanged += DebugAssetReference;
        }

        internal static void StopDebugReference()
        {
            Selection.selectionChanged -= DebugAssetReference;
        }

        private static void DebugAssetReference()
        {
            if (!FR2_CacheAsset.isReady) return;
            
            Object activeObject = Selection.activeObject;
            if (activeObject == null) return;
            
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(activeObject, out string guid, out long fileId);
            var isMainAsset = AssetDatabase.IsMainAsset(activeObject);
            var usageList = FR2_CacheAsset.CollectUsage(guid);
            var usedByList = FR2_CacheAsset.CollectUsedBy(guid, isMainAsset ? -1 : fileId);
            
            sb.Clear();
            sb.AppendLine($"{guid}:{fileId} : {AssetDatabase.GUIDToAssetPath(guid)}");
            
            sb.AppendLine($"Used: {usageList.Count}\n");
            for (var i = 0; i < usageList.Count; i++)
            {
                FR2_IDRef usage = usageList[i];
                var (useGUID, useFileId) = FR2_CacheAsset.GetGuidAndFileId(usage.toId);
                sb.AppendLine($"{useGUID}:{useFileId} - {usage} \t\t {AssetDatabase.GUIDToAssetPath(useGUID)}");
            }

            sb.AppendLine($"UsedBy: {usedByList.Count}\n");
            for (var i = 0; i < usedByList.Count; i++)
            {
                FR2_IDRef useBy = usedByList[i];
                var (useByGUID, _) = FR2_CacheAsset.GetGuidAndFileId(useBy.fromId);
                sb.AppendLine($"{useByGUID} - {useBy} \t\t {AssetDatabase.GUIDToAssetPath(useByGUID)}");
            }
            
            Debug.Log(sb.ToString());
        }
    }
}
