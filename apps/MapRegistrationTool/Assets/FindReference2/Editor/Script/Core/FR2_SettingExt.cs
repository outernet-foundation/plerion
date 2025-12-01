using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace vietlabs.fr2
{
    [Serializable] internal class FR2_SettingExt
    {
        public static FR2_AutoRefreshMode autoRefreshMode
        {
            get => inst._autoRefresh;
            set
            {
                if (inst._autoRefresh == value) return;
                inst._autoRefresh = value;

                FR2_Cache.MarkDirty();
                // EditorUtility.SetDirty(FR2_Cache._inst);
                EditorApplication.update -= DelaySave;
                EditorApplication.update += DelaySave;
            }
        }

        public static bool isAutoRefreshEnabled
        {
            get
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
                return autoRefreshMode == FR2_AutoRefreshMode.On;
            }
        }
        
        public static bool disable
        {
            get => inst.internalDisabled;
            set => inst.internalDisabled = value;
        }
        
        public static bool hideToolsWarning
        {
            get => inst._hideToolsWarning;
            set
            {
                if (inst._hideToolsWarning == value) return;
                inst._hideToolsWarning = value;

                FR2_Cache.MarkDirty();
                EditorApplication.update -= DelaySave;
                EditorApplication.update += DelaySave;
            }
        }

        public static bool isGitProject;
        public static bool gitIgnoreAdded
        {
            get => inst._gitIgnoreAdded;
            set
            {
                if (inst._gitIgnoreAdded == value) return;
                inst._gitIgnoreAdded = value;
                
                FR2_Cache.MarkDirty();
                EditorApplication.update -= DelaySave;
                EditorApplication.update += DelaySave;
            }
        }
        
        public static bool hideGitIgnoreWarning
        {
            get => inst._hideGitIgnoreWarning;
            set
            {
                if (inst._hideGitIgnoreWarning == value) return;
                inst._hideGitIgnoreWarning = value;
                
                FR2_Cache.MarkDirty();
                EditorApplication.update -= DelaySave;
                EditorApplication.update += DelaySave;
            }
        }
        
        public static bool showHierarchyReferenceCount
        {
            get => inst._showHierarchyReferenceCount;
            set
            {
                if (inst._showHierarchyReferenceCount == value) return;
                inst._showHierarchyReferenceCount = value;
                
                FR2_HierarchyReferenceIndicator.SetEnabled(value);
                EditorApplication.RepaintHierarchyWindow();
                
                EditorApplication.update -= DelaySave;
                EditorApplication.update += DelaySave;
            }
        }
        
        public static float hierarchyReferenceCountOffset
        {
            get => inst._hierarchyReferenceCountOffset;
            set
            {
                if (Mathf.Approximately(inst._hierarchyReferenceCountOffset, value)) return;
                inst._hierarchyReferenceCountOffset = value;
                
                EditorApplication.RepaintHierarchyWindow();
                
                EditorApplication.update -= DelaySave;
                EditorApplication.update += DelaySave;
            }
        }
        
        private const string path = "Library/FR2/fr2.cfg";
        private static FR2_SettingExt inst;
        
        static FR2_SettingExt()
        {
            
            inst = new FR2_SettingExt();
            if (!File.Exists(path)) return;

            try
            {
                string content = File.ReadAllText(path);
                JsonUtility.FromJsonOverwrite(content, inst);
            }
            catch (Exception e)
            {
                FR2_LOG.LogWarning(e);
            }
        }

        static void DelaySave()
        {
            EditorApplication.update -= DelaySave;
            
            try
            {
                Directory.CreateDirectory("Library/FR2/");
                File.WriteAllText(path, JsonUtility.ToJson(inst));
            }
            catch (Exception e)
            {
                FR2_LOG.LogWarning(e);
            }
        }
        
        [SerializeField] private bool _disableInPlayMode = true;
        [SerializeField] private bool _disabled;
        [SerializeField] private FR2_AutoRefreshMode _autoRefresh;
        [SerializeField] private bool _hideToolsWarning;
        [SerializeField] private bool _isGitProject;
        [SerializeField] private bool _gitIgnoreAdded;
        [SerializeField] private bool _hideGitIgnoreWarning;
        [SerializeField] private bool _showHierarchyReferenceCount = true;
        [SerializeField] private float _hierarchyReferenceCountOffset = 0f;
        
        private bool internalDisabled
        {
            get => _disabled || (_disableInPlayMode && EditorApplication.isPlayingOrWillChangePlaymode);
            set
            {
                ref bool disableRef = ref _disabled;
                if (EditorApplication.isPlayingOrWillChangePlaymode) disableRef = ref _disableInPlayMode;
                
                if (disableRef == value) return;
                disableRef = value;
                
                // disable at runtime: only disable `disableInPlayMode`
                // enable at runtime: enable all
                if (!value) _disabled = false;
                FR2_Cache.MarkDirty();
                EditorApplication.update -= DelaySave;
                EditorApplication.update += DelaySave;
            }
        }
    }
}
