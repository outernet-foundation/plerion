using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Unity.Mathematics;

namespace Plerion.VPS
{
    public class LocalizationMapVisualizer : MonoBehaviour
    {
        public LocalizationMapRenderer mapRendererPrefab;

        private Dictionary<Guid, LocalizationMapRenderer> _mapsByID =
            new Dictionary<Guid, LocalizationMapRenderer>();

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
                    var maps = await VPSManager.API.GetLoadedMapsAsync(cancellationToken);

                    await UniTask.SwitchToMainThread(cancellationToken: cancellationToken);

                    if (maps == null)
                    {
                        foreach (var map in _mapsByID.Values)
                            Destroy(map.gameObject);

                        maps.Clear();

                        await UniTask.Delay(10000, cancellationToken: cancellationToken);
                    }

                    var removedMaps = _mapsByID.Keys.Where(x => !maps.Any(y => y.Id == x)).ToArray();
                    foreach (var removedMap in removedMaps)
                    {
                        Destroy(_mapsByID[removedMap].gameObject);
                        _mapsByID.Remove(removedMap);
                    }

                    var addedMaps = maps.Where(x => !_mapsByID.ContainsKey(x.Id)).ToArray();
                    foreach (var addedMap in addedMaps)
                    {
                        var local = ReferenceFrame.EcefToUnityWorld(
                            new double3(addedMap.PositionX, addedMap.PositionY, addedMap.PositionZ),
                            new quaternion((float)addedMap.RotationX, (float)addedMap.RotationY, (float)addedMap.RotationZ, (float)addedMap.RotationW)
                        );

                        var mapView = Instantiate(mapRendererPrefab, local.position, local.rotation);
                        mapView.gameObject.name = addedMap.Name;
                        mapView.Load(addedMap.Id);
                        mapView.SetColor(Utility.ToColor(addedMap.Color));

                        _mapsByID.Add(addedMap.Id, mapView);
                    }

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
    }
}
