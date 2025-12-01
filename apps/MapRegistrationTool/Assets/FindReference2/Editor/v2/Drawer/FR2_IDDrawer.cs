using UnityEngine;
using UnityEditor;

namespace vietlabs.fr2
{
    [CustomPropertyDrawer(typeof(FR2_ID))]
    public class FR2_IDDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Get the 'value' field in the serialized property
            var valueProp = property.FindPropertyRelative("value");
            var assetIndex = (valueProp.intValue >> 10) & 0x3FFFFF;
            var subAssetIndex = valueProp.intValue & 0x3FF;

            // Set the button content with assetIndex and subAssetIndex, and show value as tooltip
            GUIContent buttonContent = FR2_GUIContent.FromString($"[{assetIndex} : {subAssetIndex}]", $"Value: {valueProp.intValue}");
            GUI.Label(position, buttonContent);
            
            // EditorGUIUtility.systemCopyBuffer = valueProp.intValue.ToString();
            // Debug.Log("Copied Value: " + EditorGUIUtility.systemCopyBuffer);
        }
    }
}
