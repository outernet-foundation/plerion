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
        private FR2_DeleteButton deleteUnused;

        private void DrawTools()
        {
            if (isFocusingDuplicate)
            {
                Duplicated.DrawLayout();
                GUILayout.FlexibleSpace();
                return;
            }

            if (isFocusingUnused)
            {
                DrawUnusedAssetsPanel();
                return;
            }

            if (isFocusingUsedInBuild)
            {
                UsedInBuild.DrawLayout();
                return;
            }

            if (isFocusingOthers)
            {
                DrawOthersPanel();
                return;
            }

            if (isFocusingGUIDs)
            {
                DrawGUIDs();
            }
        }

        private void DrawUnusedAssetsPanel()
        {
            using (FR2_Scope.HzLayout())
            {
                EditorGUILayout.LabelField("Recursive Search", FR2_Theme.Current.RecursiveSearchLabelWidth);
                bool oldRecursive = settings.recursiveUnusedScan;
                settings.recursiveUnusedScan = EditorGUILayout.Toggle(settings.recursiveUnusedScan, FR2_Theme.Current.ToggleWidth);
                if (oldRecursive != settings.recursiveUnusedScan)
                {
                    RefUnUse.ResetUnusedAsset(settings.recursiveUnusedScan);
                    EditorUtility.SetDirty(this);
                }
            }
            
            if ((RefUnUse.refs != null) && (RefUnUse.refs.Count == 0))
            {
                EditorGUILayout.HelpBox("Clean! Your project does not has have any unused assets!", MessageType.Info);
                GUILayout.FlexibleSpace();
                EditorGUILayout.HelpBox("Your deleted assets was backup at Library/FR2/ just in case you want your assets back!", MessageType.Info);
            } 
            else
            {
                RefUnUse.DrawLayout();

                if (deleteUnused == null)
                {
                    deleteUnused = new FR2_DeleteButton
                    {
                        warningMessage = "A backup (.unitypackage) will be created so you can reimport the deleted assets later!",
                        deleteLabel = FR2_GUIContent.From("DELETE ASSETS", FR2_Icon.Delete.image),
                        confirmMessage = "Create backup at Library/FR2/"
                    };
                }

                using (HzLayout())
                {
                    deleteUnused.Draw(() => { FR2_Unity.BackupAndDeleteAssets(RefUnUse.source); });
                }
            }
        }

        private void DrawOthersPanel()
        {
            GUILayout.Space(4f);
            using (FR2_Scope.HzLayout())
            {
                // Left: Vertical tab bar
                using (FR2_Scope.VtLayout(FR2_Theme.Current.TabPanelWidth))
                {
                    var tabStyle = new GUIStyle(EditorStyles.toolbarButton)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        fixedHeight = 32f,
                        fontStyle = FontStyle.Normal
                    };
                    for (var i = 0; i < 3; i++)
                    {
                        string label = i == 0 ? "Missing Scripts" : i == 1 ? "Organize Assets" : "Delete Empty Folders";
                        Color bgColor = (settings.othersTabIndex == i) ? new Color(0.7f, 0.9f, 1f, 1f) : GUI.backgroundColor;
                        using (FR2_Scope.BGColor(bgColor))
                        {
                            if (GUILayout.Toggle(settings.othersTabIndex == i, label, tabStyle))
                            {
                                if (settings.othersTabIndex != i) 
                                { 
                                    settings.othersTabIndex = i; 
                                    WillRepaint = true; 
                                }
                            }
                        }
                    }
                    GUILayout.FlexibleSpace();
                }
                
                // Right: Tool content
                using (FR2_Scope.VtLayout())
                {
                    if (settings.othersTabIndex == 0) 
                    { 
                        MissingReference.DrawLayout(); 
                    }
                    else if (settings.othersTabIndex == 1) 
                    { 
                        AssetOrganizer.DrawLayout(); 
                    }
                    else 
                    { 
                        DeleteEmptyFolder.DrawLayout(); 
                    }
                }
            }
        }
    }
} 