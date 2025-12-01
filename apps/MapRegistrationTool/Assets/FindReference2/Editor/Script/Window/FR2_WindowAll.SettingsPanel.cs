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
        private void DrawSettings()
        {
            if (bottomTabs == null || bottomTabs.current == -1) return;

            using (VtLayout(FR2_Theme.Current.SettingsPanelHeight))
            {
                GUILayout.Space(2f);
                switch (bottomTabs.current)
                {
                case 0:
                    {
                        DrawMainSettings();
                        break;
                    }

                case 1:
                    {
                        DrawIgnoreSettings();
                        break;
                    }

                case 2:
                    {
                        DrawFilterSettings();
                        break;
                    }
                }
            }

            Rect rect = GUILayoutUtility.GetLastRect();
            rect.height = 1f;
            GUI2.Rect(rect, Color.black, 0.4f);
        }

        private void DrawMainSettings()
        {
            using (HzLayout())
            {
                using (VtLayout(GUILayout.Width(200f)))
                {
                    bool alternateColor = FR2_Setting.s.alternateColor;
                    FR2_GUI.DrawToggleLeft("Alternate Row Color", ref alternateColor);
                    if (alternateColor != FR2_Setting.s.alternateColor)
                        FR2_Setting.s.alternateColor = alternateColor;
                    
                    bool referenceCount = FR2_Setting.s.referenceCount;
                    FR2_GUI.DrawToggleLeft("Asset Usage Count", ref referenceCount);
                    if (referenceCount != FR2_Setting.s.referenceCount)
                        FR2_Setting.s.referenceCount = referenceCount;
                }
                
                using (VtLayout(GUILayout.Width(200f)))
                {
                    bool showHierarchyCount = FR2_SettingExt.showHierarchyReferenceCount;
                    FR2_GUI.DrawToggleLeft("SceneObject Usage Count", ref showHierarchyCount);
                    if (showHierarchyCount != FR2_SettingExt.showHierarchyReferenceCount)
                        FR2_SettingExt.showHierarchyReferenceCount = showHierarchyCount;
                    
                    FR2_GUI.DrawToggleLeft("Write Import Log", ref settings.writeImportLog);
                }
            }
            
            using (GUIEnable(FR2_SettingExt.showHierarchyReferenceCount))
            {
                using (HzLayout())
                {
                    GUILayout.Label("SceneObject Count Offset", GUILayout.Width(140f));
                    FR2_SettingExt.hierarchyReferenceCountOffset = EditorGUILayout.FloatField(FR2_SettingExt.hierarchyReferenceCountOffset, GUILayout.Width(50f));
                    if (FR2_SettingExt.hierarchyReferenceCountOffset < 0f)
                        FR2_SettingExt.hierarchyReferenceCountOffset = 0f;
                }
            }
            
            if (FR2_SettingExt.isGitProject)
            {
                DrawGitSettings();
            }
        }

        private void DrawGitSettings()
        {
            GUILayout.Space(5f);
            EditorGUILayout.LabelField("Git Settings", EditorStyles.boldLabel);
            
            if (FR2_SettingExt.gitIgnoreAdded)
            {
                EditorGUILayout.HelpBox("FR2_Cache.asset* is already in your .gitignore file.", MessageType.Info);
            }
            else
            {
                using (FR2_Scope.HzLayout())
                {
                    EditorGUILayout.LabelField("Add FR2_Cache.asset* to .gitignore");
                    if (GUILayout.Button("Apply", FR2_Theme.Current.ApplyButtonWidth))
                    {
                        FR2_GitUtil.AddFR2CacheToGitIgnore();
                        FR2_SettingExt.gitIgnoreAdded = true;
                        FR2_SettingExt.hideGitIgnoreWarning = true;
                    }
                }
            }
        }

        private void DrawIgnoreSettings()
        {
            if (FR2_AssetGroupDrawer.DrawIgnoreFolder()) 
            {
                MarkDirty();
            }
        }

        private void DrawFilterSettings()
        {
            if (FR2_AssetGroupDrawer.DrawSearchFilter()) 
            {
                MarkDirty();
            }
        }
    }
} 
