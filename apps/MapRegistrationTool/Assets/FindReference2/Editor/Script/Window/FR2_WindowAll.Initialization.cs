using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static vietlabs.fr2.FR2_Scope;
using UnityObject = UnityEngine.Object;

namespace vietlabs.fr2
{
    internal partial class FR2_WindowAll
    {
        private void InitializeComponents()
        {
            
            // Initialize UI components first to ensure selection exists before selection manager events
            InitializeUIComponents();
            InitializeNavigationHistory();
            InitializeDrawers();
            InitializeTools();
            InitializeDrawerProperties();
            
            InitTabs();
            InitPanes();
            
            // Initialize selection manager AFTER everything else is ready
            InitializeSelectionManager();
            
            if (FR2_Cache.isReady)
            {
                RefreshActiveTab();
                RefreshFR2View();
            }
            else
            {
                // Debug.LogWarning("FR2 is not Ready just yet!");    
                FR2_Cache.onReady -= RefreshActiveTab;
                FR2_Cache.onReady += RefreshActiveTab;
                FR2_Cache.onReady -= RefreshFR2View;
                FR2_Cache.onReady += RefreshFR2View;
            }
            
            Repaint();
        }

        void RefreshActiveTab()
        {
            FR2_Cache.onReady -= RefreshActiveTab;
            FR2_Cache.onReady -= RefreshFR2View;
            
            // If tabs are not initialized yet, we'll defer this call
            // OnGUI2 will handle calling tab changes when tabs are initialized
            if (tabs == null || toolTabs == null)
            {
                return;
            }
            
            if (settings.toolMode)
            {
                toolTabs.onTabChange?.Invoke();
            }
            else
            {
                tabs.onTabChange?.Invoke();
            }
            
            // If selection was out of sync due to cache not being ready, sync now
            if (isSelectionOutOfSync && selection != null && !selection.isLock)
            {
                selection.SyncFromGlobalSelection();
                RefreshFR2View();
                isSelectionOutOfSync = false;
            }
        }
        

        private void InitializeNavigationHistory()
        {
            if (navigationHistory == null) navigationHistory = new FR2_NavigationHistory();
            navigationHistory.SetWindow(this);
        }

        private void InitializeSelectionManager()
        {
            FR2_SelectionManager.SelectionChanged -= OnSelectionManagerChanged;
            FR2_SelectionManager.SelectionChanged += OnSelectionManagerChanged;
        }

        private void InitializeDrawers()
        {
            UsesDrawer = new FR2_RefDrawer(new FR2_RefDrawer.AssetDrawingConfig
            {
                window = this,
                getSortMode = () => settings.sortMode,
                getGroupMode = () => settings.groupMode,
                showFullPath = settings.showFullPath,
                showFileSize = settings.showFileSize,
                showExtension = settings.showFileExtension,
                showUsageType = settings.showUsageType,
                showAssetBundleName = FR2_Setting.s.displayAssetBundleName,
                showAtlasName = FR2_Setting.s.displayAtlasName,
                showToggle = true,
                shouldShowExtension = () => settings.showFileExtension,
                shouldShowDetailButton = () => true,
                onCacheInvalidated = () => { }
            })
            {
                messageEmpty = "[Selected Assets] are not [USING] (depends on / contains reference to) any other assets!",
                GetContextualEmptyMessage = () => cachedUsesMessage
            };

            UsedByDrawer = new FR2_RefDrawer(new FR2_RefDrawer.AssetDrawingConfig
            {
                window = this,
                getSortMode = () => settings.sortMode,
                getGroupMode = () => settings.groupMode,
                showFullPath = settings.showFullPath,
                showFileSize = settings.showFileSize,
                showExtension = settings.showFileExtension,
                showUsageType = settings.showUsageType,
                showAssetBundleName = FR2_Setting.s.displayAssetBundleName,
                showAtlasName = FR2_Setting.s.displayAtlasName,
                showToggle = true,
                shouldShowExtension = () => settings.showFileExtension,
                shouldShowDetailButton = () => !isFocusingUsedBy,
                onCacheInvalidated = () => { }
            })
            {
                messageEmpty = "[Selected Assets] are not [USED BY] any other assets!",
                GetContextualEmptyMessage = () => cachedUsedByMessage
            };

            AddressableDrawer = new FR2_AddressableDrawer(this, () => settings.sortMode, () => settings.groupMode);

            Duplicated = new FR2_DuplicateTree2(this, () => settings.sortMode, () => settings.toolGroupMode);

            RefInScene = new FR2_RefDrawer(new FR2_RefDrawer.SceneDrawingConfig
            {
                window = this,
                getSortMode = () => settings.sortMode,
                getGroupMode = () => settings.groupMode,
                showFullPath = settings.showFullPath,
                showDetails = true,
                showToggle = true,
                shouldShowExtension = () => settings.showFileExtension,
                shouldShowDetailButton = () => true,
                onCacheInvalidated = () => { }
            })
            {
                messageEmpty = "[Selected Assets] are not [USED BY] any GameObjects in current scene!",
                GetContextualEmptyMessage = () => cachedRefInSceneMessage
            };

            RefSceneInScene = new FR2_RefDrawer(new FR2_RefDrawer.SceneDrawingConfig
            {
                window = this,
                getSortMode = () => settings.sortMode,
                getGroupMode = () => settings.groupMode,
                showFullPath = settings.showFullPath,
                showDetails = true,
                showToggle = true,
                shouldShowExtension = () => settings.showFileExtension,
                shouldShowDetailButton = () => true,
                onCacheInvalidated = () => { }
            })
            {
                messageEmpty = "[Selected GameObjects] are not [USED BY] any GameObjects in current scene!",
                GetContextualEmptyMessage = () => cachedSceneInSceneMessage
            };

            SceneUsesDrawer = new FR2_RefDrawer(new FR2_RefDrawer.SceneDrawingConfig
            {
                window = this,
                getSortMode = () => settings.sortMode,
                getGroupMode = () => settings.groupMode,
                showFullPath = settings.showFullPath,
                showDetails = true,
                showToggle = true,
                shouldShowExtension = () => settings.showFileExtension,
                shouldShowDetailButton = () => true,
                onCacheInvalidated = () => { }
            })
            {
                messageEmpty = "[Selected GameObjects] are not [USING] any GameObjects in current scene!",
                GetContextualEmptyMessage = () => cachedSceneUsesMessage
            };

            SceneToAssetDrawer = new FR2_RefDrawer(new FR2_RefDrawer.AssetDrawingConfig
            {
                window = this,
                getSortMode = () => settings.sortMode,
                getGroupMode = () => settings.groupMode,
                showFullPath = settings.showFullPath,
                showFileSize = settings.showFileSize,
                showExtension = settings.showFileExtension,
                showUsageType = settings.showUsageType,
                showAssetBundleName = FR2_Setting.s.displayAssetBundleName,
                showAtlasName = FR2_Setting.s.displayAtlasName,
                showToggle = true,
                shouldShowExtension = () => settings.showFileExtension,
                shouldShowDetailButton = () => true,
                onCacheInvalidated = () => { }
            })
            {
                messageEmpty = "[Selected GameObjects] are not [USING] any assets!",
                GetContextualEmptyMessage = () => cachedSceneToAssetMessage
            };

            RefUnUse = new FR2_RefDrawer(new FR2_RefDrawer.AssetDrawingConfig
            {
                window = this,
                getSortMode = () => settings.sortMode,
                getGroupMode = () => settings.toolGroupMode,
                showFullPath = settings.showFullPath,
                showFileSize = settings.showFileSize,
                showExtension = settings.showFileExtension,
                showUsageType = settings.showUsageType,
                showAssetBundleName = FR2_Setting.s.displayAssetBundleName,
                showAtlasName = FR2_Setting.s.displayAtlasName,
                showToggle = true,
                shouldShowExtension = () => settings.showFileExtension,
                shouldShowDetailButton = () => !isFocusingUnused,
                onCacheInvalidated = () => { }
            })
            {
                messageEmpty = "Wow! No unused assets found!",
                // RefUnUse doesn't need contextual messages as it's not selection-dependent
            };
        }

        private void InitializeTools()
        {
            UsedInBuild = new FR2_UsedInBuild(this, () => settings.sortMode, () => settings.toolGroupMode);
            MissingReference = new FR2_MissingReference(this, () => settings.sortMode, () => settings.toolGroupMode);
            AssetOrganizer = new FR2_AssetOrganizer(this, () => settings.sortMode, () => settings.toolGroupMode);
            DeleteEmptyFolder = new FR2_DeleteEmptyFolder(this, () => settings.sortMode, () => settings.toolGroupMode);
        }

        private void InitializeUIComponents()
        {
            selection = new FR2_Selection(this, () => settings.sortMode, () => settings.groupMode);
            selection.OnSelectionChanged -= OnLocalSelectionChanged;
            selection.OnSelectionChanged += OnLocalSelectionChanged;
            bookmark = new FR2_Bookmark(this, () => settings.sortMode, () => settings.groupMode);
            
            // Setup bookmark cache invalidation callback - each drawer will invalidate its own cache
            FR2_Bookmark.OnBookmarkChanged = () => {
                UsesDrawer?.InvalidateGroupCache();
                UsedByDrawer?.InvalidateGroupCache();
                RefUnUse?.InvalidateGroupCache();
                RefInScene?.InvalidateGroupCache();
                SceneToAssetDrawer?.InvalidateGroupCache();
                SceneUsesDrawer?.InvalidateGroupCache();
                RefSceneInScene?.InvalidateGroupCache();
            };
            
            // Initial sync with Unity selection - delay until after full initialization
            EditorApplication.delayCall += () =>
            {
                if (selection == null) return;
                if (!FR2_Cache.isReady) return;
                
                selection.SyncFromGlobalSelection();
                isSelectionOutOfSync = false; // Reset flag after initial sync
                RefreshFR2View();
            };
        }

        private void OnLocalSelectionChanged()
        {
            // When local selection changes (user interacts with selection panel), 
            // refresh the Uses/Used By tabs to reflect the current selection
            if (selection != null)
            {
                // Debug.Log($"OnLocalSelectionChanged - Count: {selection.Count}, IsSelectingAsset: {selection.isSelectingAsset}, GuidCount: {selection.guidSet.Count}");
                RefreshFR2View();
            }
        }

        private void InitializeDrawerProperties()
        {
            this.CacheAllDrawers();
            this.RefreshShowFileExtension();
            this.RefreshShowFullPath();
            this.RefreshShowFileSize();
            this.RefreshShowUsageType();
        }

        private void InitPanes()
        {
            sp2 = new FR2_SplitView(this)
            {
                isHorz = false,
                splits = new List<FR2_SplitView.Info>
                {
                    new FR2_SplitView.Info
                    {
                        title = new GUIContent("Scene", FR2_Icon.Scene.image),
                        draw = DrawScene,
                        visible = settings.scene,
                        sizePolicy = FR2_SplitView.Info.SizePolicy.Flexible,
                        minPixel = 100f,
                        GetDynamicTitle = GetScenePanelTitle,
                        GetDrawerDirtyState = IsScenePanelDirty,
                        OnRefresh = () => FR2_SceneCache.Api.ForceRefresh()
                    },
                    new FR2_SplitView.Info
                    {
                        title = new GUIContent("Assets", FR2_Icon.Asset.image),
                        draw = DrawAsset,
                        visible = settings.asset,
                        sizePolicy = FR2_SplitView.Info.SizePolicy.Flexible,
                        minPixel = 100f,
                        GetDynamicTitle = GetAssetPanelTitle,
                        GetDrawerDirtyState = IsAssetPanelDirty,
                        OnRefresh = () => FR2_Cache.IncrementalRefresh()
                    },
                    new FR2_SplitView.Info
                        { title = null, draw = rect => AddressableDrawer.Draw(rect), visible = false }
                }
            };

            sp2.SetupSplitParents();
            sp2.CalculateWeight();

            sp1 = new FR2_SplitView(this)
            {
                isHorz = true,
                splits = new List<FR2_SplitView.Info>
                {
                    new FR2_SplitView.Info
                    {
                        title = null, //new GUIContent("Selection"),
                        draw = DrawSelectionPanel,
                        weight = 0f,
                        visible = settings.selection,
                        sizePolicy = FR2_SplitView.Info.SizePolicy.KeepPixel,
                        preferredPixel = settings.selectionPanelPixel
                    },
                    new FR2_SplitView.Info
                    {
                        title = null,
                        draw = _ => sp2.Draw(_),
                        weight = 1f,
                        visible = true,
                        sizePolicy = FR2_SplitView.Info.SizePolicy.Flexible
                    },
                    new FR2_SplitView.Info
                    {
                        title = new GUIContent("Details", FR2_Icon.Hierarchy.image),
                        draw = DrawDetailsPanel,
                        weight = 0f,
                        visible = settings.details,
                        sizePolicy = FR2_SplitView.Info.SizePolicy.KeepPixel,
                        preferredPixel = settings.detailsPanelPixel
                    },
                    new FR2_SplitView.Info
                    {
                        title = new GUIContent("Bookmark", FR2_Icon.Favorite.image),
                        draw = _ => bookmark.Draw(_),
                        weight = 0f,
                        visible = settings.bookmark,
                        sizePolicy = FR2_SplitView.Info.SizePolicy.KeepPixel,
                        preferredPixel = settings.bookmarkPanelPixel
                    }
                }
            };

            sp1.SetupSplitParents();
            sp1.CalculateWeight();
            sp1.OnSplitterChanged = SavePanelSizes;
            
            Repaint();
            WillRepaint = true;
        }
        
        private void SavePanelSizes()
        {
            // Save panel sizes back to settings for persistence
            // Debug.Log($"[RESIZE_DEBUG] SavePanelSizes called - RESIZE DETECTED");
            for (int i = 0; i < sp1.splits.Count; i++)
            {
                var split = sp1.splits[i];
                if (split.sizePolicy != FR2_SplitView.Info.SizePolicy.KeepPixel) continue;
                
                float oldValue = 0f;
                
                // Map split index to corresponding setting
                switch (i)
                {
                    case 0: // Selection panel
                        oldValue = settings.selectionPanelPixel;
                        settings.selectionPanelPixel = split.preferredPixel;
                        break;
                    case 2: // Details panel  
                        oldValue = settings.detailsPanelPixel;
                        settings.detailsPanelPixel = split.preferredPixel;
                        break;
                    case 3: // Bookmark panel
                        oldValue = settings.bookmarkPanelPixel;
                        settings.bookmarkPanelPixel = split.preferredPixel;
                        break;
                }
                
            }
            
            // Mark the window dirty so Unity saves the serialized settings
            EditorUtility.SetDirty(this);
            // Debug.Log($"[RESIZE_DEBUG] Settings saved - Bookmark: {settings.bookmarkPanelPixel:F1}, Details: {settings.detailsPanelPixel:F1}");
        }

        private void InitTabs()
        {
            bottomTabs = FR2_TabView.Create(this, true,
                new GUIContent(FR2_Icon.Setting.image, "Settings"),
                new GUIContent(FR2_Icon.Ignore.image, "Ignore"),
                new GUIContent(FR2_Icon.Filter.image, "Filter by Type")
            );
            bottomTabs.current = -1;
            bottomTabs.flexibleWidth = false;
            bottomTabs.onTabChange = () => { 
                // Bottom tab changes work directly on FR2 selection - no locks needed
            };

            toolTabs = FR2_TabView.Create(this, false, "Duplicate", "GUID", "Unused", "In Build", "Others");
            toolTabs.current = settings.toolTabIndex;
            toolTabs.onTabChange = () =>
            {
                settings.toolTabIndex = toolTabs.current;

                if (toolTabs.current == 0) // Duplicate
                {
                    if (Duplicated != null)
                    {
                        Duplicated.SetDirty();
                        Duplicated.RefreshSort();
                    }
                }

                if (toolTabs.current == 1) // GUID
                {
                    // GUIDs tool doesn't use drawer system, no action needed
                }

                if (toolTabs.current == 2) // Unused
                {
                    if (RefUnUse != null)
                    {
                        RefUnUse.ResetUnusedAsset(settings.recursiveUnusedScan);
                        RefUnUse.SetDirty();
                        RefUnUse.RefreshSort();
                    }
                }

                if (toolTabs.current == 3) // UsedInBuild
                {
                    if (UsedInBuild != null)
                    {
                        UsedInBuild.SetDirty();
                        UsedInBuild.RefreshSort();
                    }
                }

                if (toolTabs.current == 4) // Others
                {
                    // Others tab has its own internal tab system, no action needed
                }
                
                // Ensure proper group mode restrictions for tools that need them
                if (toolTabs.IsFocusingAny(2, 3)) // Unused or UsedInBuild
                {
                    if (!allowedModes.Contains(settings.toolGroupMode))
                    {
                        settings.toolGroupMode = FR2_RefDrawer.Mode.Type;
                    }
                }
                
                Repaint();
            };
            
            if (FR2_Addressable.asmStatus == FR2_Addressable.ASMStatus.AsmNotFound)
            { // No Addressable
                tabs = FR2_TabView.Create(this, false, // , "Tools"
                    "Uses", "Used By"
                );
            } else
            {
                tabs = FR2_TabView.Create(this, false, // , "Tools"
                    "Uses", "Used By", "Addressables"
                );
            }
            
            tabs.onTabChange = () =>
            {
                settings.mainTabIndex = tabs.current;
                OnTabChange();
            };
            tabs.current = settings.mainTabIndex;
            
            const float IconW = 24f;
            const float LockButtonW = 150f; // Fixed width for lock button with text
            const float BookmarkW = 44f;
            tabs.offsetFirst = IconW * 2 + LockButtonW; // prev, next, lock(with text)
            tabs.offsetLast = IconW * 3 + BookmarkW;

            tabs.callback = new DrawCallback
            {
                BeforeDraw = rect =>
                {
                    if (navigationHistory == null) navigationHistory = new FR2_NavigationHistory();
                    
                    rect.width = IconW;
                    
                    // Previous button
                    bool canGoBack = navigationHistory.CanGoBack;
                    using (GUIEnable(canGoBack))
                    {
                        if (GUI.Button(rect, "<", EditorStyles.toolbarButton))
                        {
                            navigationHistory.GoBack();
                            GUIUtility.ExitGUI(); // Prevent layout errors
                        }
                    }
                    rect.x += IconW;
                    
                    // Next button  
                    bool canGoForward = navigationHistory.CanGoForward;
                    using (GUIEnable(canGoForward))
                    {
                        if (GUI.Button(rect, ">", EditorStyles.toolbarButton))
                        {
                            navigationHistory.GoForward();
                            GUIUtility.ExitGUI(); // Prevent layout errors
                        }
                    }
                    rect.x += IconW;
                    
                    // Lock/SmartLock button area - fixed width with text content
                    rect.width = LockButtonW;
                    
                    {
                        // Normal lock button with selection count
                        UnityObject[] fr2CurrentSelection = GetFR2Selection();
                        int selectionCount = fr2CurrentSelection?.Length ?? 0;
                        
                        // Split the button area - left side for selection info, right side for lock icon
                        Rect selectionRect = rect;
                        selectionRect.width = LockButtonW - 30f; // Leave space for lock icon
                        
                        Rect lockIconRect = rect;
                        lockIconRect.x = selectionRect.xMax;
                        lockIconRect.width = 30f;
                        
                        // Selection info button (clicking toggles selection visibility)
                        string selectionText = selectionCount > 0 ? $"Selection ({selectionCount})" : "Selection";
                        GUIContent selectionContent = new GUIContent(selectionText, "Click to toggle selection panel");
                        
                        // Highlight with subtle yellow if selection is out of sync
                        Color? buttonColor = isSelectionOutOfSync ? new Color(1f, 1f, 0f, 1f) : (Color?)null;
                        using (FR2_Scope.GUIColor(buttonColor))
                        {
                            if (GUI.Button(selectionRect, selectionContent, EditorStyles.toolbarButton))
                            {
                                settings.selection = !settings.selection;
                                sp1.SetSplitVisible(0, settings.selection);
                                WillRepaint = true;
                            }
                        }
                        
                        // Lock icon button (clicking locks/unlocks selection)
                        GUIContent lockIconContent = new GUIContent(
                            selection.isLock ? FR2_Icon.Lock.image : FR2_Icon.Unlock.image,
                            selection.isLock ? "Unlock Selection" : "Lock Selection"
                        );
                        
                        // Set green background when locked, similar to other toggle buttons
                        Color? bgColor = selection.isLock ? new Color(0.7f, 1f, 0.7f, 1f) : (Color?)null;
                        Color contentColor = selection.isLock ? FR2_Theme.Current.IconActiveColor : FR2_Theme.Current.IconColor;
                        
                        using (FR2_Scope.BGColor(bgColor))
                        using (FR2_Scope.ContentColor(contentColor))
                        {
                            if (GUI.Button(lockIconRect, lockIconContent, EditorStyles.toolbarButton))
                            {
                                selection.isLock = !selection.isLock;
                                WillRepaint = true;
                            }
                        }
                    }
                },

                AfterDraw = rect =>
                {
                    rect.xMin = rect.xMax - (IconW * 3 + BookmarkW);
                    rect.width = IconW;

                    // Scene toggle with content indication
                    if (GUI2.ToolbarToggle(ref settings.scene, FR2_Icon.Scene.image, Vector2.zero, "Show / Hide Scene References", rect, ScenePanelHasContent()))
                    {
                        if ((settings.asset == false) && (settings.scene == false))
                        {
                            settings.asset = true;
                            sp2.SetSplitVisible(1, settings.asset);
                        }

                        RefreshPanelVisible();
                        Repaint();
                    }

                    rect.x += IconW;
                    if (GUI2.ToolbarToggle(ref settings.asset, FR2_Icon.Asset.image, Vector2.zero, "Show / Hide Asset References", rect, AssetPanelHasContent()))
                    {
                        if ((settings.asset == false) && (settings.scene == false))
                        {
                            settings.scene = true;
                            sp2.SetSplitVisible(0, settings.scene);
                        }

                        RefreshPanelVisible();
                        Repaint();
                    }

                    rect.x += IconW;
                    if (GUI2.ToolbarToggle(ref settings.details, FR2_Icon.Details.image, Vector2.zero, "Show / Hide Details", rect))
                    {
                        sp1.SetSplitVisible(2, settings.details);
                        Repaint();
                    }

                    rect.x += IconW;
                    {
                        rect.width = BookmarkW;
                        int bookmarkCount = FR2_Bookmark.Count;
                        bool hasBookmarks = bookmarkCount > 0;

                        Color? bgColor = hasBookmarks ? new Color(0.7f, 1f, 0.7f, 1f) : (Color?)null;
                        using (FR2_Scope.BGColor(bgColor))
                        {
                            var bookmarkTitle = FR2_GUIContent.FromTexture(FR2_Icon.Favorite.image, "Show / Hide Bookmarks");
                            bookmarkTitle.text = hasBookmarks
                                ? (bookmarkCount > 99 ? "99+" : $"{bookmarkCount}")
                                : string.Empty;

                            if (GUI2.ToolbarToggle(rect, ref settings.bookmark, bookmarkTitle))
                            {
                                sp1.SetSplitVisible(3, settings.bookmark);
                                Repaint();
                            }
                        }
                    }
                }
            };
        }



    }
}