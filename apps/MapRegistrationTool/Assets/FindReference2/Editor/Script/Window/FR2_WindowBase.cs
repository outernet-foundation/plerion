using UnityEditor;
using UnityEngine;
namespace vietlabs.fr2
{

    public abstract class FR2_WindowBase : EditorWindow, IWindow
    {
        public bool WillRepaint { get; set; }
        protected bool showFilter, showIgnore;

        //[NonSerialized] protected bool lockSelection;
        //[NonSerialized] internal List<FR2_Asset> Selected;

        public void AddItemsToMenu(GenericMenu menu)
        {
            var api = FR2_Cache._inst;
            if (api == null) return;

            menu.AddDisabledItem(FR2_GUIContent.FromString("FR2 - v2.6.10"));
            menu.AddSeparator(string.Empty);

            menu.AddItem(FR2_GUIContent.FromString("Enable"), !FR2_SettingExt.disable, () => { FR2_SettingExt.disable = !FR2_SettingExt.disable; });
            menu.AddItem(FR2_GUIContent.FromString($"Auto Refresh: {FR2_SettingExt.autoRefreshMode}"), FR2_SettingExt.isAutoRefreshEnabled, () =>
            {
                FR2_SettingExt.autoRefreshMode = FR2_SettingExt.isAutoRefreshEnabled ? FR2_AutoRefreshMode.Off : FR2_AutoRefreshMode.On;
                if (FR2_SettingExt.autoRefreshMode == FR2_AutoRefreshMode.On)
                {
                    FR2_Cache.IncrementalRefresh();
                }
            });
            
            menu.AddItem(FR2_GUIContent.FromString($"Refresh"), false, () =>
            {
                FR2_Cache.ClearCacheCompletely();
                FR2_Cache.Check4Changes(true);
            });

            menu.AddSeparator(string.Empty);
            
            // Developer mode toggle
            bool isDebugMode = FR2_Define.IsDebugModeEnabled();
            menu.AddItem(FR2_GUIContent.FromString($"Developer Mode"), isDebugMode, () =>
            {
                FR2_Define.ToggleDebugMode(!isDebugMode);
            });

            AddToCustomMenu(menu);
        }
        
        public abstract void AddToCustomMenu(GenericMenu menu);

        public abstract void OnSelectionChange();
        protected abstract void OnGUI();

#if UNITY_2018_OR_NEWER
        protected void OnSceneChanged(Scene arg0, Scene arg1)
        {
            if (IsFocusingFindInScene || IsFocusingSceneToAsset || IsFocusingSceneInScene)
            {
                OnSelectionChange();
            }
        }
#endif

        protected bool DrawEnable()
        {
            FR2_Cache api = FR2_Cache._inst;
            if (api == null) return false;
            if (!FR2_SettingExt.disable) return true;

            bool isPlayMode = EditorApplication.isPlayingOrWillChangePlaymode;
            string message = isPlayMode
                ? "Find References 2 is disabled in play mode!"
                : "Find References 2 is disabled!";

            EditorGUILayout.HelpBox(FR2_GUIContent.From(message, FR2_Icon.Warning.image));
            if (GUILayout.Button(FR2_GUIContent.FromString("Enable")))
            {
                FR2_SettingExt.disable = !FR2_SettingExt.disable;
                if (!FR2_SettingExt.disable && FR2_Cache._inst != null)
                {
                    FR2_Cache.IncrementalRefresh();
                }
                Repaint();
            }

            return false;
        }

    }
}
