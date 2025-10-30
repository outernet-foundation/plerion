using System.Linq;
using System.Threading;

using UnityEngine;
using UnityEngine.Rendering;

using Cysharp.Threading.Tasks;

namespace Plerion.VPS
{
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(Anchor))]
    public class MeshLocalizationMapRenderer : LocalizationMapRenderer
    {
        public string shaderColorProperty = "_Color";

        private Mesh _mesh;
        private MeshRenderer _meshRenderer;
        private CancellationTokenSource _loadCancellationTokenSource;

        private void Awake()
        {
            _mesh = new Mesh();
            _meshRenderer = GetComponent<MeshRenderer>();
            GetComponent<MeshFilter>().mesh = _mesh;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            _loadCancellationTokenSource?.Cancel();
            _loadCancellationTokenSource?.Dispose();
        }

        public override void SetColor(Color color)
            => _meshRenderer.material.SetColor(shaderColorProperty, color);

        protected override void LoadPointsInternal(Point[] points)
        {
            _loadCancellationTokenSource?.Cancel();
            _loadCancellationTokenSource?.Dispose();
            _loadCancellationTokenSource = new CancellationTokenSource();
            LoadPoints(points, _loadCancellationTokenSource.Token).Forget();
        }

        private async UniTask LoadPoints(Point[] points, CancellationToken cancellationToken = default)
        {
            await UniTask.SwitchToThreadPool();

            cancellationToken.ThrowIfCancellationRequested();

            var colors = new Color32[points.Length];
            var indices = Enumerable.Range(0, points.Length).ToArray();

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            foreach (var point in points)
            {
                if (point.position.x > max.x)
                    max.x = point.position.x;

                if (point.position.y > max.y)
                    max.y = point.position.y;

                if (point.position.z > max.z)
                    max.z = point.position.z;

                if (point.position.x < min.x)
                    min.x = point.position.x;

                if (point.position.y < min.y)
                    min.y = point.position.y;

                if (point.position.z < min.z)
                    min.z = point.position.z;
            }

            await UniTask.SwitchToMainThread(cancellationToken: cancellationToken);

            _mesh.Clear();
            _mesh.indexFormat = points.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            _mesh.vertices = points.Select(x => x.position).ToArray();
            _mesh.colors = points.Select(x => x.color).ToArray();
            _mesh.SetIndices(indices, MeshTopology.Points, 0);
            _mesh.bounds = new Bounds((min + max) / 2f, max - min);
        }
    }
}