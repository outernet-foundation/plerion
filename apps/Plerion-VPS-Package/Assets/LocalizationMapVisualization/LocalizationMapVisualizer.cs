using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Plerion
{
    public class LocalizationMapVisualizer : MonoBehaviour
    {
        public double mapRadius = 25;
        public Lighting lightingCondition = Lighting.Day;
        public LocalizationMapRenderer mapRendererPrefab;

        private Dictionary<Guid, LocalizationMapRenderer> _mapsByID = new Dictionary<Guid, LocalizationMapRenderer>();
        private CancellationTokenSource _pollCancellationTokenSource;

        private void OnEnable()
        {
            if (_pollCancellationTokenSource != null)
                return;

            _pollCancellationTokenSource = new CancellationTokenSource();
            PollMaps(_pollCancellationTokenSource.Token).Forget();
        }

        private void OnDisable()
        {
            _pollCancellationTokenSource?.Cancel();
            _pollCancellationTokenSource?.Dispose();
            _pollCancellationTokenSource = null;
        }

        private async UniTask PollMaps(CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var maps = await PlerionAPI.GetMapsWithinRadiusAsync(
                        VisualPositioningSystem.RoughDeviceLatitude,
                        VisualPositioningSystem.RoughDeviceLongitude,
                        VisualPositioningSystem.RoughDeviceElevation,
                        mapRadius,
                        lightingCondition,
                        true
                    );

                    if (maps == null)
                        continue;

                    UpdateMaps(maps);

                    await UniTask.Delay(10000, cancellationToken: cancellationToken);
                }
                catch (Exception exc)
                {
                    if (exc is TaskCanceledException)
                        break;

                    Log.Error(LogGroup.MapVisualization, exc);
                }
            }
        }

        private void UpdateMaps(List<LocalizationMapModel> maps)
        {
            var removedMaps = _mapsByID.Keys.Except(maps.Select(x => x.Id)).ToArray();

            foreach (var removedMap in removedMaps)
            {
                var mapView = _mapsByID[removedMap];
                Destroy(mapView.gameObject);
                _mapsByID.Remove(removedMap);
            }

            var addedMaps = maps.Where(x => !_mapsByID.ContainsKey(x.Id)).ToArray();

            foreach (var addedMap in addedMaps)
            {
                var local = VisualPositioningSystem.EcefToUnityWorld(addedMap.EcefPosition, addedMap.EcefRotation);
                var mapView = Instantiate(mapRendererPrefab, local.position, local.rotation);
                mapView.Load(addedMap.Name, addedMap.Points);
                mapView.SetColor(Utility.ToColor(addedMap.Color));
                _mapsByID.Add(addedMap.Id, mapView);
            }
        }
    }
}
