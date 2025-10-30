using System;
using System.Linq;
using System.Threading;

using UnityEngine;

using Cysharp.Threading.Tasks;

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

        private async UniTask DownloadMapAndLoadPoints(Guid id, CancellationToken cancellationToken = default)
        {
            _loadingMap = true;

            Log.Info(LogGroup.MapVisualization, $"Downloading map {id}.");

            var pointData = await VisualPositioningSystem.GetLocalizationMapPointsAsync(id, cancellationToken);

            if (pointData == null)
                throw new Exception($"Map not found. ID {id}");

            Log.Info(LogGroup.MapVisualization, $"Downloaded map {id}.");

            await UniTask.SwitchToMainThread(cancellationToken: cancellationToken);

            _loadingMap = false;

            Load(pointData.ToArray());
        }

        protected abstract void LoadPointsInternal(Point[] points);

        public void Load(Guid localizationMapID)
        {
            _loadMapCancellationTokenSource?.Cancel();
            _loadMapCancellationTokenSource?.Dispose();
            _loadMapCancellationTokenSource = new CancellationTokenSource();
            DownloadMapAndLoadPoints(localizationMapID, _loadMapCancellationTokenSource.Token).Forget();
        }

        public void Load(Point[] points)
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