using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace vietlabs.fr2
{
    internal partial class FR2_Asset
    {
        // ----------------------------- UI DRAWING & USER ACTIONS ---------------------------------------
        // PERFORMANCE OPTIMIZATIONS APPLIED:
        // 1. Per-asset GUI content caching using FR2_GUIContent shared cache
        // 2. Pre-calculated text widths to eliminate CalcSize() calls during drawing
        // 3. Cached icons, file sizes, and usage icons
        // 4. Extracted methods to reduce main Draw() complexity and branching
        // 5. Optimized color operations and removed duplicate GUI calls
        // 6. Early return for non-repaint events
        // 7. Proper cache invalidation when asset data changes

        internal class FR2_AssetDrawConfig
        {
            public bool highlight;
            public bool drawPath = true;
            public bool showFileSize = true;
            public bool showABName = false;
            public bool showAtlasName = false;
            public bool showUsageIcon = false;
            public IWindow window = null;
            public bool drawExtension = true;
            public Action onShowDetails = null;

            public FR2_AssetDrawConfig(
            bool highlight,
            bool drawPath = true,
            bool showFileSize = true,
            bool showABName = false,
            bool showAtlasName = false,
            bool showUsageIcon = false,
            IWindow window = null,
            bool drawExtension = true,
                Action onShowDetails = null)
            {
                this.highlight = highlight;
                this.drawPath = drawPath;
                this.showFileSize = showFileSize;
                this.showABName = showABName;
                this.showAtlasName = showAtlasName;
                this.showUsageIcon = showUsageIcon;
                this.window = window;
                this.drawExtension = drawExtension;
                this.onShowDetails = onShowDetails;
            }
        }

        internal float Draw(
            Rect r,
            FR2_AssetDrawConfig cfg
        )
        {
            bool isRepaint = Event.current.type == EventType.Repaint;
            if (!isRepaint && !Event.current.isMouse)
            {
                // Debug.Log($"Event: {Event.current.type}");
                return FR2_Theme.Current.TreeItemHeight;
            }
            
            bool selected = FR2_Bookmark.Contains(guid);
            Rect rowRect = new Rect(r.x, r.y, r.width, FR2_Theme.Current.TreeItemHeight);
            bool isHover = isRepaint && rowRect.Contains(Event.current.mousePosition);
            bool singleLine = r.height <= 18f;
            r.height = FR2_Theme.Current.TreeItemHeight;
            HandleMouseEvents(r);
            
            if (IsMissing)
            {
                return DrawMissingAsset(r, singleLine);
            }

            var cache = GetDrawCache();
            CalculateRightPanelWidth(cfg, cache);
            
            // Check if max widths changed and trigger repaint if needed
            if (ConsumeMaxWidthsChanged() && cfg.window != null)
            {
                cfg.window.Repaint();
            }
            
            DrawUsedByCount(ref r, cache);
            float iconStartX = r.x; // Store icon start position for clickable rect
            DrawIcon(ref r, cache);
            DrawActionButtons(ref r, cfg, isHover);
            
            if (!isRepaint) return FR2_Theme.Current.TreeItemHeight;
            if (Event.current.type == EventType.Repaint)
            {
                cache.iconStartX = iconStartX;
            }
            
            DrawSingleLineLabels(ref r, cfg, cache, selected);
            DrawRightPanelInfo(ref r, cfg, cache);
            return FR2_Theme.Current.TreeItemHeight;
        }
        
        internal GenericMenu AddArray(
            GenericMenu menu, System.Collections.Generic.List<string> list, string prefix, string title,
            string emptyTitle, bool showAsset, int max = 10)
        {
            menu.AddItem(FR2_GUIContent.FromString(emptyTitle), true, null);
            return menu;
        }

        internal void CopyGUID()
        {
            EditorGUIUtility.systemCopyBuffer = guid;
            Debug.Log(guid);
        }

        internal void CopyName()
        {
            EditorGUIUtility.systemCopyBuffer = m_assetName;
            Debug.Log(m_assetName);
        }

        internal void CopyAssetPath()
        {
            EditorGUIUtility.systemCopyBuffer = m_assetPath;
            Debug.Log(m_assetPath);
        }

        internal void CopyAssetPathFull()
        {
            string fullName = new FileInfo(m_assetPath).FullName;
            EditorGUIUtility.systemCopyBuffer = fullName;
            Debug.Log(fullName);
        }


        internal void RemoveFromSelection()
        {
            if (FR2_Bookmark.Contains(guid)) FR2_Bookmark.Remove(guid);
        }

        internal void AddToSelection()
        {
            if (!FR2_Bookmark.Contains(guid)) FR2_Bookmark.Add(guid);
        }

        internal void Ping()
        {
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath(m_assetPath, typeof(UnityObject)));
        }

        internal void Open()
        {
            AssetDatabase.OpenAsset(
                AssetDatabase.LoadAssetAtPath(m_assetPath, typeof(UnityObject))
            );
        }

        internal void OpenProperties()
        {
#if UNITY_2022_3_OR_NEWER
            var obj = AssetDatabase.LoadAssetAtPath(m_assetPath, typeof(UnityObject));
            if (obj != null)
            {
                EditorUtility.OpenPropertyEditor(obj);
            }
#endif
        }


        private void HandleMouseEvents(Rect r)
        {
            bool hasMouse = (Event.current.type == EventType.MouseDown) && r.Contains(Event.current.mousePosition);
            if (!hasMouse) return;
            if (Event.current.button == 1)
            {
                ShowContextMenu();
                Event.current.Use();
                return;
            }
            
            if (Event.current.button == 0)
            {
                var cache = GetDrawCache();
                if (cache.clickableRect.Contains(Event.current.mousePosition))
                {
                    
                    if (Event.current.clickCount == 2)
                    {   
                        Open();
                        return;
                    }
                    
                    Ping();
                }
            }
        }
        
        private float DrawMissingAsset(Rect r, bool singleLine)
        {
            if (!singleLine) r.y += 16f;
            if (Event.current.type != EventType.Repaint) return 0;

            var missingCache = GetDrawCache();
            if (missingCache.assetNameContent == null || missingCache.assetNameContent.text != guid)
            {
                missingCache.assetNameContent = FR2_GUIContent.FromString(guid);
            }
            GUI.Label(r, missingCache.assetNameContent, EditorStyles.whiteBoldLabel);
            return 0;
        }
        
        private void DrawIcon(ref Rect r, DrawCache cache)
        {
            Rect iconRect = GUI2.LeftRect(16f, ref r);
            GUI2.LeftRect(2f, ref r);
            
            EventType evtType = Event.current.type;
            
            if (evtType == EventType.Repaint && cache.cachedIcon != null)
            {
                GUI.DrawTexture(iconRect, cache.cachedIcon, ScaleMode.ScaleToFit);
            }
        }
        
        private void DrawActionButtons(ref Rect r, FR2_AssetDrawConfig cfg, bool isHover)
        {
            r.xMax -= 4f;
            if (cfg.onShowDetails != null)
            {
                var (detailRect, flex) = r.ExtractRight(20f);
                // Always draw button, but use invisible style when not hovering
                GUIContent detailContent = isHover ? s_detailsButtonContent : GUIContent.none;
                GUIStyle detailStyle = isHover ? EditorStyles.miniButton : EditorStyles.label;
                
                if (GUI.Button(detailRect, detailContent, detailStyle))
                {
                    cfg.onShowDetails?.Invoke();
                }
                r = flex;
            }
                
#if UNITY_2022_3_OR_NEWER
            var (propRect, flex1) = r.ExtractRight(20f);
            // Always draw button, but use invisible style when not hovering
            GUIContent propContent = isHover ? s_propertiesButtonContent : GUIContent.none;
            GUIStyle propStyle = isHover ? EditorStyles.miniButton : EditorStyles.label;
            
            if (GUI.Button(propRect, propContent, propStyle))
            {
                OpenProperties();
            }
            r = flex1;
#endif
        }
        
        private void DrawUsedByCount(ref Rect r, DrawCache cache)
        {
            if (_usedByMap == null || _usedByMap.Count == 0) return;
            if (Event.current.type != EventType.Repaint) return;
            if (cache.usedByCountContent == null || cache.lastUsedByCount != _usedByMap.Count)
            {
                cache.usedByCountContent = FR2_GUIContent.FromInt(_usedByMap.Count);
                cache.lastUsedByCount = _usedByMap.Count;
            }
            
            const float w = 24f;
            var countRect = new Rect(r.x - w, r.y, w, FR2_Theme.Current.TreeItemHeight);
            countRect.RDrawMiniLabel(cache.usedByCountContent, Color.gray);
        }
        
        private void DrawSingleLineLabels(ref Rect r, FR2_AssetDrawConfig cfg, DrawCache cache, bool selected)
        {
            float overlapW = 2f;
            float nameW = (cfg.drawPath ? cache.assetNameWidthBold : cache.assetNameWidthLabel);
            float extW = cache.extensionWidth;
            Color selectionColor = GUI.skin.settings.selectionColor;
            var isRepaint = Event.current.type == EventType.Repaint;
            
            var currentX = r.x;
            if (cfg.drawPath)
            {
                // GUI2.Rect(r, Color.aquamarine);
                var pathW = r.width - cache.cachedRightPanelWidth - nameW - extW;
                var pathRect = new Rect(currentX, r.y, pathW, r.height);
                float actualPathWidth;
                using (FR2_Scope.GUIColor(FR2_Theme.Current.SecondaryTextColor))
                {
                    actualPathWidth = ClippedLabel.Draw(pathRect, assetFolder, EditorStyles.miniLabel);
                }
                currentX += actualPathWidth - overlapW;
            }
            
            var nameRect = new Rect(currentX, r.y, nameW + 2f, r.height);
            if (selected)
            {
                using (FR2_Scope.GUIColor(selectionColor))
                {
                    if (isRepaint) GUI.DrawTexture(nameRect, EditorGUIUtility.whiteTexture);
                }
            }
            
            var assetNameStyle = selected 
                ? (cfg.drawPath ? EditorStyles.whiteBoldLabel : EditorStyles.whiteLabel)
                : (cfg.drawPath ? EditorStyles.boldLabel : EditorStyles.label);

            var maxX = nameRect.xMax;
            if (isRepaint && cache.assetNameContent != null) GUI.Label(nameRect, cache.assetNameContent, assetNameStyle);
            
            if (cfg.drawExtension && cache.extensionContent != null)
            {
                var extensionX = nameRect.x + nameW - overlapW;
                var extRect = new Rect(extensionX, r.y + 1f, cache.extensionWidth, r.height);
                using (FR2_Scope.GUIColor(FR2_Theme.Current.SecondaryTextColor))
                {
                    if (isRepaint) GUI.Label(extRect, cache.extensionContent, EditorStyles.miniLabel);
                }
                maxX = extRect.xMax;
            }
            
            if (Event.current.type == EventType.Repaint)
            {
                cache.clickableRect = new Rect(cache.iconStartX, r.y, maxX - cache.iconStartX, r.height);
            }
        }
        
        private void DrawRightPanelInfo(ref Rect r, FR2_AssetDrawConfig cfg, DrawCache cache)
        {
            // Batch all the dimmed color operations together
            bool hasDimmedContent = cfg.showFileSize || cache.addressableContent != null || 
                                   (cfg.showAtlasName && cache.atlasContent != null) || 
                                   (cfg.showABName && cache.assetBundleContent != null);
            
            if (hasDimmedContent)
            {
                using (FR2_Scope.GUIColor(FR2_Theme.Current.SecondaryTextColor))
                {
                    var isRepaint = Event.current.type == EventType.Repaint;

                    if (cfg.showFileSize && cache.fileSizeContent != null)
                    {
                        Rect fsRect = GUI2.RightRect(s_maxFileSizeWidth + 4f, ref r);
                        if (isRepaint) GUI.Label(fsRect, cache.fileSizeContent, GUI2.miniLabelAlignRight);
                    }

                    if (cache.addressableContent != null)
                    {
                        Rect adRect = GUI2.RightRect(s_maxAddressableWidth + 4f, ref r);
                        if (isRepaint) GUI.Label(adRect, cache.addressableContent, GUI2.miniLabelAlignRight);
                    }

                    if (cfg.showAtlasName && cache.atlasContent != null)
                    {
                        GUI2.RightRect(10f, ref r);
                        Rect abRect = GUI2.RightRect(s_maxAtlasNameWidth + 4f, ref r);
                        if (isRepaint) GUI.Label(abRect, cache.atlasContent, GUI2.miniLabelAlignRight);
                    }

                    if (cfg.showABName && cache.assetBundleContent != null)
                    {
                        GUI2.RightRect(10f, ref r);
                        Rect abRect = GUI2.RightRect(s_maxAssetBundleWidth + 4f, ref r);
                        if (isRepaint) GUI.Label(abRect, cache.assetBundleContent, GUI2.miniLabelAlignRight);
                    }
                }
            }
            
            // Usage icons use normal color
            // if (cfg.showUsageIcon && HashUsedByClassesIds != null && HashUsedByClassesIds.Count > 0)
            // {
            //     DrawUsageIcons(ref r);
            // }
        }
        
        private float GetTotalLabelWidth(FR2_AssetDrawConfig cfg, DrawCache cache)
        {
            float pathW = cfg.drawPath ? cache.assetFolderWidth : 8f;
            float nameW = cfg.drawPath ? cache.assetNameWidthBold : cache.assetNameWidthLabel;
            return pathW + nameW;
        }
        
        private float CalculateRightPanelWidth(FR2_AssetDrawConfig cfg, DrawCache cache)
        {
            // Performance optimization: Cache the right panel width calculation
            if (cache.cachedRightPanelWidth > 0) return cache.cachedRightPanelWidth;
            
            float width = 0f;
            width += 2f; // Base padding
            
            if (cfg.showFileSize)
            {
                width += s_maxFileSizeWidth + 4f;
                // Debug.Log($"MaxFileSizeWidth: {s_maxFileSizeWidth}");
            }
            
            // Addressable content - use calculated width with small padding
            if (cache.addressableContent != null)
            {
                width += s_maxAddressableWidth + 4f;
                // Debug.Log($"s_maxAddressableWidth: {s_maxAddressableWidth}");
            }
            
            // Atlas name - use calculated width with padding
            if (cfg.showAtlasName && cache.atlasContent != null) {
                width += s_maxAtlasNameWidth + 4f;
                // Debug.Log($"s_maxAtlasNameWidth: {s_maxAtlasNameWidth}");
            }
            
            // Asset bundle name - use calculated width with padding
            if (cfg.showABName && cache.assetBundleContent != null) {
                width += s_maxAssetBundleWidth + 14f; // 10f spacing + 4f padding
                // Debug.Log($"s_maxAssetBundleWidth: {s_maxAssetBundleWidth}");
            }
            
            cache.cachedRightPanelWidth = width;
            return width;
        }
        
        
        private void DrawUsageIcons(ref Rect r)
        {
            if (HashUsedByClassesIds == null || HashUsedByClassesIds.Count == 0) return;
            
            var cache = GetDrawCache();
            
            // Pre-calculate valid icons to avoid GUI calls for invalid ones
            var validIcons = new List<GUIContent>(HashUsedByClassesIds.Count);
            
            foreach (int item in HashUsedByClassesIds)
            {
                if (!FR2_Unity.HashClassesNormal.ContainsKey(item)) continue;

                if (!cache.usageIconCache.TryGetValue(item, out GUIContent content))
                {
                    string name = FR2_Unity.HashClassesNormal[item];
                    if (!HashClasses.TryGetValue(item, out Type t))
                    {
                        t = FR2_Unity.GetType(name);
                        if (t != null) HashClasses.Add(item, t);
                    }

                    if (!cacheImage.TryGetValue(name, out content))
                    {
                        content = t == null ? GUIContent.none : FR2_GUIContent.FromType(t, name);
                        cacheImage[name] = content;
                    }
                    cache.usageIconCache[item] = content;
                }

                if (content != null && content != GUIContent.none)
                {
                    validIcons.Add(content);
                }
            }
            
            // Batch draw all valid icons
            if (validIcons.Count > 0)
            {
                try
                {
                    for (var i = 0; i < validIcons.Count; i++)
                    {
                        GUI.Label(GUI2.RightRect(15f, ref r), validIcons[i], GUI2.miniLabelAlignRight);
                    }
                }
                catch (System.Exception e)
                {
                    FR2_LOG.LogWarning(e);
                }
            }
        }
        
        private void ShowContextMenu()
        {
            var menu = new GenericMenu();
            if (extension == ".prefab") menu.AddItem(FR2_GUIContent.FromString("Edit in Scene"), false, EditPrefab);

            menu.AddItem(FR2_GUIContent.FromString("Open"), false, Open);
            menu.AddItem(FR2_GUIContent.FromString("Ping"), false, Ping);
            #if UNITY_2022_3_OR_NEWER
            menu.AddItem(FR2_GUIContent.FromString("Properties..."), false, OpenProperties);
            #endif
            menu.AddItem(FR2_GUIContent.FromString(guid), false, CopyGUID);

            menu.AddSeparator(string.Empty);
            menu.AddItem(FR2_GUIContent.FromString("Copy path"), false, CopyAssetPath);
            menu.AddItem(FR2_GUIContent.FromString("Copy full path"), false, CopyAssetPathFull);

            menu.ShowAsContext();
        }

        internal void EditPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(m_assetPath);
            if (prefab != null)
            {
                PrefabUtility.InstantiatePrefab(prefab);
            }
        }
    }
} 