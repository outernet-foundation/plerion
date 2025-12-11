using System.Linq;
using PlerionApiClient.Model;
using UnityEngine;
using Color = UnityEngine.Color;
using Vector3 = UnityEngine.Vector3;

namespace Plerion.VPS
{
    [RequireComponent(typeof(ParticleSystem))]
    public class ParticleSystemLocalizationMapRenderer : LocalizationMapRenderer
    {
        private ParticleSystem _particleSystem;
        private Color _color = Color.white;

        private void Awake()
        {
            _particleSystem = GetComponent<ParticleSystem>();
        }

        public override void SetColor(Color color)
        {
            var m = _particleSystem.main;
            m.startColor = color;
        }

        protected override void LoadPointsInternal(PointCloudPoint[] points)
        {
            var m = _particleSystem.main;
            _particleSystem.SetParticles(
                points
                    .Select(x => new ParticleSystem.Particle()
                    {
                        position = new Vector3((float)x.Position.X, (float)x.Position.Y, (float)x.Position.Z),
                        startLifetime = Mathf.Infinity,
                        startSize = 10000, // Make this huge and cap the particle size in the renderer, so they are always a constant size on screen
                        startColor = new Color(x.Color.R / 255f, x.Color.G / 255f, x.Color.B / 255f),
                    })
                    .ToArray()
            );
            _particleSystem.Play();
        }
    }
}
