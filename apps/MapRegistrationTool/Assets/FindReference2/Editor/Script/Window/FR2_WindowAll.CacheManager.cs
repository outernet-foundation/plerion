using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace vietlabs.fr2
{
    internal partial class FR2_WindowAll
    {
        protected void DrawScanProject()
        {
            bool writeImportLog = settings.writeImportLog;
            settings.writeImportLog = EditorGUILayout.Toggle("Write Import Log", settings.writeImportLog);
            if (writeImportLog != settings.writeImportLog)
            {
                EditorUtility.SetDirty(this);
            }

            if (GUILayout.Button("Scan project"))
            {
                FR2_Asset.shouldWriteImportLog = writeImportLog;
                FR2_Cache.DeleteCache();
                FR2_Cache.CreateCache();
            }
        }
        
        protected bool CheckDrawImport()
        {
            FR2_Unity.RefreshEditorStatus();
            
            if (FR2_Unity.isEditorCompiling)
            {
                EditorGUILayout.HelpBox("Compiling scripts, please wait!", MessageType.Warning);
                Repaint();
                return false;
            }

            if (FR2_Unity.isEditorUpdating)
            {
                EditorGUILayout.HelpBox("Importing assets, please wait!", MessageType.Warning);
                Repaint();
                return false;
            }

            InitIfNeeded();

            if (EditorSettings.serializationMode != SerializationMode.ForceText)
            {
                EditorGUILayout.HelpBox("FR2 requires serialization mode set to FORCE TEXT!", MessageType.Warning);
                if (GUILayout.Button("FORCE TEXT")) EditorSettings.serializationMode = SerializationMode.ForceText;

                return false;
            }

            if (FR2_Cache.cacheStatus == FR2_CacheStatus.Incompatible)
            {
                EditorGUILayout.HelpBox("Incompatible cache version found!!!\nFR2 will need a full refresh and according to your project's size this process may take several minutes to complete finish!",
                    MessageType.Warning);

                DrawScanProject();
                return false;
            }

            if (FR2_Cache.isReady) return DrawEnable();

            if (!FR2_Cache.hasCache)
            {
                EditorGUILayout.HelpBox(
                    "FR2 cache not found!\nA first scan is needed to build the cache for all asset references.\nDepending on the size of your project, this process may take a few minutes to complete but once finished, searching for asset references will be incredibly fast!",
                    MessageType.Warning);

                DrawScanProject();
                return false;
            }

            if (!DrawEnable()) return false;

            if (FR2_Cache.status == FR2_Status.ReadAsset)
            {
                var progressInfo = FR2_Cache.GetReadFileProgress();
                var text = $"Refreshing ... {progressInfo.current}/{progressInfo.total}";
                var progress = progressInfo.current/ (float)progressInfo.total;
                var currentAssetName = progressInfo.assetName;
                
                // Show current asset being processed
                if (!string.IsNullOrEmpty(currentAssetName))
                {
                    EditorGUILayout.LabelField(currentAssetName, EditorStyles.miniLabel);
                }

                Rect rect = GUILayoutUtility.GetRect(1f, Screen.width, 18f, 18f);
                EditorGUI.ProgressBar(rect, progress, text);
                Repaint();
            }
            
            return false;
        }
    }
} 