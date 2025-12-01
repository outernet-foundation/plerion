using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static vietlabs.fr2.FR2_Addressable;

namespace vietlabs.fr2
{
    internal class FR2_AddressableDrawer : IRefDraw
    {
        private const string AUTO_DEPEND_TITLE = "(Auto dependency)";

        private readonly Dictionary<ASMStatus, string> AsmMessage = new Dictionary<ASMStatus, string>
        {
            { ASMStatus.None, "-" },
            { ASMStatus.AsmNotFound, "Addressable Package not imported!" },
            { ASMStatus.TypeNotFound, "Addressable Classes not found (addressable library code changed?)!" },
            { ASMStatus.FieldNotFound, "Addressable Fields not found (addressable library code changed?)!" },
            { ASMStatus.AsmOK, "-" }
        };

        internal readonly FR2_RefDrawer drawer;
        internal readonly Dictionary<string, AddressInfo> map = new Dictionary<string, AddressInfo>();

        private readonly Dictionary<ProjectStatus, string> ProjectStatusMessage = new Dictionary<ProjectStatus, string>
        {
            { ProjectStatus.None, "-" },
            { ProjectStatus.NoSettings, "No Addressables Settings found!\nOpen [Window/Asset Management/Addressables/Groups] to create new Addressables Settings!\n \n" },
            { ProjectStatus.NoGroup, "No AssetBundle Group created!" },
            { ProjectStatus.Ok, "-" }
        };
        private bool dirty;
        internal List<string> groups;
        // internal float maxWidth;
        internal Dictionary<string, FR2_Ref> refs;

        public FR2_AddressableDrawer(IWindow window, Func<FR2_RefDrawer.Sort> getSortMode, Func<FR2_RefDrawer.Mode> getGroupMode)
        {
            this.window = window;
            drawer = new FR2_RefDrawer(new FR2_RefDrawer.AssetDrawingConfig
            {
                window = window,
                getSortMode = getSortMode,
                getGroupMode = getGroupMode,
                showFullPath = false,
                showFileSize = true,
                showExtension = true,
                showUsageType = false,
                showAssetBundleName = false,
                showAtlasName = false,
                shouldShowDetailButton = () => false // Disable detail buttons in addressable drawer
            })
            {
                messageNoRefs = "No Addressable Asset",
                messageEmpty = "No Addressable Asset",
                customGetGroup = GetGroup,

                customDrawGroupLabel = DrawGroupLabel,
                beforeItemDraw = BeforeDrawItem,
                afterItemDraw = AfterDrawItem
            };

            dirty = true;
            drawer.SetDirty();
        }

        public IWindow window { get; set; }


        public int ElementCount()
        {
            return refs?.Count ?? 0;
        }

        public bool Draw(Rect rect)
        {
            if (dirty) RefreshView();
            if (refs == null) return false;

            rect.yMax -= 24f;
            bool result = drawer.Draw(rect);

            Rect btnRect = rect;
            btnRect.xMin = btnRect.xMax - 24f;
            btnRect.yMin = btnRect.yMax;
            btnRect.height = 24f;

            if (GUI.Button(btnRect, FR2_Icon.Refresh.image))
            {
                FR2_Addressable.Scan();
                RefreshView();
            }

            return result;
        }

        public bool DrawLayout()
        {
            if (dirty) RefreshView();
            return drawer.DrawLayout();
        }

        private string GetGroup(FR2_Ref rf)
        {
            return rf.group;
        }

        private void DrawGroupLabel(Rect r, string label, int childCount)
        {
            GUI.Label(r, FR2_GUIContent.FromString(label), EditorStyles.boldLabel);
        }

        private void BeforeDrawItem(Rect r, FR2_Ref rf)
        {
        }

        private void AfterDrawItem(Rect r, FR2_Ref rf)
        {
            string guid = rf.asset.guid;
            if (!map.TryGetValue(guid, out AddressInfo address))
            {
                Color c2 = GUI.contentColor;
                c2.a = 1f;
                GUI.contentColor = c2;
                return;
            }

            Color c = GUI.contentColor;
            Color c1 = c;
            c1.a = 0.5f;

            GUI.contentColor = c1;
            {
                // Calculate and update max file size width
                GUIContent fileSizeContent = FR2_GUIContent.FromString(FR2_Helper.GetfileSizeString(rf.asset.fileSize));
                float currentFileSizeWidth = EditorStyles.miniLabel.CalcSize(fileSizeContent).x;
                if (currentFileSizeWidth > FR2_Asset.s_maxFileSizeWidth)
                {
                    FR2_Asset.s_maxFileSizeWidth = currentFileSizeWidth;
                }
                
                // Calculate and update max addressable width
                GUIContent addressContent = FR2_GUIContent.FromString(address.address);
                float currentAddressableWidth = EditorStyles.miniLabel.CalcSize(addressContent).x;
                if (currentAddressableWidth > FR2_Asset.s_maxAddressableWidth)
                {
                    FR2_Asset.s_maxAddressableWidth = currentAddressableWidth;
                }
                
                // Only account for file size width + minimal padding (no detail buttons in addressable drawer)
                var rightPanelSpace = FR2_Asset.s_maxFileSizeWidth + 4f;
                r.xMax -= rightPanelSpace;
                r.xMin = r.xMax - FR2_Asset.s_maxAddressableWidth;
                
                GUI.Label(r, addressContent, EditorStyles.miniLabel);
            }
            GUI.contentColor = c;

        }

        public void SetDirty()
        {
            dirty = true;
            drawer.SetDirty();
        }



        public void RefreshView()
        {
            if (refs == null) refs = new Dictionary<string, FR2_Ref>();
            refs.Clear();

            Dictionary<string, AddressInfo> addresses = FR2_Addressable.GetAddresses();
            if (FR2_Addressable.asmStatus != ASMStatus.AsmOK)
            {
                drawer.messageNoRefs = AsmMessage[FR2_Addressable.asmStatus];
            } else if (FR2_Addressable.projectStatus != ProjectStatus.Ok)
            {
                drawer.messageNoRefs = ProjectStatusMessage[FR2_Addressable.projectStatus];
            }
            drawer.messageEmpty = drawer.messageNoRefs;

            if (addresses == null) addresses = new Dictionary<string, AddressInfo>();
            groups = addresses.Keys.ToList();
            map.Clear();

            if (addresses.Count > 0)
            {
                var maxLengthGroup = string.Empty;
                foreach (KeyValuePair<string, AddressInfo> kvp in addresses)
                {
                    foreach (string guid in kvp.Value.assetGUIDs)
                    {
                        if (refs.ContainsKey(guid)) continue;

                        FR2_Asset asset = FR2_Cache.GetAsset(guid);
                        refs.Add(guid, new FR2_Ref(0, 1, asset, null, null)
                        {
                            isSceneRef = false,
                            group = kvp.Value.bundleGroup
                        });

                        map.Add(guid, kvp.Value);
                        if (maxLengthGroup.Length < kvp.Value.address.Length)
                        {
                            maxLengthGroup = kvp.Value.address;
                        }
                    }

                    foreach (string guid in kvp.Value.childGUIDs)
                    {
                        if (refs.ContainsKey(guid)) continue;

                        FR2_Asset asset = FR2_Cache.GetAsset(guid, true);
                        refs.Add(guid, new FR2_Ref(0, 1, asset, null, null)
                        {
                            isSceneRef = false,
                            group = kvp.Value.bundleGroup
                        });

                        map.Add(guid, kvp.Value);
                        if (maxLengthGroup.Length < kvp.Value.address.Length)
                        {
                            maxLengthGroup = kvp.Value.address;
                        }
                    }
                }
            
                // Find usage
                Dictionary<string, FR2_Ref> usages = FR2_Ref.FindUsage(map.Keys.ToArray());
                foreach (KeyValuePair<string, FR2_Ref> kvp in usages)
                {
                    if (refs.ContainsKey(kvp.Key)) continue;
                    FR2_Ref v = kvp.Value;

                    // do not take script
                    if (v.asset.IsScript) continue;
                    if (v.asset.IsExcluded) continue;

                    refs.Add(kvp.Key, kvp.Value);
                    kvp.Value.depth = 1;
                    kvp.Value.group = AUTO_DEPEND_TITLE;
                }
            }

            dirty = false;
            drawer.SetRefs(refs);
        }

        internal void RefreshSort()
        {
            drawer.RefreshSort();
        }
    }
}
