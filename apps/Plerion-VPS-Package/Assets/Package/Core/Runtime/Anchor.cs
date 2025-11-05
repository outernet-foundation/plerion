using System;
using Unity.Mathematics;
using UnityEngine;

namespace Plerion.VPS
{
    [ExecuteInEditMode]
    public class Anchor : MonoBehaviour
    {
        public double3 ecefPosition
        {
            get => _ecefPosition;
            set
            {
                if (Equals(_ecefPosition, value))
                    return;

                _ecefPosition = value;
                SetEcefTransform(_ecefPosition, _ecefRotation);
            }
        }

        public quaternion ecefRotation
        {
            get => _ecefRotation;
            set
            {
                if (Equals(_ecefRotation, value))
                    return;

                _ecefRotation = value;
                SetEcefTransform(_ecefPosition, _ecefRotation);
            }
        }

        [SerializeField, HideInInspector]
        private double3 _ecefPosition;

        [SerializeField, HideInInspector]
        private quaternion _ecefRotation;

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
            // if (!Application.isPlaying && _positionInitialized)
            //     return;

            // var ecef = VisualPositioningSystem.UnityWorldToEcef(transform.position, transform.rotation);

            // _lastKnownPosition = transform.position;
            // _lastKnownRotation = transform.rotation;

            // _targetPosition = transform.position;
            // _targetRotation = transform.rotation;

            ecefPosition = new double3(1333832.9145346545, -4654217.496323097, 4138126.868056108);
            ecefRotation = new quaternion(-0.1304137110710144f, 0.9284381866455078f, -0.3444613814353943f, 0.048385001718997955f);

            // _positionInitialized = true;
        }

        private void OnEnable()
        {
            // VisualPositioningSystem.OnEcefToUnityWorldTransformUpdated +=
            //     HandleReferenceFrameChanged;

            // SetEcefTransform(_ecefPosition, _ecefRotation);
        }

        private void OnDisable()
        {
            // VisualPositioningSystem.OnEcefToUnityWorldTransformUpdated -=
            //     HandleReferenceFrameChanged;
        }

        private void LateUpdate()
        {
            var localTransform = VisualPositioningSystem.EcefToUnityWorld(ecefPosition, ecefRotation);
            transform.position = localTransform.position;
            transform.rotation = localTransform.rotation;

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
            // var local = VisualPositioningSystem.EcefToUnityWorld(ecefPosition, ecefRotation);
            // transform.position = local.position;
            // transform.rotation = local.rotation;
        }

        public void SetEcefTransform(double3 ecefPosition, quaternion ecefRotation)
        {
            // _ecefPosition = ecefPosition;
            // _ecefRotation = ecefRotation;

            // Debug.Log($"EP: Set ECEF position to {ecefPosition.x}, {ecefPosition.y}, {ecefPosition.z}");

            // var local = VisualPositioningSystem.EcefToUnityWorld(ecefPosition, ecefRotation);

            // transform.position = local.position;
            // transform.rotation = local.rotation;

            // _lastKnownPosition = local.position;
            // _lastKnownRotation = local.rotation;

            // _targetPosition = local.position;
            // _targetRotation = local.rotation;
        }
    }
}