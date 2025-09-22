using System;
using Unity.Mathematics;
using UnityEngine;

namespace Plerion.VPS
{
    [ExecuteInEditMode]
    public class Anchor : MonoBehaviour
    {
        public double3 ecefPosition => _ecefPosition;

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

        private void Awake()
        {
            if (_positionInitialized)
                return;

            var ecef = LocalizedReferenceFrame.LocalToEcef(transform.position, transform.rotation);

            _lastKnownPosition = transform.position;
            _lastKnownRotation = transform.rotation;

            _ecefPosition = ecef.position;
            _ecefRotation = ecef.rotation;

            _positionInitialized = true;
        }

        private void OnEnable()
        {
            LocalizedReferenceFrame.onReferenceFrameUpdated +=
                HandleLocalizedReferenceFrameChanged;

            HandleLocalizedReferenceFrameChanged();
        }

        private void OnDisable()
        {
            LocalizedReferenceFrame.onReferenceFrameUpdated -=
                HandleLocalizedReferenceFrameChanged;
        }

        private void LateUpdate()
        {
            if (transform.position != _lastKnownPosition ||
                transform.rotation != _lastKnownRotation)
            {
                var ecef = LocalizedReferenceFrame.LocalToEcef(transform.position, transform.rotation);
                _lastKnownPosition = transform.position;
                _lastKnownRotation = transform.rotation;
                _ecefPosition = ecef.position;
                _ecefRotation = ecef.rotation;
            }
        }


        private void HandleLocalizedReferenceFrameChanged()
        {
            var local = LocalizedReferenceFrame.EcefToLocal(_ecefPosition, _ecefRotation);
            _lastKnownPosition = local.position;
            _lastKnownRotation = local.rotation;
            transform.position = local.position;
            transform.rotation = local.rotation;
        }
    }
}