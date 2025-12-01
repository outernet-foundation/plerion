using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;
using AddUsageCB = System.Action<string, long>;

namespace vietlabs.fr2
{
    internal static partial class FR2_Parser // Model3D
    {
        private static readonly HashSet<string> MODEL_FILES = new HashSet<string>()
        {
            ".fbx", ".obj", ".3ds", ".dxf", ".max", ".c4d", ".blend", 
            ".lwo", ".lws", ".ma", ".mb", ".jas", ".skp", ".dae", ".3dm", 
            ".stl", ".ply", ".dxf", ".lwo", ".lws"
        };

        private static void Read_Model3D(GameObject go, AddUsageCB callback)
        {
            Component[] compList = go.GetComponentsInChildren<Component>();
            for (var i = 0; i < compList.Length; i++)
            {
                LoadSerialized(compList[i], callback);
            }
        }
        
        private static void LoadSerialized(UnityObject target, AddUsageCB callback)
        {
            SerializedProperty[] props = FR2_Unity.xGetSerializedProperties(target, true);

            for (var i = 0; i < props.Length; i++)
            {
                if (props[i].propertyType != SerializedPropertyType.ObjectReference) continue;
                UnityObject refObj = props[i].objectReferenceValue;
                AddObjectUsage(refObj, callback);
            }
        }
    }
}
