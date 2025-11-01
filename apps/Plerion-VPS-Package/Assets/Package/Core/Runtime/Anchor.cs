using System;
using Unity.Mathematics;
using UnityEngine;

namespace Plerion.VPS
{
    [ExecuteInEditMode]
    public class Anchor : MonoBehaviour
    {
        // public double3 ecefPosition
        // {
        //     get => _ecefPosition;
        //     set
        //     {
        //         if (Equals(_ecefPosition, value))
        //             return;

        //         _ecefPosition = value;
        //         SetEcefTransform(_ecefPosition, _ecefRotation);
        //     }
        // }

        // public quaternion ecefRotation
        // {
        //     get => _ecefRotation;
        //     set
        //     {
        //         if (Equals(_ecefRotation, value))
        //             return;

        //         _ecefRotation = value;
        //         SetEcefTransform(_ecefPosition, _ecefRotation);
        //     }
        // }

        [SerializeField, HideInInspector]
        public float3 ecefPosition;

        [SerializeField, HideInInspector]
        public quaternion ecefRotation;

        [SerializeField, HideInInspector]
        private Vector3 _lastKnownPosition;

        [SerializeField, HideInInspector]
        private Quaternion _lastKnownRotation;

        [SerializeField, HideInInspector]
        private bool _positionInitialized;

        private Vector3 _targetPosition;
        private Quaternion _targetRotation;

        private void Awake()
        {
            if (!Application.isPlaying && _positionInitialized)
                return;

            var ecef = VisualPositioningSystem.ColmapWorldFromUnityWorld(transform.position, transform.rotation);

            _lastKnownPosition = transform.position;
            _lastKnownRotation = transform.rotation;

            _targetPosition = transform.position;
            _targetRotation = transform.rotation;

            Debug.Log($"EP: Setting ECEF position to {ecef.position.x}, {ecef.position.y}, {ecef.position.z}");
            ecefPosition = ecef.position;
            ecefRotation = ecef.rotation;

            _positionInitialized = true;
        }

        private void OnEnable()
        {
            VisualPositioningSystem.OnEcefToUnityWorldTransformUpdated +=
                HandleReferenceFrameChanged;

            // SetEcefTransform(_ecefPosition, _ecefRotation);
        }

        private void OnDisable()
        {
            VisualPositioningSystem.OnEcefToUnityWorldTransformUpdated -=
                HandleReferenceFrameChanged;
        }

        private void LateUpdate()
        {
            // if (transform.position != _lastKnownPosition ||
            //     transform.rotation != _lastKnownRotation)
            // {
            //     _lastKnownPosition = transform.position;
            //     _lastKnownRotation = transform.rotation;

            //     _targetPosition = transform.position;
            //     _targetRotation = transform.rotation;

            //     var ecef = VisualPositioningSystem.UnityWorldToEcef(transform.position, transform.rotation);
            //     _ecefPosition = ecef.position;
            //     _ecefRotation = ecef.rotation;

            //     Debug.Log($"EP: Set ECEF position to {ecef.position.x}, {ecef.position.y}, {ecef.position.z}");
            // }

            // if (Application.isPlaying)
            // {
            //     transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime);
            //     transform.rotation = Quaternion.Lerp(transform.rotation, _targetRotation, Time.deltaTime * 5);

            //     _lastKnownPosition = transform.position;
            //     _lastKnownRotation = transform.rotation;
            // }
            // else
            // {
            //     _lastKnownPosition = _targetPosition;
            //     _lastKnownRotation = _targetRotation;

            //     transform.position = _targetPosition;
            //     transform.rotation = _targetRotation;
            // }
        }

        private void HandleReferenceFrameChanged()
        {
            var local = VisualPositioningSystem.UnityWorldFromColmapWorld(ecefPosition, ecefRotation);
            transform.position = local.position;
            transform.rotation = local.rotation;
        }

        // public void SetEcefTransform(double3 ecefPosition, quaternion ecefRotation)
        // {
        //     _ecefPosition = ecefPosition;
        //          = ecefRotation;

        //     Debug.Log($"EP: Set ECEF position to {ecefPosition.x}, {ecefPosition.y}, {ecefPosition.z}");

        //     var local = VisualPositioningSystem.EcefToUnityWorld(ecefPosition, ecefRotation);

        //     transform.position = local.position;
        //     transform.rotation = local.rotation;

        //     // _lastKnownPosition = local.position;
        //     // _lastKnownRotation = local.rotation;

        //     // _targetPosition = local.position;
        //     // _targetRotation = local.rotation;
        // }

    }
}