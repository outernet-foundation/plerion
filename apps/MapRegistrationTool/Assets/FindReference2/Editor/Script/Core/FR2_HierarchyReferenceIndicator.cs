#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace vietlabs.fr2
{
    internal static class FR2_HierarchyReferenceIndicator
    {
        private static readonly Dictionary<int, ReferenceInfo> _referenceCache = new Dictionary<int, ReferenceInfo>();
        private static bool _isInitialized = false;
        private static GUIStyle _numberStyle;
        
        private class ReferenceInfo
        {
            public readonly int referenceCount;
            public GUIContent countText;
            public Vector2 textSize;

            public ReferenceInfo(int count) {
	            referenceCount = count;
	            countText = FR2_GUIContent.From(count.ToString());
	            textSize = _numberStyle.CalcSize(countText);
            }
        }
        
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyGUI;
            EditorApplication.hierarchyChanged -= RefreshReferenceCache;
            FR2_SceneCache.onReady -= RefreshReferenceCache;
            
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
            EditorApplication.hierarchyChanged += RefreshReferenceCache;
            FR2_SceneCache.onReady += RefreshReferenceCache;
            
            _referenceCache.Clear();
            _isInitialized = true;
        }
        
        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            _isInitialized = false;
            Initialize();
        }
        
        private static void OnHierarchyGUI(int instanceID, Rect selectionRect)
        {
            if (!_isInitialized) return;
            if (!FR2_SettingExt.showHierarchyReferenceCount) return;
            if (Event.current.type != EventType.Repaint) return;
            if (!FR2_SceneCache.isReady) return; // not ready!
            if (_numberStyle == null) return;
            if (!_referenceCache.TryGetValue(instanceID, out var refInfo) || refInfo.referenceCount == 0) return;

            var offset = FR2_SettingExt.hierarchyReferenceCountOffset;
            var textSize = refInfo.textSize;
            var textContent = refInfo.countText;
            var indicatorRect = new Rect(selectionRect.xMax - textSize.x - offset, selectionRect.y, textSize.x, selectionRect.height);
            GUI.Label(indicatorRect, textContent, _numberStyle);
        }
        
        
        private static void RefreshReferenceCache()
        {
            _referenceCache.Clear();
            if (_numberStyle == null) _numberStyle = new GUIStyle(EditorStyles.miniLabel)
            {
	            alignment = TextAnchor.MiddleRight,
	            normal = { textColor = new Color(0.7f, 0.7f, 0.7f, 1f) }
            };
            
            var usedByCounts = FR2_SceneCache.Api.gameObjectUsedByCount;
            foreach (var kvp in usedByCounts)
            {
	            // Debug.Log($"{kvp.Key} -> {kvp.Value}");
	            if (kvp.Value <= 0)  continue;
	            
	            // Debug.Log($"Add {kvp.Key} -> {kvp.Value}");
	            _referenceCache[kvp.Key] = new ReferenceInfo(kvp.Value);
            }
            
            // Debug.LogWarning($"{FR2_SceneCache.Api.GetHashCode()} - RefreshReferenceCache {usedByCounts.Count} --> {_referenceCache.Count}");
            EditorApplication.RepaintHierarchyWindow();
        }
        
        public static void SetEnabled(bool enabled)
        {
	        EditorApplication.RepaintHierarchyWindow();
        }
    }
}
#endif
