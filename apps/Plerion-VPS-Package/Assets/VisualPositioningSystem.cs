using Unity.Mathematics;
using UnityEngine;

namespace Plerion.VPS
{
    [ExecuteInEditMode]
    public class VisualPositioningSystem : MonoBehaviour
    {
#if !UNITY_EDITOR
        private void Awake()
        {
            ImmersalNative.Initialize();
            ImmersalNative.SetInteger("LocalizationMaxPixels", 960 * 720);
            ImmersalNative.SetInteger("NumThreads", 1);
            
            MapManager.Initialize();
            Localizer.Initialize();
            LocalizedReferenceFrame.Initialize();

            PlaneDetector.Initialize().Forget();
        }
        
        private void Update()
        {
            PlaneDetector.Update();
        }

        private void OnDestroy()
        {
            MapManager.Terminate();
            Localizer.Terminate();
            LocalizedReferenceFrame.Terminate();

        }
#else
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

            LocalizedReferenceFrame.SetLocalToEcefTransform(
                Utility.GpsToEcef(Mathf.Deg2Rad * editorLatitude, Mathf.Deg2Rad * editorLongitude, editorElevation),
                Quaternion.AngleAxis(editorRotation, Vector3.up)
            );
        }
#endif
    }
}