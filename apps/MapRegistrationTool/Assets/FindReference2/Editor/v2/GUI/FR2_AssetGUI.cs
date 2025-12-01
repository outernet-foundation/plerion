using UnityEditor;
using UnityEngine;
namespace vietlabs.fr2
{
    public static class FR2_AssetGUI
    {
        public class DrawContext
        {
            public bool drawPath = false;
            public bool drawHighlight = true;
        }
        
        // draw context
        public static DrawContext context = new DrawContext();
        
        public static void DrawAsset(Rect rect, FR2_ID id)
        {
            var assetFR2Id = id.WithoutSubAssetIndex();
            var (guid, _) = FR2_CacheAsset.GetGuidAndFileId(assetFR2Id);
            DrawAsset(rect, guid);
        }

        public static void DrawId(Rect rect, FR2_ID fr2Id)
        {
            var fr2IdWidth = 70f;
            int assetIndex = fr2Id.AssetIndex;
            int subIndex = fr2Id.SubAssetIndex;
            GUIContent title = FR2_GUIContent.FromString($"[{assetIndex}-{subIndex}]", $"Value: {fr2Id}");
            
            if (GUI.Button(GUI2.LeftRect(fr2IdWidth, ref rect), title))
            {
                EditorGUIUtility.systemCopyBuffer = fr2Id.ToString();
                UnityEngine.Debug.Log("Copied FR2_ID Value: " + EditorGUIUtility.systemCopyBuffer);
            }
        }
        
        public static void DrawGuid(Rect rect, string guid)
        {
            var fr2IdWidth = 250f;
            GUIContent title = FR2_GUIContent.FromString(guid, "Click to copy GUID");
            if (GUI.Button(GUI2.LeftRect(fr2IdWidth, ref rect), title))
            {
                EditorGUIUtility.systemCopyBuffer = guid;
                FR2_LOG.Log("Copied FR2_ID Value: " + EditorGUIUtility.systemCopyBuffer);
            }
        }
        
        public static void DrawAsset(Rect rect, string guid)
        {
            var info = FR2_AssetInfo.GetOrCreate(guid);
            if (info.folderContent == null) info.RefreshGUIContent();

            float pathW = context.drawPath ? EditorStyles.miniLabel.CalcSize(info.folderContent).x
                : 8f;

            float nameW = context.drawPath
                ? EditorStyles.boldLabel.CalcSize(info.fileNameContent).x
                : EditorStyles.label.CalcSize(info.fileNameContent).x;

            float extW = string.IsNullOrEmpty(info.fileExt)
                ? 0f
                : EditorStyles.miniLabel.CalcSize(info.fileExtContent).x;
            
            
            Rect iconRect = GUI2.LeftRect(16f, ref rect);
            if (Event.current.type == EventType.Repaint)
            {
                Texture icon = AssetDatabase.GetCachedIcon(info.assetPath);
                if (icon != null) GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
            }
            
            if (context.drawPath)
            {
                Color c2 = GUI.color;
                GUI.color = FR2_Theme.Current.SecondaryTextColor;
                GUI.Label(GUI2.LeftRect(pathW, ref rect), info.folderContent, EditorStyles.miniLabel);
                GUI.color = c2;
                rect.xMin -= 2f;
            }
            
            
            GUI.Label(GUI2.LeftRect(nameW, ref rect), info.fileNameContent,
                context.drawPath ? EditorStyles.boldLabel : EditorStyles.label);
            
            if (extW > 0)
            {
                rect.xMin -= 2f;
                GUI.Label(GUI2.LeftRect(extW, ref rect), info.fileExtContent, EditorStyles.miniLabel);
            }
        }
    }
}
