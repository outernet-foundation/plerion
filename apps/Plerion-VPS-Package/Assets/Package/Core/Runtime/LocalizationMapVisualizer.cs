using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using PlerionApiClient.Api;
using PlerionApiClient.Client;
using PlerionApiClient.Model;
using UnityEngine;
using Color = UnityEngine.Color;
using Vector3 = UnityEngine.Vector3;

namespace Plerion.VPS
{
    [RequireComponent(typeof(ParticleSystem))]
    public class LocalizationMapVisualizer : MonoBehaviour
    {
        private bool _loadingMap = false;
        private CancellationTokenSource _loadMapCancellationTokenSource;
        private ParticleSystem _particleSystem;

        private void Awake()
        {
            _particleSystem = GetComponent<ParticleSystem>();
        }

        protected virtual void OnDestroy()
        {
            _loadMapCancellationTokenSource?.Cancel();
            _loadMapCancellationTokenSource?.Dispose();
        }

        public void SetColor(Color color)
        {
            var m = _particleSystem.main;
            m.startColor = color;
        }

        public async UniTask Load(DefaultApi api, Guid mapId, CancellationToken cancellationToken = default)
        {
            var response = await api.GetLocalizationMapPointsAsync(mapId, AxisConvention.UNITY);
            var stream = response.Content;

            if (_loadingMap)
            {
                _loadMapCancellationTokenSource.Cancel();
                _loadMapCancellationTokenSource.Dispose();
            }

            _loadMapCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _loadingMap = true;

            var header = new byte[4];
            await stream.ReadExactlyAsync(header, 0, 4, cancellationToken);

            var pointCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(header);
            var positionsByteCount = pointCount * 3 * sizeof(float);
            var colorsByteCount = pointCount * 3;
            var payloadByteCount = 4 + positionsByteCount + colorsByteCount;

            var payload = ArrayPool<byte>.Shared.Rent(payloadByteCount);
            Buffer.BlockCopy(header, 0, payload, 0, 4);
            await stream.ReadExactlyAsync(payload, 4, payloadByteCount - 4, cancellationToken);

            try
            {
                PopulateParticleSystemFromPayload(_particleSystem, payload);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(payload);
                _loadingMap = false;
            }
        }

        private static void PopulateParticleSystemFromPayload(ParticleSystem particleSystem, byte[] payload)
        {
            var pointCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0, 4));
            var positionsByteCount = pointCount * 3 * sizeof(float);

            var positions = MemoryMarshal.Cast<byte, float>(payload.AsSpan(4, positionsByteCount));
            var colors = payload.AsSpan(4 + positionsByteCount, pointCount * 3);

            var particles = ArrayPool<ParticleSystem.Particle>.Shared.Rent(pointCount);
            try
            {
                for (var i = 0; i < pointCount; i++)
                {
                    var index = i * 3;
                    particles[i].position = new Vector3(
                        positions[index + 0],
                        positions[index + 1],
                        positions[index + 2]
                    );
                    particles[i].startColor = new Color32(colors[index + 0], colors[index + 1], colors[index + 2], 255);
                    particles[i].startSize = 10000;
                    particles[i].startLifetime = float.MaxValue;
                    particles[i].remainingLifetime = float.MaxValue;
                }

                particleSystem.SetParticles(particles, pointCount);
            }
            finally
            {
                ArrayPool<ParticleSystem.Particle>.Shared.Return(particles);
            }
        }
    }

    internal static class StreamExtensions
    {
        public static async UniTask ReadExactlyAsync(
            this Stream stream,
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken = default
        )
        {
            while (count > 0)
            {
                var read = await stream.ReadAsync(buffer, offset, count, cancellationToken);
                if (read == 0)
                    throw new EndOfStreamException();
                offset += read;
                count -= read;
            }
        }
    }
}
