using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace vietlabs.fr2
{
    internal partial class FR2_Asset
    {
        // Static button content shared by all instances
        private static readonly GUIContent s_detailsButtonContent = new GUIContent("...", "Show Details");
        private static readonly GUIContent s_propertiesButtonContent = new GUIContent("P", "Open Properties");
        
        // Static width tracking for right panel elements - start with minimal values
        internal static float s_maxFileSizeWidth = 20f;
        internal static float s_maxAddressableWidth = 20f;
        internal static float s_maxAtlasNameWidth = 20f;
        internal static float s_maxAssetBundleWidth = 20f;
        private static bool s_maxWidthsChanged = false;
        
        internal class DrawCache
        {
            public GUIContent assetNameContent;
            public GUIContent extensionContent;
            public GUIContent fileSizeContent;
            public GUIContent addressableContent;
            public GUIContent assetBundleContent;
            public GUIContent atlasContent;
            public GUIContent usedByCountContent;
            public Dictionary<int, GUIContent> usageIconCache;
            
            public float assetNameWidthLabel;
            public float assetNameWidthBold;
            public float assetFolderWidth;
            public float extensionWidth;
            
            // Right panel element widths
            public float fileSizeWidth;
            public float addressableWidth;
            public float atlasWidth;
            public float assetBundleWidth;
            
            public Texture cachedIcon;
            public int lastUsedByCount;
            public bool isValid;
            public Rect clickableRect;
            public float iconStartX;
            
            // Performance caching
            public float cachedRightPanelWidth;
        }

        [System.NonSerialized] private DrawCache m_drawCache;
        
        internal DrawCache GetDrawCache()
        {
            if (m_drawCache == null || !m_drawCache.isValid)
            {
                RefreshDrawCache();
            }
            return m_drawCache;
        }
        
        private void RefreshDrawCache()
        {
            if (m_drawCache == null) 
            {
                m_drawCache = new DrawCache
                {
                    usageIconCache = new Dictionary<int, GUIContent>()
                };
            }
            
            // Use FR2_GUIContent for shared caching - no duplication
            m_drawCache.assetNameContent = FR2_GUIContent.FromString(m_assetName ?? string.Empty);
            m_drawCache.extensionContent = string.IsNullOrEmpty(m_extension) ? null : FR2_GUIContent.FromString(m_extension);
            m_drawCache.fileSizeContent = FR2_GUIContent.FromString(FR2_Helper.GetfileSizeString(m_fileSize));
            m_drawCache.addressableContent = string.IsNullOrEmpty(m_addressable) ? null : FR2_GUIContent.FromString(m_addressable);
            m_drawCache.assetBundleContent = string.IsNullOrEmpty(m_assetbundle) ? null : FR2_GUIContent.FromString(m_assetbundle);
            m_drawCache.atlasContent = string.IsNullOrEmpty(m_atlas) ? null : FR2_GUIContent.FromString(m_atlas);
            
            // Pre-calculate widths only - avoid CalcSize during draw
            m_drawCache.assetNameWidthLabel = EditorStyles.label.CalcSize(m_drawCache.assetNameContent).x;
            m_drawCache.assetNameWidthBold = EditorStyles.boldLabel.CalcSize(m_drawCache.assetNameContent).x;
            m_drawCache.extensionWidth = m_drawCache.extensionContent != null ? EditorStyles.miniLabel.CalcSize(m_drawCache.extensionContent).x : 0f;
            
            // Calculate right panel element widths and update static max widths
            CalculateRightPanelElementWidths();
            
            // Reset cached values that depend on configuration
            m_drawCache.cachedRightPanelWidth = 0f;
            
            // Cache icon - only get it once and store it
            if (m_drawCache.cachedIcon == null && !string.IsNullOrEmpty(m_assetPath))
            {
                m_drawCache.cachedIcon = AssetDatabase.GetCachedIcon(m_assetPath);
            }
            
            m_drawCache.isValid = true;
        }
        
        private void CalculateRightPanelElementWidths()
        {
            var style = GUI2.miniLabelAlignRight;
            bool widthsChanged = false;
            
            // Calculate file size width
            m_drawCache.fileSizeWidth = style.CalcSize(m_drawCache.fileSizeContent).x;
            if (m_drawCache.fileSizeWidth > s_maxFileSizeWidth)
            {
                s_maxFileSizeWidth = m_drawCache.fileSizeWidth;
                widthsChanged = true;
            }
            
            // Calculate addressable width
            if (m_drawCache.addressableContent != null)
            {
                m_drawCache.addressableWidth = style.CalcSize(m_drawCache.addressableContent).x;
                if (m_drawCache.addressableWidth > s_maxAddressableWidth)
                {
                    s_maxAddressableWidth = m_drawCache.addressableWidth;
                    widthsChanged = true;
                }
            }
            
            // Calculate atlas width
            if (m_drawCache.atlasContent != null)
            {
                m_drawCache.atlasWidth = style.CalcSize(m_drawCache.atlasContent).x;
                if (m_drawCache.atlasWidth > s_maxAtlasNameWidth)
                {
                    s_maxAtlasNameWidth = m_drawCache.atlasWidth;
                    widthsChanged = true;
                }
            }
            
            // Calculate asset bundle width
            if (m_drawCache.assetBundleContent != null)
            {
                m_drawCache.assetBundleWidth = style.CalcSize(m_drawCache.assetBundleContent).x;
                if (m_drawCache.assetBundleWidth > s_maxAssetBundleWidth)
                {
                    s_maxAssetBundleWidth = m_drawCache.assetBundleWidth;
                    widthsChanged = true;
                }
            }
            
            if (widthsChanged)
            {
                s_maxWidthsChanged = true;
                // Invalidate all cached right panel widths since they need recalculation
                InvalidateAllRightPanelWidths();
            }
        }
        
        internal void InvalidateDrawCache()
        {
            if (m_drawCache != null) m_drawCache.isValid = false;
        }
        
        internal static void ClearAllDrawCaches()
        {
            // This could be called when themes change or major UI updates occur
            FR2_GUIContent.Release(); // Clear the shared GUI content cache
            ResetMaxWidths();
        }
        
        internal static void ResetMaxWidths()
        {
            s_maxFileSizeWidth = 20f;
            s_maxAddressableWidth = 20f;
            s_maxAtlasNameWidth = 20f;
            s_maxAssetBundleWidth = 20f;
            s_maxWidthsChanged = true;
        }
        
        internal static void ForceRecalculateWidths()
        {
            ResetMaxWidths();
            // Force invalidate all draw caches so they recalculate
            InvalidateAllDrawCaches();
        }
        
        private static void InvalidateAllDrawCaches()
        {
            // This would ideally iterate through all FR2_Asset instances and invalidate their caches
            // For now, we trigger a repaint which will cause recalculation
        }
        
        internal static bool ConsumeMaxWidthsChanged()
        {
            bool changed = s_maxWidthsChanged;
            s_maxWidthsChanged = false;
            return changed;
        }
        
        private static void InvalidateAllRightPanelWidths()
        {
            // This would ideally iterate through all FR2_Asset instances and invalidate their cached widths
            // For now, we'll rely on the cache invalidation happening naturally during the next draw cycle
            // since cached widths are reset to 0 when draw cache is refreshed
        }
    }
}
