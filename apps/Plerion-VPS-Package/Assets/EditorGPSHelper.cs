using Unity.Mathematics;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace Plerion
{
    [ExecuteInEditMode]
    public class EditorGPSHelper : MonoBehaviour
    {
        public double editorLatitude;
        public double editorLongitude;
        public double editorElevation;
        public float editorRotation;

        private double _lastEditorLatitude;
        private double _lastEditorLongitude;
        private double _lastEditorAltitude;
        private float _lastEditorRotation;

        private void OnEnable()
        {
            UpdateLocalizedReferenceFrame();
        }

        private void Update()
        {
            if (_lastEditorLatitude != editorLatitude ||
                _lastEditorLongitude != editorLongitude ||
                _lastEditorAltitude != editorElevation ||
                _lastEditorRotation != editorRotation)
            {
                UpdateLocalizedReferenceFrame();
            }
        }

        private void UpdateLocalizedReferenceFrame()
        {
            _lastEditorLatitude = editorLatitude;
            _lastEditorLongitude = editorLongitude;
            _lastEditorAltitude = editorElevation;
            _lastEditorRotation = editorRotation;

            VisualPositioningSystem.SetUnityWorldToEcefTransform(
                Utility.GpsToEcef(Mathf.Deg2Rad * editorLatitude, Mathf.Deg2Rad * editorLongitude, editorElevation),
                Quaternion.AngleAxis(editorRotation, Vector3.up)
            );
        }
    }
}