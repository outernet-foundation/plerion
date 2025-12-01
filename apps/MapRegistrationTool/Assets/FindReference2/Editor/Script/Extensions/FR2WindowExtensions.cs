using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace vietlabs.fr2
{
    internal static class FR2WindowExtensions
    {
        // Panel visibility extensions
        internal static bool IsScenePanelVisible(this FR2_WindowAll window)
        {
            if (window.isFocusingAddressable) return false;

            if (window.selection.isSelectingAsset && window.isFocusingUses) // Override
            {
                return false;
            }

            if (!window.selection.isSelectingAsset && window.isFocusingUsedBy)
            {
                return true;
            }

            return window.settings.scene;
        }

        internal static bool IsAssetPanelVisible(this FR2_WindowAll window)
        {
            if (window.isFocusingAddressable) return false;

            if (window.selection.isSelectingAsset && window.isFocusingUses) // Override
            {
                return true;
            }

            if (!window.selection.isSelectingAsset && window.isFocusingUsedBy)
            {
                return false;
            }

            return window.settings.asset;
        }

        internal static void RefreshPanelVisibility(this FR2_WindowAll window)
        {
            window.sp2.SetSplitVisible(0, window.IsScenePanelVisible());
            window.sp2.SetSplitVisible(1, window.IsAssetPanelVisible());
            window.sp2.SetSplitVisible(2, window.isFocusingAddressable);
        }

        // Selection management extensions
        internal static UnityObject[] GetCachedSelectionExtension(this FR2_WindowAll window)
        {
            int currentFrame = Time.frameCount;
            if (window._cachedSelectionFrame != currentFrame)
            {
                window._cachedSelection = Selection.objects;
                window._cachedSelectionFrame = currentFrame;
            }
            return window._cachedSelection;
        }

        // Asset drawer extension
        internal static FR2_RefDrawer GetAssetDrawerExtension(this FR2_WindowAll window)
        {
            if (window.isFocusingUses) return window.selection.isSelectingAsset ? window.UsesDrawer : window.SceneToAssetDrawer;
            if (window.isFocusingUsedBy) return window.selection.isSelectingAsset ? window.UsedByDrawer : null;
            if (window.isFocusingAddressable) return window.AddressableDrawer.drawer;
            return null;
        }
        
        internal static void OnCSVClickExtension(this FR2_WindowAll window)
        {
            FR2_Ref[] csvSource = null;
            FR2_RefDrawer drawer = window.GetAssetDrawerExtension();

            if (drawer != null) csvSource = drawer.source;

            if (window.isFocusingUnused && (csvSource == null)) csvSource = window.RefUnUse.source;
            if (window.isFocusingUsedInBuild && (csvSource == null)) csvSource = FR2_Ref.FromDict(window.UsedInBuild.refs);
            if (window.isFocusingDuplicate && (csvSource == null)) csvSource = FR2_Ref.FromList(window.Duplicated.list);

            FR2_Export.ExportCSV(csvSource);
        }
        
        internal static void CacheAllDrawers(this FR2_WindowAll window)
        {
            window._allDrawersCache = new FR2_RefDrawer[]
            {
                window.UsedByDrawer,
                window.UsesDrawer,
                window.SceneToAssetDrawer,
                window.RefUnUse,
                window.RefInScene,
                window.RefSceneInScene,
                window.SceneUsesDrawer,
                window.UsedInBuild.Drawer
            };
        }

        internal static void RefreshShowUsageType(this FR2_WindowAll window)
        {
            if (window._allDrawersCache != null)
            {
                foreach (var drawer in window._allDrawersCache)
                {
                    if (drawer != null && drawer.AssetConfig != null) drawer.AssetConfig.showUsageType = window.settings.showUsageType;
                }
            }
        }

        internal static void RefreshSort(this FR2_WindowAll window)
        {
            if (window._allDrawersCache != null)
            {
                foreach (var drawer in window._allDrawersCache)
                {
                    drawer?.RefreshSort();
                }
            }
            window.AddressableDrawer.RefreshSort();
            window.Duplicated.RefreshSort();
            window.UsedInBuild.RefreshSort();
            
            // Ensure tool-specific drawers are also refreshed
            if (window.settings.toolMode)
            {
                window.RefUnUse?.RefreshSort();
            }
        }
    }
} 