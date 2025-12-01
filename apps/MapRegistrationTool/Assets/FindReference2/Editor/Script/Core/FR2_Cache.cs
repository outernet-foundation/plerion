//#define FR2_DEBUG
// #define FR2_DEV

using System;
using UnityEditor;
using UnityEngine;

namespace vietlabs.fr2
{
    internal partial class FR2_Cache : ScriptableObject
    {
        public static bool autoRefresh => FR2_SettingExt.isAutoRefreshEnabled;
        
        [NonSerialized] private bool _needsRefreshOnFocus;
        [NonSerialized] private bool _isFR2WindowFocused;
        
        // ----------------------------------- INSTANCE -------------------------------------
        
        [NonSerialized] private int frameSkipped;

        private void OnEnable()
        {
            // Register for window focus changes
            FR2_WindowFocus.FocusedWindowChanged -= OnWindowFocusChanged;
            FR2_WindowFocus.FocusedWindowChanged += OnWindowFocusChanged;
        }

        private void OnWindowFocusChanged(EditorWindow window)
        {
            bool isFR2Window = window != null && window.GetType().Name.Contains("FR2_Window");
            bool wasFocused = _isFR2WindowFocused;
            _isFR2WindowFocused = isFR2Window;
            
            // If FR2 window just gained focus and we need to refresh, do it now
            if (isFR2Window && !wasFocused && _needsRefreshOnFocus && FR2_SettingExt.isAutoRefreshEnabled)
            {
                _needsRefreshOnFocus = false;
                IncrementalRefresh();
            }
        }
    }
}
