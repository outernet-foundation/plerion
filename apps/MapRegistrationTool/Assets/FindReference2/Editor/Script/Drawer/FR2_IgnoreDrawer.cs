using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static vietlabs.fr2.FR2_Scope;

namespace vietlabs.fr2
{
    internal class FR2_IgnoreDrawer
    {
        private readonly FR2_TreeUI2.GroupDrawer groupIgnore;
        private bool dirty;
        private Dictionary<string, FR2_Ref> refs;

        public FR2_IgnoreDrawer()
        {
            groupIgnore = new FR2_TreeUI2.GroupDrawer(DrawGroup, DrawItem)
            {
                hideGroupIfPossible = true
            };

            ApplyFiter();
        }

        private void DrawItem(Rect r, string guid)
        {
            FR2_Ref rf;
            if (!refs.TryGetValue(guid, out rf)) return;

            if (rf.depth == 1) //mode != Mode.Dependency && 
            {
                using (FR2_Scope.GUIColor(Color.blue))
                {
                    GUI.DrawTexture(new Rect(r.x - 4f, r.y + 2f, 2f, 2f), EditorGUIUtility.whiteTexture);
                }
            }

            rf.asset.Draw(
                r,
                new FR2_Asset.FR2_AssetDrawConfig(
                    false,
                true,
                    false,
                    false,
                    false,
                    false,
                    null,
                    true
                )
            );

            Rect drawR = r;
            drawR.x = drawR.x + drawR.width - 50f; // (groupDrawer.TreeNoScroll() ? 60f : 70f) ;
            drawR.width = 30;
            drawR.y += 1;
            drawR.height -= 2;

            if (GUI.Button(drawR, "X", EditorStyles.miniButton)) FR2_Setting.RemoveIgnore(rf.asset.assetPath);
        }

        private void DrawGroup(Rect r, string id, int childCound)
        {
            GUI.Label(r, FR2_GUIContent.FromString(id), EditorStyles.boldLabel);
            if (childCound <= 1) return;

            Rect drawR = r;
            drawR.x = drawR.x + drawR.width - 50f; // (groupDrawer.TreeNoScroll() ? 60f : 70f) ;
            drawR.width = 30;
            drawR.y += 1;
            drawR.height -= 2;
        }

        public void SetDirty()
        {
            dirty = true;
        }

        //private float sizeRatio {
        //    get{
        //        if(FR2_Window.window != null)
        //            return FR2_Window.window.sizeRatio;
        //        return .3f;
        //    }
        //}

        public void Draw()
        {
            if (dirty) ApplyFiter();

            using (HzLayout())
            {
                GUILayout.Space(4f);
                Object[] drops = GUI2.DropZone("Drag & Drop folders here to exclude", 100, 95);
                if ((drops != null) && (drops.Length > 0))
                {
                    for (var i = 0; i < drops.Length; i++)
                    {
                        string path = AssetDatabase.GetAssetPath(drops[i]);
                        if (path.Equals(FR2_Cache.DEFAULT_CACHE_PATH)) continue;
                        FR2_Setting.AddIgnore(path);
                    }
                }

                groupIgnore.DrawLayout();
            }
        }


        private void ApplyFiter()
        {
            dirty = false;
            refs = new Dictionary<string, FR2_Ref>();

            //foreach (KeyValuePair<string, List<string>> item in FR2_Setting.IgnoreFiltered)
            foreach (string item2 in FR2_Setting.s.listIgnore)
            {
                string guid = AssetDatabase.AssetPathToGUID(item2);
                if (string.IsNullOrEmpty(guid)) continue;

                FR2_Asset asset = FR2_Cache.GetAsset(guid, true);
                var r = new FR2_Ref(0, 0, asset, null, "Ignore");
                refs.Add(guid, r);
            }

            groupIgnore.Reset
            (
                refs.Values.ToList(),
                rf => rf.asset != null ? rf.asset.guid : "",
                GetGroup,
                SortGroup
            );
        }

        private string GetGroup(FR2_Ref rf)
        {
            return "Ignore";
        }

        private void SortGroup(List<string> groups)
        { }
    }
}
