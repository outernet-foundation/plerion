using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace vietlabs.fr2
{
    public class FR2_AssetInfo
    {
        [NonSerialized] internal static readonly Dictionary<string, FR2_AssetInfo> infoMap = new Dictionary<string, FR2_AssetInfo>();
        
        internal static FR2_AssetInfo Get(string guid) => infoMap.GetValueOrDefault(guid);
        internal static FR2_AssetInfo GetOrCreate(string guid) => infoMap.GetValueOrDefault(guid) ?? new FR2_AssetInfo(guid);
        internal static void Clear() => infoMap.Clear();
        
        public string guid;
        public string assetPath;

        public string folder;
        public string fileName;
        public string fileExt;

        // GUIContent
        public GUIContent folderContent;
        public GUIContent fileNameContent;
        public GUIContent fileExtContent;

        private FR2_AssetInfo(string guid)
        {
            this.guid = guid;
            assetPath = AssetDatabase.GUIDToAssetPath(guid);
            folder = System.IO.Path.GetDirectoryName(assetPath) + "/";
            fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            fileExt = System.IO.Path.GetExtension(assetPath);

            infoMap.Add(guid, this);
        }

        public void RefreshGUIContent()
        {
            folderContent = FR2_GUIContent.From(folder);
            fileNameContent = FR2_GUIContent.From(fileName);
            fileExtContent = string.IsNullOrEmpty(fileExt) ? GUIContent.none : FR2_GUIContent.From(fileExt);
        }
    }
}
