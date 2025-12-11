using System.Threading;
using PlerionApiClient.Model;
using UnityEngine;
using Color = UnityEngine.Color;

namespace Plerion.VPS
{
    public abstract class LocalizationMapRenderer : MonoBehaviour
    {
        private bool _loadingMap = false;
        private CancellationTokenSource _loadMapCancellationTokenSource;

        protected virtual void OnDestroy()
        {
            _loadMapCancellationTokenSource?.Cancel();
            _loadMapCancellationTokenSource?.Dispose();
        }

        protected abstract void LoadPointsInternal(PointCloudPoint[] points);

        public void Load(PointCloudPoint[] points)
        {
            if (_loadingMap)
            {
                _loadMapCancellationTokenSource.Cancel();
                _loadMapCancellationTokenSource.Dispose();
                _loadMapCancellationTokenSource = null;
            }

            LoadPointsInternal(points);
        }

        public abstract void SetColor(Color color);
    }
}
