using Outernet.Client.Location;
using Outernet.Shared;
using Unity.Mathematics;
using UnityEngine;

namespace Outernet.Client
{
    [RequireComponent(typeof(Anchor))]
    public class SyncedAnchor : MonoBehaviour
    {
        public enum SyncState
        {
            Synced,
            LocallyControlled,
            Animating
        }

        private GeoPoseRecord anchorRecord;
        private Anchor anchor;

        public SyncState State { get; set; }

        private void Awake()
        {
            anchor = GetComponent<Anchor>();
        }

        public void Terminate()
        {
            LocalizedReferenceFrame.RemoveSyncedAnchor(this);
        }

        public void Initialize(GeoPoseRecord anchorRecord, double3? initialPosition = null, Quaternion? initialRotation = null)
        {
            this.anchorRecord = anchorRecord;

            LocalizedReferenceFrame.AddSyncedAnchor(this);

            if (initialPosition.HasValue && initialRotation.HasValue)
            {
                anchor.SetEcefTransform(initialPosition.Value, initialRotation.Value);
            }
            else
            {
                anchor.SetEcefTransform(new double3(anchorRecord.ecefPosition.Value.x, anchorRecord.ecefPosition.Value.y, anchorRecord.ecefPosition.Value.z), anchorRecord.ecefRotation.Value);
            }
        }

        public void RealUpdate()
        {
            if (State == SyncState.Animating) return;

            if (State == SyncState.LocallyControlled)
            {
                anchor.SetEcefTransformFromLocalTransform();
                anchorRecord.ecefPosition.EnqueueSet(new Double3(anchor.EcefPosition.x, anchor.EcefPosition.y, anchor.EcefPosition.z));
                anchorRecord.ecefRotation.EnqueueSet(anchor.EcefRotation);

                return;
            }

            anchor.SetEcefTransform(
                math.lerp(anchor.EcefPosition, new double3(anchorRecord.ecefPosition.Value.x, anchorRecord.ecefPosition.Value.y, anchorRecord.ecefPosition.Value.z), 0.1f),
                math.slerp(anchor.EcefRotation, anchorRecord.ecefRotation.Value, 0.25f));
        }

        public void SetLocalTransformFromEcefTransform()
        {
            anchor.SetLocalTransformFromEcefTransform();
        }
    }
}