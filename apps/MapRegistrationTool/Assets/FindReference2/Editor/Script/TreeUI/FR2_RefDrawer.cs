using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;
namespace vietlabs.fr2
{
    internal partial class FR2_RefDrawer : IRefDraw
    {
        internal class RefDrawerConfig
        {
            public IWindow window;
            public Func<Sort> getSortMode;
            public Func<Mode> getGroupMode;
            public bool showFullPath;
            public bool showToggle = true;
            public bool showHighlight = true;
            public Func<bool> shouldShowExtension;
            public Func<bool> shouldShowDetailButton;
            public Action onCacheInvalidated;
        }

        internal class AssetDrawingConfig : RefDrawerConfig
        {
            public bool showFileSize;
            public bool showExtension;
            public bool showUsageType;
            public bool showAssetBundleName;
            public bool showAtlasName;
        }

        internal class SceneDrawingConfig : RefDrawerConfig
        {
            public bool showDetails;
        }

        public static GUIStyle toolbarSearchField;
        public static GUIStyle toolbarSearchFieldCancelButton;
        public static GUIStyle toolbarSearchFieldCancelButtonEmpty;
        private readonly Dictionary<string, BookmarkInfo> gBookmarkCache = new Dictionary<string, BookmarkInfo>();
        private readonly Func<Mode> getGroupMode;

        private readonly Func<Sort> getSortMode;

        internal readonly FR2_TreeUI2.GroupDrawer groupDrawer;

        public readonly List<FR2_Asset> highlight = new List<FR2_Asset>();

        // FILTERING
        private readonly string searchTerm = string.Empty;
        private readonly bool showSearch = true;
        public Action<Rect, FR2_Ref> afterItemDraw;
        public Action<Rect, FR2_Ref> beforeItemDraw;
        public bool caseSensitive = false;

        public Action<Rect, string, int> customDrawGroupLabel;

        public Func<FR2_Ref, string> customGetGroup;

        // STATUS
        private bool dirty;
        public RefDrawerConfig Config { get; private set; }
        private int excludeCount;
        public AssetDrawingConfig AssetConfig { get; private set; }
        public SceneDrawingConfig SceneConfig { get; private set; }

        public string level0Group;
        internal List<FR2_Ref> list;
        public string messageEmpty = "It's empty!";

        public string messageNoRefs = "Do select something!";
        internal Dictionary<string, FR2_Ref> refs;
        private bool selectFilter;
        public bool showDetail;
        private bool showIgnore;
        public float paddingLeft = -4f;
        public float paddingRight = 0f;
        
        // Track whether we have a valid selection (independent of refs count)
        private bool hasValidSelection = false;
        
        // Hook for getting contextual empty message from external source (e.g. window)
        public Func<string> GetContextualEmptyMessage;


        public FR2_RefDrawer(RefDrawerConfig config)
        {
            this.window = config.window;
            this.getSortMode = config.getSortMode;
            this.getGroupMode = config.getGroupMode;
            this.Config = config;
            this.AssetConfig = config as AssetDrawingConfig ?? new AssetDrawingConfig();
            this.SceneConfig = config as SceneDrawingConfig ?? new SceneDrawingConfig();
            groupDrawer = new FR2_TreeUI2.GroupDrawer(DrawGroup, DrawAsset);
        }



        // ORIGINAL
        internal FR2_Ref[] source => FR2_Ref.FromList(list);

        public IWindow window { get; set; }
        
        public bool IsDirty => dirty;
        public bool Draw(Rect rect)
        {
            if (refs == null || refs.Count == 0)
            {
                // Show appropriate message based on whether we have a valid selection
                string message = hasValidSelection ? (GetContextualEmptyMessage?.Invoke() ?? messageEmpty) : messageNoRefs;
                DrawEmpty(rect, message);
                return false;
            }

            if (dirty || list == null) ApplyFilter();

            rect.xMin += paddingLeft;
            rect.xMax += paddingRight;

            if (!groupDrawer.hasChildren)
            {
                DrawEmpty(rect, GetContextualEmptyMessage?.Invoke() ?? messageEmpty);
            } else
            {
                groupDrawer.Draw(rect);
            }
            return false;
        }

        public bool DrawLayout()
        {
            if (refs == null || refs.Count == 0) 
            {
                // Only return false if we have no valid selection, otherwise continue to show empty message
                return !hasValidSelection;
            }

            if (dirty || list == null) ApplyFilter();

            if (groupDrawer.tree != null)
            {
                groupDrawer.tree.itemPaddingLeft = paddingLeft;
                groupDrawer.tree.itemPaddingRight = paddingRight;
            }
            groupDrawer.DrawLayout();
            return false;
        }

        public int ElementCount()
        {
            if (refs == null) return 0;

            return refs.Count;

            // return refs.Where(x => x.Value.depth != 0).Count();
        }

        private void DrawEmpty(Rect rect, string text)
        {
            rect = GUI2.Padding(rect, 2f, 2f);
            rect.height = 45f;

            // Determine message type based on message content
            MessageType messageType = MessageType.Info;
            if (text.Contains("not scanned") || text.Contains("content changed") || text.Contains("refresh cache"))
            {
                messageType = MessageType.Warning;
            }

            EditorGUI.HelpBox(rect, text, messageType);
        }
        public void SetRefs(Dictionary<string, FR2_Ref> dictRefs)
        {
            ValidateRefs(dictRefs);
            refs = dictRefs;
            SetupRefCallbacks();
            
            // When setting refs directly, we consider this a valid selection
            hasValidSelection = true;
            dirty = true;
        }
        
        private void SetupRefCallbacks()
        {
            // Setup callbacks for each ref to point to this drawer
            if (refs != null)
            {
                foreach (var kvp in refs)
                {
                    var rf = kvp.Value;
                    rf.OnCtrlClick = HandleRefCtrlClick;
                    rf.OnAltClick = HandleRefAltClick;
                    rf.OnShiftClick = HandleRefShiftClick;
                }
            }
        }

        void ValidateRefs(Dictionary<string, FR2_Ref> dictRefs)
        {
            var sceneRef = 0;
            var assetRef = 0;
            foreach (var kvp in dictRefs)
            {
                if (kvp.Value.isSceneRef) sceneRef++;
                if (!kvp.Value.isSceneRef) assetRef++;
                if (sceneRef > 0 && assetRef > 0)
                {
                    FR2_LOG.LogWarning("Mixed content???");
                }
            }
        }

        private void SetBookmarkGroup(string groupLabel, bool willbookmark)
        {
            string[] ids = groupDrawer.GetChildren(groupLabel);
            if (ids == null) return; // Handle case where group doesn't exist in groupDict
            
            for (var i = 0; i < ids.Length; i++)
            {
                FR2_Ref rf;
                if (!refs.TryGetValue(ids[i], out rf)) continue;

                if (willbookmark)
                {
                    FR2_Bookmark.Add(rf);
                } else
                {
                    FR2_Bookmark.Remove(rf);
                }
            }

            // Invalidate cache so group toggles reflect the correct state
            InvalidateGroupCache();
        }

        private BookmarkInfo GetBMInfo(string groupLabel)
        {
            BookmarkInfo info = null;
            if (!gBookmarkCache.TryGetValue(groupLabel, out info))
            {
                string[] ids = groupDrawer.GetChildren(groupLabel);

                info = new BookmarkInfo();
                if (ids != null)
                {
                    for (var i = 0; i < ids.Length; i++)
                    {
                        FR2_Ref rf;
                        if (!refs.TryGetValue(ids[i], out rf)) continue;
                        
                        // Only count non-SceneRef items for bookmarking
                        if (!rf.isSceneRef)
                        {
                            info.total++;
                            bool isBM = FR2_Bookmark.Contains(rf);
                            if (isBM) info.count++;
                        }
                    }
                }

                gBookmarkCache.Add(groupLabel, info);
            }

            return info;
        }

        private void DrawToggleGroup(Rect r, string groupLabel)
        {
            BookmarkInfo info = GetBMInfo(groupLabel);
            
            // Hide group toggle if no bookmarkable items (all SceneRefs)
            if (info.total == 0) return;
            
            bool selectAll = info.count == info.total;
            bool originalSelectAll = selectAll;
            r.width = 16f;
            
            Event evt = Event.current;
            bool isMouseOver = r.Contains(evt.mousePosition);
            bool isMouseDown = evt.type == EventType.MouseDown && evt.button == 0 && isMouseOver;
            
            // Handle modifier keys for advanced selection
            if (isMouseDown)
            {
                bool ctrl = Application.platform == RuntimePlatform.OSXEditor ? evt.command : evt.control;
                bool alt = evt.alt;
                
                if (ctrl)
                {
                    // Cmd/Ctrl: Apply same action to all sibling groups
                    bool newState = !selectAll;
                    ApplySameActionToAllSiblingGroups(newState);
                    evt.Use();
                    return;
                }
                else if (alt)
                {
                    // Alt: Invert current state of all siblings
                    InvertAllSiblingGroupsState();
                    evt.Use();
                    return;
                }
            }
            
            // Normal toggle behavior
            if (GUI2.Toggle(r, ref selectAll)) SetBookmarkGroup(groupLabel, selectAll);

            if (!selectAll && (info.count > 0))
            {
                //GUI.DrawTexture(r, EditorStyles.
            }
        }

        private void DrawGroup(Rect r, string label, int childCount)
        {
            if (string.IsNullOrEmpty(label)) label = "(none)";
            
            BookmarkInfo info = GetBMInfo(label);
            bool hasBookmarkableItems = info.total > 0;
            
            if (hasBookmarkableItems)
            {
                DrawToggleGroup(r, label);
                r.xMin += 18f;
            }

            Mode groupMode = getGroupMode();
            if (groupMode == Mode.Folder)
            {
                Texture tex = AssetDatabase.GetCachedIcon("Assets");
                GUI.DrawTexture(new Rect(r.x, r.y, 16f, 16f), tex);
                r.xMin += 16f;
            }

            if (customDrawGroupLabel != null)
            {
                customDrawGroupLabel.Invoke(r, label, childCount);
            } else
            {
                GUIContent lbContent = FR2_GUIContent.FromString(label);
                GUI.Label(r, lbContent, EditorStyles.label);

                Rect cRect = r;
                cRect.x += EditorStyles.label.CalcSize(lbContent).x;
                cRect.y += 1f;
                GUI.Label(cRect, FR2_GUIContent.FromString($"({childCount})"), EditorStyles.miniLabel);
            }

            bool hasMouse = (Event.current.type == EventType.MouseUp) && r.Contains(Event.current.mousePosition);
            if (hasMouse && (Event.current.button == 1))
            {
                var menu = new GenericMenu();
                
                // Only show bookmark options if group has bookmarkable items
                if (hasBookmarkableItems)
                {
                    menu.AddItem(FR2_GUIContent.FromString("Add Bookmark"), false, () => { SetBookmarkGroup(label, true); });
                    menu.AddItem(FR2_GUIContent.FromString("Remove Bookmark"), false, () =>
                    {
                        SetBookmarkGroup(label, false);
                    });
                }
                else
                {
                    menu.AddDisabledItem(FR2_GUIContent.FromString("Add Bookmark (No assets to bookmark)"));
                    menu.AddDisabledItem(FR2_GUIContent.FromString("Remove Bookmark"));
                }

                menu.ShowAsContext();
                Event.current.Use();
            }
        }

        public void DrawDetails(Rect rect)
        {
            Rect r = rect;
            r.xMin += 18f;
            r.height = 18f;

            for (var i = 0; i < highlight.Count; i++)
            {
                highlight[i].Draw(
                    r,
                    new FR2_Asset.FR2_AssetDrawConfig(
                        false,
                    false,
                    false,
                    false,
                    false,
                    false,
                        window,
                        false
                    )
                );
                r.y += 18f;
                r.xMin += 18f;
            }
        }

        private void DrawAsset(Rect r, string guid)
        {
            if (!refs.TryGetValue(guid, out FR2_Ref rf)) return;

            if (rf.isSceneRef)
            {
                if (rf.component == null) return;
                if (!(rf is FR2_SceneRef re)) return;
                beforeItemDraw?.Invoke(r, rf);

                // SceneObjects cannot be bookmarked - no toggle shown, no space reserved
                re.Draw(r, getGroupMode(), SceneConfig.showDetails, Config.showFullPath);
            } else
            {
                beforeItemDraw?.Invoke(r, rf);

                // Draw content first so right-side buttons handle mouse events before the bookmark toggle
                Rect contentRect = r;
                if (Config.showToggle) contentRect.xMin += 32f;

                bool isHighlight = Config.showHighlight && highlight.Contains(rf.asset);

                // if (isHighlight)
                // {
                //     var hlRect = new Rect(-20, r.y, 15f, r.height);
                //     GUI2.Rect(hlRect, GUI2.darkGreen);
                // }

                // Use configurable delegates for behavior
                bool shouldShowExtension = Config.shouldShowExtension?.Invoke() ?? AssetConfig.showExtension;
                bool shouldShowDetailBtn = Config.shouldShowDetailButton?.Invoke() ?? true;
                Action onShowDetail = () => {
                        showDetail = true;
                        highlight.Clear();
                        highlight.Add(rf.asset);

                        FR2_Asset p = rf.addBy;
                        var cnt = 0;
                        while ((p != null) && refs.ContainsKey(p.guid))
                        {
                            highlight.Add(p);
                            FR2_Ref fr2_ref = refs[p.guid];
                            if (fr2_ref != null) p = fr2_ref.addBy;
                            if (++cnt > 100)
                            {
                                FR2_LOG.LogWarning("Break on depth 1000????");
                                break;
                            }
                        }

                        highlight.Sort((item1, item2) =>
                        {
                            int d1 = refs[item1.guid].depth;
                            int d2 = refs[item2.guid].depth;
                            return d1.CompareTo(d2);
                        });
                        Event.current.Use();
                    };
                
                rf.asset.Draw(
                    contentRect,
                    new FR2_Asset.FR2_AssetDrawConfig(
                        isHighlight,
                        Config.showFullPath,
                        AssetConfig.showFileSize,
                        AssetConfig.showAssetBundleName && FR2_Setting.s.displayAssetBundleName,
                        AssetConfig.showAtlasName && FR2_Setting.s.displayAtlasName,
                        AssetConfig.showUsageType,
                        window,
                        shouldShowExtension,
                        shouldShowDetailBtn ? onShowDetail : null
                    )
                );

                if (Config.showToggle)
                {
                    rf.DrawToogleSelect(r);
                }
            }

            afterItemDraw?.Invoke(r, rf);
        }

        private string GetGroup(FR2_Ref rf)
        {
            if (customGetGroup != null) return customGetGroup(rf);

            if (rf.depth == 0) return level0Group;

            if (getGroupMode() == Mode.None) return "(no group)";

            FR2_SceneRef sr = null;
            if (rf.isSceneRef)
            {
                sr = rf as FR2_SceneRef;
                if (sr == null) return null;
            }

            if (!rf.isSceneRef)
            {
                if (rf.asset.IsExcluded)
                {
                    return null; // "(ignored)"
                }
            }

            switch (getGroupMode())
            {
            case Mode.Extension:
                {
                    // if (!rf.isSceneRef) Debug.Log($"Extension: {rf.asset.assetPath} | {rf.asset.extension}");
                    return rf.isSceneRef ? sr.targetType
                        : string.IsNullOrEmpty(rf.asset.extension) ? "(no extension)" : rf.asset.extension;
                }
            case Mode.Type:
                {
                    return rf.isSceneRef ? sr.targetType : FR2_AssetGroupDrawer.FILTERS[rf.type].name;
                }

            case Mode.Folder: return rf.isSceneRef ? sr.scenePath : rf.asset.assetFolder;

            case Mode.Dependency:
                {
                    return rf.depth == 1 ? "Direct Usage" : "Indirect Usage";
                }

            case Mode.Depth:
                {
                    return "Level " + rf.depth;
                }

            case Mode.Atlas: return rf.isSceneRef ? "(not in atlas)" : string.IsNullOrEmpty(rf.asset.AtlasName) ? "(not in atlas)" : rf.asset.AtlasName;
            case Mode.AssetBundle: return rf.isSceneRef ? "(not in assetbundle)" : string.IsNullOrEmpty(rf.asset.AssetBundleName) ? "(not in assetbundle)" : rf.asset.AssetBundleName;
            }

            return "(others)";
        }

        private void SortGroup(List<string> groups)
        {
            groups.Sort((item1, item2) =>
            {
                if (item1.Contains("(")) return 1;
                if (item2.Contains("(")) return -1;

                return string.Compare(item1, item2, StringComparison.Ordinal);
            });
        }

        public FR2_RefDrawer Reset(string[] assetGUIDs, bool isUsage) //, bool isSceneRef
        {
            gBookmarkCache.Clear();
            
            // Set hasValidSelection based on whether we received valid asset GUIDs
            hasValidSelection = assetGUIDs != null && assetGUIDs.Length > 0;
            refs = isUsage ? FR2_Ref.FindUsage(assetGUIDs) : FR2_Ref.FindUsedBy(assetGUIDs);
            SetupRefCallbacks();
            
            // ValidateRefs(dictRefs);
            
            dirty = true;
            if (list != null) list.Clear();
            return this;
        }

        public void Reset(Dictionary<string, FR2_Ref> newRefs)
        {
            if (refs == null) refs = new Dictionary<string, FR2_Ref>();
            refs.Clear();
            ValidateRefs(newRefs);
            
            foreach (KeyValuePair<string, FR2_Ref> kvp in newRefs)
            {
                refs.Add(kvp.Key, kvp.Value);
            }
            SetupRefCallbacks();
            
            // When resetting with refs directly, we consider this a valid selection
            hasValidSelection = true;
            
            dirty = true;
            if (list != null) list.Clear();
        }

        public FR2_RefDrawer Reset(GameObject[] objs, bool findDept, bool findPrefabInAsset)
        {
            // Set hasValidSelection based on whether we received valid GameObjects
            hasValidSelection = objs != null && objs.Length > 0;
            refs = FR2_Ref.FindUsageScene(objs, findDept)
                .Where(item=> !item.Value.isSceneRef)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            var guidss = new List<string>();
            Dictionary<GameObject, HashSet<string>> dependent = FR2_SceneCache.Api.prefabDependencies;
            foreach (GameObject gameObject in objs)
            {
                if (!dependent.TryGetValue(gameObject, out HashSet<string> hash)) continue;
                foreach (string guid in hash)
                {
                    guidss.Add(guid);
                }
            }

            Dictionary<string, FR2_Ref> usageRefs1 = FR2_Ref.FindUsage(guidss.ToArray());
            foreach (KeyValuePair<string, FR2_Ref> kvp in usageRefs1)
            {
                if (refs.ContainsKey(kvp.Key)) continue;

                if (guidss.Contains(kvp.Key)) kvp.Value.depth = 1;

                refs.Add(kvp.Key, kvp.Value);
            }


            if (findPrefabInAsset)
            {
                var guids = new List<string>();
                for (var i = 0; i < objs.Length; i++)
                {
                    string guid = FR2_Unity.GetPrefabParent(objs[i]);
                    if (string.IsNullOrEmpty(guid)) continue;

                    guids.Add(guid);
                }

                Dictionary<string, FR2_Ref> usageRefs = FR2_Ref.FindUsage(guids.ToArray());
                foreach (KeyValuePair<string, FR2_Ref> kvp in usageRefs)
                {
                    if (refs.ContainsKey(kvp.Key)) continue;

                    if (guids.Contains(kvp.Key)) kvp.Value.depth = 1;

                    refs.Add(kvp.Key, kvp.Value);
                }
            }

            SetupRefCallbacks();
            dirty = true;
            if (list != null) list.Clear();

            return this;
        }

        //ref in scene
        public FR2_RefDrawer Reset(string[] assetGUIDs)
        {
            // Set hasValidSelection based on whether we received valid asset GUIDs
            hasValidSelection = assetGUIDs != null && assetGUIDs.Length > 0;
            
            refs = FR2_SceneRef.FindRefInScene(assetGUIDs, true, SetRefInScene);
            SetupRefCallbacks();
            dirty = true;
            if (list != null) list.Clear();

            return this;
        }

        private void SetRefInScene(Dictionary<string, FR2_Ref> data)
        {
            refs = data;
            SetupRefCallbacks();
            dirty = true;
            if (list != null) list.Clear();
        }

        //scene in scene
        public FR2_RefDrawer ResetSceneInScene(GameObject[] objs)
        {
            // Set hasValidSelection based on whether we received valid GameObjects
            hasValidSelection = objs != null && objs.Length > 0;
            
            refs = FR2_SceneCache.FindSceneInScene(objs);
            SetupRefCallbacks();
            dirty = true;
            if (list != null) list.Clear();

            return this;
        }

        public FR2_RefDrawer ResetSceneUseSceneObjects(GameObject[] objs)
        {
            // Set hasValidSelection based on whether we received valid GameObjects
            hasValidSelection = objs != null && objs.Length > 0;
            
            refs = FR2_SceneCache.FindSceneUseSceneObjects(objs);
            SetupRefCallbacks();
            dirty = true;
            if (list != null) list.Clear();

            return this;
        }

        public FR2_RefDrawer ResetUnusedAsset(bool recursive = true)
        {
            List<FR2_Asset> lst = FR2_Cache._inst.ScanUnused(recursive);

            refs = lst.ToDictionary(x => x.guid, x => new FR2_Ref(0, 1, x, null));
            SetupRefCallbacks();
            
            // For unused assets, we always consider this a valid operation
            hasValidSelection = true;
            
            dirty = true;
            if (list != null) list.Clear();

            return this;
        }

        public void RefreshSort()
        {
            if (list == null) return;
            list.RemoveAll(item => item == null ||
                 (item.isSceneRef 
                     ? (item.component == null)
                     : (item.asset == null)
                 ));
            
            if (list.Count == 0) return;
            
            var sortMode = getSortMode();
            list.Sort((r1, r2) =>
            {
                bool isMixed = r1.isSceneRef != r2.isSceneRef;
                if (isMixed)
                {
// #if FR2_DEBUG
// 					var sb = new System.Text.StringBuilder();
// 					sb.Append("r1: " + r1.ToString());
// 					sb.AppendLine();
// 					sb.Append("r2: " +r2.ToString());
// 					FR2_LOG.LogWarning($"Mixed compared!\n{sb}");
// #endif

                    var v1 = r1.isSceneRef ? 1 : 0;
                    var v2 = r2.isSceneRef ? 1 : 0;
                    return v2.CompareTo(v1);
                }

                if (r1.isSceneRef && r2.isSceneRef)
                {
                    var rs1 = (FR2_SceneRef)r1;
                    var rs2 = (FR2_SceneRef)r2;

                    return SortSceneRefs(rs1, rs2, sortMode);
                }

                if (r1.asset == null) return -1;
                if (r2.asset == null) return 1;

                return SortAssetRefs(r1, r2, sortMode);
            });
            
            groupDrawer.Reset(list,
                rf =>
                {
                    if (rf == null) return null;
                    return rf.isSceneRef ? rf.GetSceneObjId() : rf.asset?.guid;
                }, GetGroup, SortGroup);
        }

        public bool isExclueAnyItem()
        {
            return excludeCount > 0;
        }

        private void ApplyFilter()
        {
            dirty = false;

            if (refs == null) return;

            if (list == null)
            {
                list = new List<FR2_Ref>();
            } else
            {
                list.Clear();
            }

            int minScore = searchTerm.Length;

            string term1 = searchTerm;
            if (!caseSensitive) term1 = term1.ToLower();

            string term2 = term1.Replace(" ", string.Empty);

            excludeCount = 0;

            foreach (KeyValuePair<string, FR2_Ref> item in refs)
            {
                FR2_Ref r = item.Value;

                if (FR2_Setting.IsTypeExcluded(r.type))
                {
                    excludeCount++;
                    continue; //skip this one
                }

                if (!showSearch || string.IsNullOrEmpty(searchTerm))
                {
                    r.matchingScore = 0;
                    list.Add(r);
                    continue;
                }

                //calculate matching score
                string name1 = r.isSceneRef ? (r as FR2_SceneRef)?.sceneFullPath : r.asset.assetName;
                if (!caseSensitive) name1 = name1?.ToLower();

                string name2 = name1?.Replace(" ", string.Empty);

                int score1 = FR2_Unity.StringMatch(term1, name1);
                int score2 = FR2_Unity.StringMatch(term2, name2);

                r.matchingScore = Mathf.Max(score1, score2);
                if (r.matchingScore > minScore) list.Add(r);
            }

            RefreshSort();
        }

        public void SetDirty()
        {
            dirty = true;
        }

        
        public void InvalidateGroupCache()
        {
            gBookmarkCache.Clear();
            Config.onCacheInvalidated?.Invoke();
        }
        
        public string GetGroupForRef(FR2_Ref rf)
        {
            return GetGroup(rf);
        }
        
        public void ToggleAllItems()
        {
            if (refs == null) return;
            
            // Determine the new state based on majority rule - only count bookmarkable items
            int bookmarkedCount = 0;
            int totalBookmarkableCount = 0;
            
            foreach (var kvp in refs)
            {
                if (!kvp.Value.isSceneRef) // Only count non-SceneRef items
                {
                    totalBookmarkableCount++;
                    if (FR2_Bookmark.Contains(kvp.Value))
                    {
                        bookmarkedCount++;
                    }
                }
            }
            
            if (totalBookmarkableCount == 0) return; // No bookmarkable items
            
            bool newState = bookmarkedCount < totalBookmarkableCount / 2; // If less than half are bookmarked, bookmark all
            
            foreach (var kvp in refs)
            {
                if (!kvp.Value.isSceneRef) // Only process non-SceneRef items
                {
                    if (newState)
                    {
                        FR2_Bookmark.Add(kvp.Value);
                    }
                    else
                    {
                        FR2_Bookmark.Remove(kvp.Value);
                    }
                }
            }
            
            InvalidateGroupCache();
        }
        
        public void ToggleGroupItems(string groupLabel)
        {
            // Reuse existing SetBookmarkGroup logic with toggle behavior
            string[] ids = groupDrawer.GetChildren(groupLabel);
            if (ids == null) return;
            
            // Toggle each sibling individually - if it's on, turn it off; if it's off, turn it on
            for (var i = 0; i < ids.Length; i++)
            {
                if (!refs.TryGetValue(ids[i], out FR2_Ref rf)) continue;
                
                bool currentState = FR2_Bookmark.Contains(rf);
                if (currentState)
                {
                    FR2_Bookmark.Remove(rf);
                }
                else
                {
                    FR2_Bookmark.Add(rf);
                }
            }
            
            InvalidateGroupCache();
        }
        
        public void SetGroupItemsState(string groupLabel, bool willBookmark)
        {
            // Delegate to existing SetBookmarkGroup method
            SetBookmarkGroup(groupLabel, willBookmark);
        }

        
        private void HandleRefCtrlClick(FR2_Ref rf)
        {
            // Toggle self and set all siblings to the same new state
            bool newState = !rf.isSelected();
            if (newState)
            {
                FR2_Bookmark.Add(rf);
            } else
            {
                FR2_Bookmark.Remove(rf);
            }
            
            // Then set all siblings to the same state
            string groupName = GetGroupForRef(rf);
            if (!string.IsNullOrEmpty(groupName) && groupDrawer.GetChildren(groupName) != null)
            {
                SetGroupItemsState(groupName, newState);
            }
            
            InvalidateGroupCache();
        }
        
        private void HandleRefAltClick(FR2_Ref rf)
        {
            // Toggle all siblings in the same group
            string groupName = GetGroupForRef(rf);
            if (!string.IsNullOrEmpty(groupName) && groupDrawer.GetChildren(groupName) != null)
            {
                ToggleGroupItems(groupName);
            }
        }
        
        private void HandleRefShiftClick(FR2_Ref rf)
        {
            // Toggle all items in all groups
            ToggleAllItems();
        }
        
        public void ClearSelection()
        {
            hasValidSelection = false;
            refs = null;
            if (list != null) list.Clear();
            dirty = true;
        }
        
        private void ApplySameActionToAllSiblingGroups(bool newState)
        {
            if (groupDrawer?.tree?.rootItem?.children == null) return;
            
            foreach (var groupItem in groupDrawer.tree.rootItem.children)
            {
                BookmarkInfo info = GetBMInfo(groupItem.id);
                if (info.total > 0) // Only process groups with bookmarkable items
                {
                    SetBookmarkGroup(groupItem.id, newState);
                }
            }
        }
        
        private void InvertAllSiblingGroupsState()
        {
            if (groupDrawer?.tree?.rootItem?.children == null) return;
            
            foreach (var groupItem in groupDrawer.tree.rootItem.children)
            {
                BookmarkInfo info = GetBMInfo(groupItem.id);
                if (info.total > 0) // Only process groups with bookmarkable items
                {
                    bool currentState = info.count == info.total;
                    SetBookmarkGroup(groupItem.id, !currentState);
                }
            }
        }

        private int SortSceneRefs(FR2_SceneRef rs1, FR2_SceneRef rs2, Sort sortMode)
        {
            switch (sortMode)
            {
                case Sort.Type:
                    int typeCompare = string.Compare(rs1.targetType, rs2.targetType, StringComparison.Ordinal);
                    return typeCompare != 0 ? typeCompare : string.Compare(rs1.sceneFullPath, rs2.sceneFullPath, StringComparison.Ordinal);
                
                case Sort.Path:
                    return string.Compare(rs1.sceneFullPath, rs2.sceneFullPath, StringComparison.Ordinal);
                
                case Sort.Size:
                    // Scene refs don't have file size, sort by path
                    return string.Compare(rs1.sceneFullPath, rs2.sceneFullPath, StringComparison.Ordinal);
                
                default:
                    return string.Compare(rs1.sceneFullPath, rs2.sceneFullPath, StringComparison.Ordinal);
            }
        }

        private int SortAssetRefs(FR2_Ref r1, FR2_Ref r2, Sort sortMode)
        {
            switch (sortMode)
            {
                case Sort.Type:
                    string type1 = r1.asset.extension ?? "";
                    string type2 = r2.asset.extension ?? "";
                    int typeCompare = string.Compare(type1, type2, StringComparison.Ordinal);
                    return typeCompare != 0 ? typeCompare : string.Compare(r1.asset.assetPath, r2.asset.assetPath, StringComparison.Ordinal);
                
                case Sort.Path:
                    return string.Compare(r1.asset.assetPath, r2.asset.assetPath, StringComparison.Ordinal);
                
                case Sort.Size:
                    int sizeCompare = r2.asset.fileSize.CompareTo(r1.asset.fileSize); // Descending order (larger first)
                    return sizeCompare != 0 ? sizeCompare : string.Compare(r1.asset.assetPath, r2.asset.assetPath, StringComparison.Ordinal);
                
                default:
                    return string.Compare(r1.asset.assetPath, r2.asset.assetPath, StringComparison.Ordinal);
            }
        }

        private int SortAsset(string term11, string term12, string term21, string term22, bool swap)
        {
            //			if (term11 == null) term11 = string.Empty;
            //			if (term12 == null) term12 = string.Empty;
            //			if (term21 == null) term21 = string.Empty;
            //			if (term22 == null) term22 = string.Empty;
            int v1 = string.Compare(term11, term12, StringComparison.Ordinal);
            int v2 = string.Compare(term21, term22, StringComparison.Ordinal);
            return swap ? v1 == 0 ? v2 : v1 : v2 == 0 ? v1 : v2;
        }

        public Dictionary<string, FR2_Ref> getRefs()
        {
            return refs;
        }

        internal class BookmarkInfo
        {
            public int count;
            public int total;
        }
    }
}
