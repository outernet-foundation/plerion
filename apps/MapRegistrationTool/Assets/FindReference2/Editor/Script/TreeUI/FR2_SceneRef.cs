using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace vietlabs.fr2
{
    internal class FR2_SceneRef : FR2_Ref
    {
        internal static readonly Dictionary<string, Type> CacheType = new Dictionary<string, Type>();
        private static readonly Dictionary<Type, string> typeNameCache = new Dictionary<Type, string>();
        private static readonly Dictionary<string, string> beautifiedPathCache = new Dictionary<string, string>();

        private static Action<Dictionary<string, FR2_Ref>> onFindRefInSceneComplete;
        private static Dictionary<string, FR2_Ref> refs = new Dictionary<string, FR2_Ref>();
        private static string[] cacheAssetGuids;
        private GUIContent assetNameGC;
        private GUIContent assetTypeGC;

        public Func<bool> drawFullPath;
        public string sceneFullPath = "";
        public string scenePath = "";
        public string targetType;
        
        public List<SceneRefInfo> sourceRefs;
        public List<SceneRefInfo> backwardRefs;
        
        // Cached grouping data - calculated once when refs change
        private List<ComponentGroup> _cachedGroups;
        private float _cachedReferenceIconsWidth;
        private string _cachedSingleRefPropertyPath; // Cached beautified path for single ref
        private float _cachedSingleRefPropertyPathWidth; // Width for single ref
        private bool _groupingDirty = true;
        

        public FR2_SceneRef(int index, int depth, FR2_Asset asset, FR2_Asset by) : base(index, depth, asset, by)
        {
            isSceneRef = true;
            sourceRefs = new List<SceneRefInfo>();
            backwardRefs = new List<SceneRefInfo>();
            // Ensure tooltip always shows full path with proper slashes
            string tooltipPath = asset?.assetPath ?? "Unknown";
            assetNameGC = FR2_GUIContent.FromString(asset?.assetName ?? "Unknown", tooltipPath);
            assetTypeGC = FR2_GUIContent.FromString("");
        }
        
        public FR2_SceneRef(int depth, UnityObject target) : base(0, depth, null, null)
        {
            component = target;
            this.depth = depth;
            isSceneRef = true;
            sourceRefs = new List<SceneRefInfo>();
            backwardRefs = new List<SceneRefInfo>();
            InitializeTargetInfo(target);
        }

        void InitializeTargetInfo(UnityObject target)
        {
            if (target == null)
            {
                targetType = "Missing";
                scenePath = "";
                sceneFullPath = "Missing Object";
                assetNameGC = FR2_GUIContent.FromString("Missing Object", "Object has been destroyed");
                assetTypeGC = FR2_GUIContent.FromString("Missing");
                return;
            }

            if (target is GameObject obj)
            {
                targetType = nameof(GameObject);
                scenePath = FR2_Unity.GetGameObjectPath(obj, false);
                // Add trailing slash if scenePath is not empty
                string pathWithSlash = string.IsNullOrEmpty(scenePath) ? "" : scenePath + "/";
                sceneFullPath = pathWithSlash + obj.name;
                assetNameGC = FR2_GUIContent.FromString(obj.name, sceneFullPath);
                assetTypeGC = GUIContent.none;
            }
            else if (target is Component com)
            {
                targetType = GetCachedTypeName(component.GetType());
                scenePath = FR2_Unity.GetGameObjectPath(com.gameObject, false);
                // Add trailing slash if scenePath is not empty
                string pathWithSlash = string.IsNullOrEmpty(scenePath) ? "" : scenePath + "/";
                sceneFullPath = pathWithSlash + com.gameObject.name;
                assetNameGC = FR2_GUIContent.FromString(com.gameObject.name, sceneFullPath);
                assetTypeGC = FR2_GUIContent.FromString(GetCachedTypeName(component.GetType()));
            }
        }

        static string GetCachedTypeName(Type type)
        {
            if (typeNameCache.TryGetValue(type, out string cachedName)) return cachedName;
            cachedName = type.Name;
            typeNameCache.Add(type, cachedName);
            return cachedName;
        }

        public override bool isSelected()
        {
            // SceneObjects should never be considered bookmarked/selected
            return false;
        }


        public void Draw(Rect r, FR2_RefDrawer.Mode groupMode, bool showDetails, bool drawFullPath = false)
        {
            // r.xMin -= 12f;
            r.xMax -= 4f;
            
            var margin = 2;
            var pingRect = r;
            Rect iconRect = GUI2.LeftRect(16f, ref r);
            
            // Right-click context menu for scene objects
            if ((Event.current.type == EventType.MouseUp) && (Event.current.button == 1) && pingRect.Contains(Event.current.mousePosition))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Open"), false, () => { if (component != null) EditorGUIUtility.PingObject(component); });
                menu.AddItem(new GUIContent("Ping"), false, () => { if (component != null) EditorGUIUtility.PingObject(component); });
#if UNITY_2022_3_OR_NEWER
                menu.AddItem(new GUIContent("Properties..."), false, () => { if (component != null) EditorUtility.OpenPropertyEditor(component); });
#else
                menu.AddDisabledItem(new GUIContent("Properties..."));
#endif
                menu.ShowAsContext();
                Event.current.Use();
            }
            
            var (icon, iconTooltip) = GetTargetIcon();
            
            // Calculate available space for reference icons first (reserve space on the right)
            float referenceIconsWidth = CalculateReferenceIconsWidth();
#if UNITY_2022_3_OR_NEWER
            float reservedRightSpace = referenceIconsWidth + 18f + 4f; // reference icons + P button + margin
#else
            float reservedRightSpace = referenceIconsWidth + 4f; // reference icons + margin
#endif
            float availableWidth = r.width - reservedRightSpace;
            
            // Calculate sizes based on whether we're showing full path
            float pathW = 0f;
            float nameW = 0f;
            
            // Sophisticated space contracting flow for scene references
            nameW = EditorStyles.label.CalcSize(assetNameGC).x;
            float typeW = EditorStyles.miniLabel.CalcSize(assetTypeGC).x;
            
            SpaceAllocationResult spaceAllocation;
            if (drawFullPath && !string.IsNullOrEmpty(scenePath))
            {
                spaceAllocation = CalculateOptimalSpaceAllocation(availableWidth, nameW, typeW, margin);
                pathW = spaceAllocation.pathWidth;
            }
            else
            {
                // When path is not drawn, always show all elements - no space constraints
                spaceAllocation = new SpaceAllocationResult
                {
                    pathWidth = 0f,
                    elementsToShow = DisplayElements.All
                };
            }
            
            // Draw elements and calculate actual positions
            float currentX = r.x; // Start immediately after icon (r already adjusted by GUI2.LeftRect)
            float actualPathW = 0f;
            
            // Draw path and get actual width used
            if (drawFullPath && pathW > 0)
            {
                Rect pathRect = new Rect(currentX, r.y, pathW, r.height);
                actualPathW = DrawScenePath(pathRect, drawFullPath);
                currentX += actualPathW;
            }
            
            // Draw object name immediately after path
            Rect nameRect = new Rect(currentX, r.y, nameW, r.height);
            currentX += nameW;
            
            // Draw type only if space allocation allows it
            Rect typeRect = new Rect();
            bool showComponentType = (spaceAllocation.elementsToShow & DisplayElements.ComponentType) != 0;
            if (showComponentType)
            {
                typeRect = new Rect(currentX, r.y, typeW, r.height);
                currentX += typeW;
            }
            
            float totalUsedWidth = 16f + actualPathW + nameW + (showComponentType ? typeW : 0f);
            pingRect.width = totalUsedWidth;
            
            DrawPingRect(pingRect);
            DrawTargetIcon(iconRect, icon, iconTooltip);
            DrawTargetName(nameRect, assetNameGC);
            
            if (showComponentType)
            {
                DrawTargetType(typeRect, assetTypeGC);
            }

#if UNITY_2022_3_OR_NEWER
            // Draw P only on hover and repaint on mouse move for responsiveness
            Rect rowRect = new Rect(r.x, r.y, r.width, FR2_Theme.Current.TreeItemHeight);
            bool isHover = rowRect.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.MouseMove)
            {
                var focused = EditorWindow.focusedWindow;
                if (focused != null) focused.Repaint();
            }
            
            var propRect = new Rect(r.xMax - 18f, r.y, 19f, r.height);
            r.width -= 20f;
            
            if (isHover)
            {
                propRect.yMax -= 2f;
                if (GUI.Button(propRect, FR2_GUIContent.FromString("P", "Open Properties"), EditorStyles.miniButton))
                {
                    UnityObject targetForProperties = GetSourceComponentForProperties();
                    if (targetForProperties != null) 
                    {
                        EditorUtility.OpenPropertyEditor(targetForProperties);
                        GUIUtility.ExitGUI(); // Prevent layout errors after opening property editor
                    }
                }
            }
#endif

            DrawReferenceIcons(r);
        }

        (Texture icon, string tooltip) GetTargetIcon()
        {
            if (component == null) return (null, "");
            
            if (component is GameObject go)
                return (EditorGUIUtility.ObjectContent(go, typeof(GameObject)).image, "GameObject");
            if (component is Component comp)
            {
                var compType = comp.GetType();
                return (EditorGUIUtility.ObjectContent(comp, compType).image, GetCachedTypeName(compType));
            }
            
            var componentType = component.GetType();
            return (EditorGUIUtility.ObjectContent(component, componentType).image, GetCachedTypeName(componentType));
        }

        UnityObject GetSourceComponentForProperties()
        {
            if (sourceRefs?.Count > 0)
            {
                return sourceRefs[0].sourceComponent;
            }
            if (backwardRefs?.Count > 0)
            {
                return backwardRefs[0].sourceComponent;
            }
            return component;
        }

        void DrawTargetIcon(Rect iconRect, Texture icon, string iconTooltip)
        {
            if (icon != null)
            {
                var iconContent = FR2_GUIContent.FromTexture(icon, iconTooltip);
                GUI.Label(iconRect, iconContent, EditorStyles.label);
            }
        }

        [System.Flags]
        enum DisplayElements
        {
            None = 0,
            ComponentType = 1 << 0,
            ClassName = 1 << 1,
            PropertyPath = 1 << 2,
            All = ComponentType | ClassName | PropertyPath
        }
        
        struct SpaceAllocationResult
        {
            public float pathWidth;
            public DisplayElements elementsToShow;
        }
        
        SpaceAllocationResult CalculateOptimalSpaceAllocation(float availableWidth, float nameW, float typeW, float margin)
        {
            // 100% CORRECT MATH - Progressive element hiding strategy
            // Note: availableWidth already excludes right-side elements AND icon (r already adjusted)
            float typeSpacing = 4f;
            
            // Calculate ideal path width (full text width)
            var fullPathContent = FR2_GUIContent.FromString(scenePath + "/");
            float idealPathW = EditorStyles.miniLabel.CalcSize(fullPathContent).x;
            
            // LEVEL 0: Try with ALL elements visible - use EXACT math
            float leftSideWidth = nameW + typeW + typeSpacing; // No icon - already excluded from availableWidth
            float spaceNeededForIdealPath = leftSideWidth + idealPathW;
            
            if (spaceNeededForIdealPath <= availableWidth)
            {
                // Perfect fit - show everything with ideal path width
                return new SpaceAllocationResult
                {
                    pathWidth = idealPathW,
                    elementsToShow = DisplayElements.All
                };
            }
            
            // LEVEL 1: Hide component type - recalculate with EXACT math
            leftSideWidth = nameW; // No type, no spacing, no icon
            spaceNeededForIdealPath = leftSideWidth + idealPathW;
            
            if (spaceNeededForIdealPath <= availableWidth)
            {
                // Can fit ideal path without component type
                return new SpaceAllocationResult
                {
                    pathWidth = idealPathW,
                    elementsToShow = DisplayElements.ClassName | DisplayElements.PropertyPath
                };
            }
            
            // FINAL: Use all remaining space for path with ellipsis - EXACT math
            float remainingSpaceForPath = availableWidth - leftSideWidth;
            return new SpaceAllocationResult
            {
                pathWidth = Mathf.Max(20f, remainingSpaceForPath),
                elementsToShow = DisplayElements.ClassName | DisplayElements.PropertyPath // Keep these for reference system
            };
        }
        
        float CalculateReferenceIconsWidthWithoutClassName()
        {
            // Calculate reference icons width without class names
            // This is a simplified version - would need to implement based on actual reference drawing logic
            return _cachedReferenceIconsWidth * 0.7f; // Approximate reduction
        }
        
        float CalculateReferenceIconsWidthIconsOnly()
        {
            // Calculate reference icons width with only icons (no text)
            // This is a simplified version - would need to implement based on actual reference drawing logic
            return _cachedReferenceIconsWidth * 0.4f; // Approximate reduction to icons only
        }

        float DrawScenePath(Rect pathRect, bool drawFullPath)
        {
            if (!drawFullPath || string.IsNullOrEmpty(scenePath)) return 0f;
            
            using (FR2_Scope.GUIColor(FR2_Theme.Current.SecondaryTextColor))
            {
                return ClippedLabel.Draw(pathRect, scenePath + "/", EditorStyles.miniLabel);
            }
        }

        void DrawTargetName(Rect nameRect, GUIContent displayContent)
        {
            if (isSelected())
            {
                Color c = GUI.color;
                GUI.color = GUI.skin.settings.selectionColor;
                GUI.DrawTexture(nameRect, EditorGUIUtility.whiteTexture);
                GUI.color = c;
            }
            GUI.Label(nameRect, displayContent, EditorStyles.label);
        }

        void DrawTargetType(Rect typeRect, GUIContent typeContent)
        {
            if (!string.IsNullOrEmpty(typeContent.text))
            {
                Color c = GUI.color;
                GUI.color = FR2_Theme.Current.SecondaryTextColor;
                GUI.Label(typeRect, typeContent, EditorStyles.miniLabel);
                GUI.color = c;
            }
        }

        class ComponentGroup
        {
            public Type componentType;
            public List<SceneRefInfo> refs;
            public int count;
            
            // Cached layout data
            public float countWidth;
            public float iconWidth;
            public float nameWidth;
            public float totalWidth;
            public string displayName;
            
            // Cached GUI data
            public GUIContent iconContent;
            public GUIContent countContent;
            public GUIContent nameContent;
            public string multiLineTooltip;
            
            // Cycling state
            public int cyclingIndex;
        }
        
        public void MarkGroupingDirty()
        {
            _groupingDirty = true;
        }
        
        void EnsureGroupingCached()
        {
            if (!_groupingDirty && _cachedGroups != null) return;
            
            var refInfos = sourceRefs?.Count > 0 ? sourceRefs : backwardRefs;
            if (refInfos == null || refInfos.Count == 0)
            {
                _cachedGroups = new List<ComponentGroup>();
                _cachedReferenceIconsWidth = 0f;
                _groupingDirty = false;
                return;
            }
            
            _cachedGroups = GroupReferencesByComponentType(refInfos);
            _cachedReferenceIconsWidth = CalculateReferenceIconsWidthInternal(_cachedGroups, refInfos);
            _groupingDirty = false;
        }

        List<ComponentGroup> GroupReferencesByComponentType(List<SceneRefInfo> refInfos)
        {
            var groups = new List<ComponentGroup>();
            var groupDict = new Dictionary<Type, List<SceneRefInfo>>();
            
            foreach (var refInfo in refInfos)
            {
                if (refInfo.sourceComponent == null) continue;
                
                var componentType = refInfo.sourceComponent.GetType();
                if (!groupDict.ContainsKey(componentType))
                {
                    groupDict[componentType] = new List<SceneRefInfo>();
                }
                groupDict[componentType].Add(refInfo);
            }
            
            float iconWidth = 18f;
            
            foreach (var kvp in groupDict)
            {
                var componentType = kvp.Key;
                var refs = kvp.Value;
                var count = refs.Count;
                var displayName = GetCachedTypeName(componentType);
                
                // Calculate layout sizes ONCE
                float countWidth = 0f;
                GUIContent countContent = null;
                if (count > 1)
                {
                    var countText = $"({count})";
                    countContent = FR2_GUIContent.FromString(countText);
                    countWidth = EditorStyles.miniLabel.CalcSize(countContent).x;
                }
                
                // Create cached GUIContent objects
                var nameContent = FR2_GUIContent.FromString(displayName);
                float nameWidth = EditorStyles.label.CalcSize(nameContent).x;
                float totalWidth = countWidth + (countWidth > 0 ? 2f : 0f) + iconWidth; // NO nameWidth - added separately for single group
                
                // Create icon content (will be set with proper icon in DrawGroup)
                var iconContent = EditorGUIUtility.ObjectContent(refs[0].sourceComponent, componentType);
                
                // Create multi-line tooltip: "propertyPath --> target reference name" per line
                var tooltipLines = new List<string>();
                foreach (var refInfo in refs)
                {
                    var propertyPath = refInfo.propertyPath;
                    var targetName = refInfo.target != null ? refInfo.target.name : "null";
                    tooltipLines.Add($"{propertyPath} --> {targetName}");
                }
                var multiLineTooltip = string.Join("\n", tooltipLines);
                
                groups.Add(new ComponentGroup
                {
                    componentType = componentType,
                    refs = refs,
                    count = count,
                    countWidth = countWidth,
                    iconWidth = iconWidth,
                    nameWidth = nameWidth,
                    totalWidth = totalWidth,
                    displayName = displayName,
                    iconContent = iconContent,
                    countContent = countContent,
                    nameContent = nameContent,
                    multiLineTooltip = multiLineTooltip,
                    cyclingIndex = -1 // Start at -1 so first click shows index 0
                });
            }
            
            return groups;
        }

        float CalculateReferenceIconsWidth()
        {
            EnsureGroupingCached();
            return _cachedReferenceIconsWidth;
        }
        
        float CalculateReferenceIconsWidthInternal(List<ComponentGroup> groups, List<SceneRefInfo> refInfos)
        {
            if (groups == null || groups.Count == 0) return 0f;
            
            bool isSingleRef = refInfos.Count == 1;
            
            if (isSingleRef)
            {
                // Single reference: calculate and cache EVERYTHING
                var refInfo = refInfos[0];
                _cachedSingleRefPropertyPath = GetCachedBeautifiedPath(refInfo.propertyPath);
                _cachedSingleRefPropertyPathWidth = string.IsNullOrEmpty(_cachedSingleRefPropertyPath) ? 0f : EditorStyles.miniLabel.CalcSize(FR2_GUIContent.FromString(_cachedSingleRefPropertyPath)).x;
                var group = groups[0];
                float spacing = _cachedSingleRefPropertyPathWidth > 0 ? 4f : 0f;
                return _cachedSingleRefPropertyPathWidth + spacing + group.iconWidth + group.nameWidth;
            }
            
            // Multiple refs: sum up group widths
            if (groups.Count == 1)
            {
                // Single group: include name width
                return groups[0].totalWidth + groups[0].nameWidth;
            }
            
            // Multiple groups: include icon + count + name widths
            float totalWidth = 0f;
            foreach (var group in groups)
            {
                totalWidth += group.totalWidth + group.nameWidth;
            }
            
            return totalWidth;
        }

        void DrawReferenceIcons(Rect r)
        {
            if (sourceRefs?.Count > 0)
            {
                DrawReferenceInformation(r, sourceRefs);
            }
            else if (backwardRefs?.Count > 0)
            {
                DrawReferenceInformation(r, backwardRefs);
            }
        }

        void DrawGroup(Rect r, float currentX, ComponentGroup group, SceneRefInfo refInfo, string countText, string propertyPathText, float propertyPathWidth, string tooltipText)
        {
            float startX = currentX;
            
            // Draw count if provided (use cached content)
            if (!string.IsNullOrEmpty(countText) && group.countContent != null)
            {
                var countRect = new Rect(currentX, r.y, group.countWidth, r.height);
                using (FR2_Scope.GUIColor(FR2_Theme.Current.SecondaryTextColor))
                {
                    GUI.Label(countRect, group.countContent, EditorStyles.miniLabel);
                }
                currentX += group.countWidth + 2f;
            }
            
            // Draw property path if provided
            if (!string.IsNullOrEmpty(propertyPathText))
            {
                var pathRect = new Rect(currentX, r.y, propertyPathWidth, r.height);
                using (FR2_Scope.GUIColor(FR2_Theme.Current.SecondaryTextColor))
                {
                    GUI.Label(pathRect, propertyPathText, EditorStyles.miniLabel);
                }
                currentX += propertyPathWidth + 4f;
            }
            
            // Draw icon (use cached content)
            var iconRect = new Rect(currentX, r.y, group.iconWidth, r.height);
            if (group.iconContent?.image != null) 
            {
                GUI.DrawTexture(iconRect, group.iconContent.image);
            }
            currentX += group.iconWidth;
            
            // Draw component name if no property path (use cached content)
            if (string.IsNullOrEmpty(propertyPathText))
            {
                var nameRect = new Rect(currentX, r.y, group.nameWidth, r.height);
                GUI.Label(nameRect, group.nameContent, EditorStyles.label);
            }
            
            // Tooltip + click handler with cycling support
            float totalWidth;
            if (!string.IsNullOrEmpty(propertyPathText))
            {
                totalWidth = propertyPathWidth + 4f + group.iconWidth + group.nameWidth;
            }
            else
            {
                totalWidth = group.totalWidth + group.nameWidth;
            }
            
            var tooltipRect = new Rect(startX, r.y, totalWidth, r.height);
            // Use multi-line tooltip for groups with multiple items
            var finalTooltip = group.count > 1 ? group.multiLineTooltip : tooltipText;
            if (GUI.Button(tooltipRect, new GUIContent("", finalTooltip), GUIStyle.none))
            {
                // Cycle to next item in the group using the group's cycling index
                int currentIndex;
                if (group.count > 1)
                {
                    group.cyclingIndex = (group.cyclingIndex + 1) % group.count;
                    currentIndex = group.cyclingIndex;
                }
                else
                {
                    currentIndex = 0; // Single item, always use index 0
                }
                
                // Get the item to highlight based on current index
                var targetRef = group.refs[currentIndex];
                EditorGUIUtility.PingObject(targetRef.sourceComponent);
                FR2_Unity.PingAndHighlight(targetRef.sourceComponent, targetRef.propertyPath);
                Event.current.Use();
            }
        }

        void DrawReferenceInformation(Rect r, List<SceneRefInfo> refInfos)
        {
            if (refInfos == null || refInfos.Count == 0) return;
            
            // Use cached groups and widths - ZERO recalculation!
            EnsureGroupingCached();
            var groups = _cachedGroups;
            
            bool isSingleRef = refInfos.Count == 1;
            float startX = r.x + r.width - _cachedReferenceIconsWidth;
            float currentX = startX;
            
            if (isSingleRef)
            {
                // Single reference - show both property path AND component name
                var refInfo = refInfos[0];
                var group = groups[0];
                
                // Draw property path first (if exists)
                if (!string.IsNullOrEmpty(_cachedSingleRefPropertyPath))
                {
                    var pathRect = new Rect(currentX, r.y, _cachedSingleRefPropertyPathWidth, r.height);
                    using (FR2_Scope.GUIColor(FR2_Theme.Current.SecondaryTextColor))
                    {
                        GUI.Label(pathRect, _cachedSingleRefPropertyPath, EditorStyles.miniLabel);
                    }
                    currentX += _cachedSingleRefPropertyPathWidth + 4f;
                }
                
                // Draw icon
                var iconRect = new Rect(currentX, r.y, group.iconWidth, r.height);
                var icon = EditorGUIUtility.ObjectContent(refInfo.sourceComponent, group.componentType).image;
                if (icon != null) GUI.DrawTexture(iconRect, icon);
                currentX += group.iconWidth;
                
                // Always draw component name for single reference
                var nameRect = new Rect(currentX, r.y, group.nameWidth, r.height);
                GUI.Label(nameRect, group.displayName, EditorStyles.label);
                
                // Tooltip + click handler for the entire area
                var tooltipText = refInfo.IsBackwardRef ? refInfo.propertyPath : $"{group.displayName}.{refInfo.propertyPath}";
                var totalWidth = (_cachedSingleRefPropertyPathWidth > 0 ? _cachedSingleRefPropertyPathWidth + 4f : 0f) + group.iconWidth + group.nameWidth;
                var tooltipRect = new Rect(r.x + r.width - _cachedReferenceIconsWidth, r.y, totalWidth, r.height);
                if (GUI.Button(tooltipRect, new GUIContent("", tooltipText), GUIStyle.none))
                {
                    EditorGUIUtility.PingObject(refInfo.sourceComponent);
                    FR2_Unity.PingAndHighlight(refInfo.sourceComponent, refInfo.propertyPath);
                    Event.current.Use();
                    // GUIUtility.ExitGUI(); // Prevent layout errors after ping/highlight
                }
            }
            else if (groups.Count == 1)
            {
                // Single group
                var group = groups[0];
                var refInfo = group.refs[0];
                var countText = group.countWidth > 0 ? $"({group.count})" : null;
                var tooltipText = $"{group.displayName} ({group.count} references)";
                DrawGroup(r, currentX, group, refInfo, countText, null, 0f, tooltipText);
            }
            else
            {
                // Multiple groups
                foreach (var group in groups)
                {
                    var refInfo = group.refs[0];
                    var countText = group.countWidth > 0 ? $"({group.count})" : null;
                    var tooltipText = group.count > 1 ? $"{group.displayName} ({group.count} references)" : $"{group.displayName}.{refInfo.propertyPath}";
                    DrawGroup(r, currentX, group, refInfo, countText, null, 0f, tooltipText);
                    currentX += group.totalWidth;
                }
            }
        }


        static string GetCachedBeautifiedPath(string propertyPath)
        {
            if (string.IsNullOrEmpty(propertyPath)) return "";
            if (beautifiedPathCache.TryGetValue(propertyPath, out string cachedPath)) return cachedPath;
            
            string result = propertyPath;
            result = result.Replace(".Array.data[", "[");
            result = result.Replace("].Array.data[", "][");
            
            beautifiedPathCache.Add(propertyPath, result);
            return result;
        }

        void DrawPingRect(Rect pingRect)
        {
            // Bigger Rect
            pingRect.yMin -= 1;
            pingRect.yMax += 1;

            if (Event.current.type != EventType.MouseDown) return;
            if (Event.current.button != 0) return;
            if (!pingRect.Contains(Event.current.mousePosition)) return;
            
            EditorGUIUtility.PingObject(component);
            Event.current.Use();
        }

        public static Dictionary<string, FR2_Ref> FindSceneUseSceneObjects(GameObject[] targets)
        {
            return FR2_SceneCache.FindSceneUseSceneObjects(targets);
        }

        public static Dictionary<string, FR2_Ref> FindSceneBackwardReferences(GameObject[] targets)
        {
            return FR2_SceneCache.FindSceneBackwardReferences(targets);
        }

        public static Dictionary<string, FR2_Ref> FindSceneInScene(GameObject[] targets)
        {
            return FR2_SceneCache.FindSceneInScene(targets);
        }

        public static Dictionary<string, FR2_Ref> FindRefInScene(
            string[] assetGUIDs, bool depth,
            Action<Dictionary<string, FR2_Ref>> onComplete)
        {
            cacheAssetGuids = assetGUIDs;
            onFindRefInSceneComplete = onComplete;
            if (FR2_SceneCache.isReady)
            {
                FindRefInScene();
            } else
            {
                FR2_SceneCache.onReady -= FindRefInScene;
                FR2_SceneCache.onReady += FindRefInScene;
            }

            return refs;
        }

        private static void FindRefInScene()
        {
            if (refs == null) refs = new Dictionary<string, FR2_Ref>();
            else refs.Clear(); // Reuse existing dictionary
            
            for (var i = 0; i < cacheAssetGuids.Length; i++)
            {
                FR2_Asset asset = FR2_Cache.GetAsset(cacheAssetGuids[i]);
                if (asset == null) continue;

                Add(refs, asset, 0);
                ApplyFilter(refs, asset);
            }

            if (onFindRefInSceneComplete != null) onFindRefInSceneComplete(refs);
            FR2_SceneCache.onReady -= FindRefInScene;
        }

        private static void ApplyFilter(Dictionary<string, FR2_Ref> refs, FR2_Asset asset)
        {
            string targetPath = AssetDatabase.GUIDToAssetPath(asset.guid);
            if (string.IsNullOrEmpty(targetPath)) return;

            if (targetPath != asset.assetPath) asset.MarkAsMoved();
            var target = AssetDatabase.LoadAssetAtPath(targetPath, typeof(UnityObject));
            if (target == null) return;

            if (target is GameObject)
            {
                // Use prefabDependencies to find only prefab instance roots that depend on this prefab
                // This ensures children of prefab instances are not included
                Dictionary<GameObject, HashSet<string>> dependencies = FR2_SceneCache.Api.prefabDependencies;
                foreach (var kvp in dependencies)
                {
                    if (kvp.Value.Contains(asset.guid))
                    {
                        Add(refs, kvp.Key, 1);
                    }
                }
            }

            // Search through all cached components for references to this asset
            foreach (var cacheEntry in FR2_SceneCache.Api.cache)
            {
                foreach (var hashValue in cacheEntry.Value)
                {
                    if (targetPath == AssetDatabase.GetAssetPath(hashValue.target))
                    {
                        Add(refs, cacheEntry.Key, 1);
                        break;
                    }
                }
            }
        }

        private static void Add(Dictionary<string, FR2_Ref> refs, FR2_Asset asset, int depth)
        {
            string targetId = asset.guid;
            if (!refs.ContainsKey(targetId)) refs.Add(targetId, new FR2_Ref(0, depth, asset, null));
        }

        private static void Add(Dictionary<string, FR2_Ref> refs, UnityObject target, int depth)
        {
            if (target == null) return;
            var targetId = target.GetInstanceID().ToString();
            if (!refs.ContainsKey(targetId)) refs.Add(targetId, new FR2_SceneRef(depth, target));
        }
    }
    

    

}
