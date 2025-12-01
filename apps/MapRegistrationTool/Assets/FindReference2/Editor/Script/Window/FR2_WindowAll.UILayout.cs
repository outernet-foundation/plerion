using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace vietlabs.fr2
{
    internal partial class FR2_WindowAll
    {
        internal FR2_RefDrawer[] _allDrawersCache;

        private void OnCSVClickExtension()
        {
            FR2_Ref[] csvSource = null;
            FR2_RefDrawer drawer = GetAssetDrawer();

            if (drawer != null) csvSource = drawer.source;

            if (isFocusingUnused && (csvSource == null)) csvSource = RefUnUse.source;
            if (isFocusingUsedInBuild && (csvSource == null)) csvSource = FR2_Ref.FromDict(UsedInBuild.refs);
            if (isFocusingDuplicate && (csvSource == null)) csvSource = FR2_Ref.FromList(Duplicated.list);

            FR2_Export.ExportCSV(csvSource);
        }

        private void RefreshPanelVisible()
        {
            if (sp2 == null) InitIfNeeded();
            if (sp2 == null) return;

            sp2.SetSplitVisible(0, isScenePanelVisible);
            sp2.SetSplitVisible(1, isAssetPanelVisible);
            sp2.SetSplitVisible(2, isFocusingAddressable);
        }

        private void RefreshShowFullPath()
        {
            if (_allDrawersCache != null)
            {
                foreach (var drawer in _allDrawersCache)
                {
                    if (drawer != null && drawer.Config != null) drawer.Config.showFullPath = settings.showFullPath;
                }
            }
        }

        private void RefreshShowFileSize()
        {
            if (_allDrawersCache != null)
            {
                foreach (var drawer in _allDrawersCache)
                {
                    if (drawer != null && drawer.AssetConfig != null) drawer.AssetConfig.showFileSize = settings.showFileSize;
                }
            }
        }

        private void RefreshShowFileExtension()
        {
            if (_allDrawersCache == null) return;
            foreach (var drawer in _allDrawersCache)
            {
                if (drawer != null && drawer.AssetConfig != null) drawer.AssetConfig.showExtension = settings.showFileExtension;
            }
        }

        private void MarkDirty()
        {
            if (_allDrawersCache != null)
            {
                foreach (var drawer in _allDrawersCache)
                {
                    drawer?.SetDirty();
                }
            }
            Duplicated.SetDirty();
            UsedInBuild.SetDirty();
            AddressableDrawer.RefreshSort();
            WillRepaint = true;
        }

        private void RefreshSort()
        {
            if (_allDrawersCache != null)
            {
                foreach (var drawer in _allDrawersCache)
                {
                    drawer?.RefreshSort();
                }
            }
            AddressableDrawer.RefreshSort();
            Duplicated.RefreshSort();
            UsedInBuild.RefreshSort();
            bookmark?.RefreshSort();
            
            // Ensure tool-specific drawers are also refreshed
            if (settings.toolMode)
            {
                RefUnUse?.RefreshSort();
            }
            
            Repaint();
        }

        private FR2_RefDrawer GetAssetDrawer()
        {
            if (isFocusingUses) return IsSelectingAssets ? UsesDrawer : SceneToAssetDrawer;
            if (isFocusingUsedBy) return IsSelectingAssets ? UsedByDrawer : null;
            if (isFocusingAddressable) return AddressableDrawer.drawer;
            return null;
        }
    }
} 