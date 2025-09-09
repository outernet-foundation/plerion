using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Outernet.Shared;
using Unity.Mathematics;
using UnityEngine;

namespace Outernet.Client.Location
{
    [RequireComponent(typeof(Anchor))]
    [RequireComponent(typeof(MapRenderer))]
    public class Map : MonoBehaviour
    {
        private MapRenderer mapRenderer;
        private Anchor anchor;
        public MapRenderer.Status status => mapRenderer.status;
        public LocalizationMapRecord metadata { get; private set; }

        public int NativeHandle => mapRenderer.nativeHandle;

        public bool renderVisualization
        {
            get { return mapRenderer.renderVisualization; }
            set { mapRenderer.renderVisualization = value; }
        }

        private quaternion ecefRotation;
        private double3 ecefPosition;

        public quaternion EcefRotation => ecefRotation;
        public double3 EcefPosition => ecefPosition;

        private void Awake()
        {
            mapRenderer = GetComponent<MapRenderer>();
            anchor = GetComponent<Anchor>();
        }

        public async UniTask Load(LocalizationMapRecord record)
        {
            this.metadata = record;

            var color = ColorExtensions.ToColor(record.color);
            color.a = 1f;
            mapRenderer.SetColor(color);

            ecefPosition = new double3(record.position_x, record.position_y, record.position_z);
            ecefRotation = new quaternion((float)record.rotation_x, (float)record.rotation_y, (float)record.rotation_z, (float)record.rotation_w);

            LocalizedReferenceFrame.AddAnchor(anchor);
            anchor.SetEcefTransform(EcefPosition, EcefRotation);

            await mapRenderer.Load(record.id, record.name, Toast.ShowToast);
        }
    }
}