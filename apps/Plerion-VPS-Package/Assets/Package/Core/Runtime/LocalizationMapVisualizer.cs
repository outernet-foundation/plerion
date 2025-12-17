using System.Linq;
using System.Threading;
using PlerionApiClient.Model;
using UnityEngine;
using Color = UnityEngine.Color;
using Vector3 = UnityEngine.Vector3;

namespace Plerion.Core
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

        public void Load(PointCloudPoint[] points)
        {
            if (_loadingMap)
            {
                _loadMapCancellationTokenSource.Cancel();
                _loadMapCancellationTokenSource.Dispose();
                _loadMapCancellationTokenSource = null;
            }

            _particleSystem.SetParticles(
                points
                    .Select(point => new ParticleSystem.Particle()
                    {
                        position = new Vector3(
                            (float)point.Position.X,
                            (float)point.Position.Y,
                            (float)point.Position.Z
                        ),
                        startLifetime = Mathf.Infinity,
                        startSize = 10000, // Make this huge and cap the particle size in the renderer, so they are always a constant size on screen
                        startColor = new Color(point.Color.R / 255f, point.Color.G / 255f, point.Color.B / 255f),
                    })
                    .ToArray()
            );
            _particleSystem.Play();
        }
    }
}
