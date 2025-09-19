using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Android;
using static Plerion.VPS.Assert;

namespace Plerion.VPS
{
    static class MapManager
    {
        static private bool enabled = false;
        static private Dictionary<Guid, LocalizationMapModel> _mapsByID = new Dictionary<Guid, LocalizationMapModel>();
        static private Dictionary<int, LocalizationMapModel> _mapsByNativeHandle = new Dictionary<int, LocalizationMapModel>();
        static private Dictionary<Guid, int> _nativeHandlesByID = new Dictionary<Guid, int>();

        static private CancellationTokenSource _pollCancellationTokenSource = new CancellationTokenSource();

        static public void Enable()
        {
            if (enabled)
                return;

            enabled = true;
            _pollCancellationTokenSource = new CancellationTokenSource();
            PollMaps(_pollCancellationTokenSource.Token).Forget();
        }

        static public void Terminate()
        {
            _mapsByID.Clear();
            _mapsByNativeHandle.Clear();
            _nativeHandlesByID.Clear();

            _pollCancellationTokenSource?.Cancel();
            _pollCancellationTokenSource?.Dispose();
            _pollCancellationTokenSource = null;
            enabled = false;
        }

        static private async UniTask PollMaps(CancellationToken cancellationToken = default)
        {
            Input.location.Start();

            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
                Permission.RequestUserPermission(Permission.FineLocation);

            await UniTask.WaitUntil(
                () => Permission.HasUserAuthorizedPermission(Permission.FineLocation) && Input.location.status == LocationServiceStatus.Running,
                cancellationToken: cancellationToken
            );

            var wifiScanner = new WifiScanner();

            double Latitude = 0;
            double Longitude = 0;
            float Height = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                var wifiAccessPoints = await wifiScanner.ScanAsync();

#if UNITY_EDITOR
                Latitude = 40.710882619863504;
                Longitude = -74.00844618336362;
#else
                var geolocation = await GeolocationAPI.Geolocate(wifiAccessPoints);
                
                if (geolocation == null) 
                    continue;

                Latitude = geolocation.Value.latitude;
                Longitude = geolocation.Value.longitude;
#endif

                Height = await PlerionAPI.GetHeight(Latitude, Longitude);
                Log.Info(LogGroup.Localizer, $"Height: {Height}");

                var maps = await PlerionAPI.GetMapsWithinRadiusAsync(
                    Latitude,
                    Longitude,
                    Height,
                    25,
                    Settings.lightingConditionOverride ?? Lighting.Day
                );

                if (maps == null)
                    continue;

                await UniTask.WhenAll(
                    UpdateMaps(maps, cancellationToken),
                    UniTask.Delay(10000, cancellationToken: cancellationToken)
                );
            }
        }

        private static async UniTask UpdateMaps(List<LocalizationMapModel> maps, CancellationToken cancellationToken = default)
        {
            var newMaps = maps
                .Where(map => !_mapsByID.ContainsKey(map.Id))
                .ToList();

            Dictionary<LocalizationMapModel, int> nativeHandles = new Dictionary<LocalizationMapModel, int>();

            await UniTask.WhenAll(newMaps.Select(async x =>
            {
                Log.Info(LogGroup.Localizer, $"Loading map {x.Name}");

                try
                {
                    var bytes = await PlerionAPI.DownloadMapBytes(x.Id, x.Name);
                    var nativeHandle = await ImmersalNative.LoadMap(bytes);
                    nativeHandles.Add(x, nativeHandle);
                    Log.Info(LogGroup.Localizer, $"Loaded map {x.Name}");
                }
                catch (Exception exc)
                {
                    Log.Error(LogGroup.Localizer, $"Encountered an error loading map {x.Name}", exc);
                }
            }));

            await UniTask.SwitchToMainThread();

            if (cancellationToken.IsCancellationRequested)
            {
                foreach (var nativeHandle in nativeHandles.Values)
                    ImmersalNative.FreeMap(nativeHandle).Forget();

                cancellationToken.ThrowIfCancellationRequested();
            }

            var removedMaps = _mapsByID.Keys
                .Where(id => !maps.Any(x => x.Id == id))
                .ToList();

            foreach (var mapId in removedMaps)
            {
                ASSERT(_mapsByID.ContainsKey(mapId), $"Map {mapId} is not in maps");
                ASSERT(_nativeHandlesByID.ContainsKey(mapId), "Loaded map is not in nativeHandles");

                var nativeHandle = _nativeHandlesByID[mapId];

                _nativeHandlesByID.Remove(mapId);
                _mapsByNativeHandle.Remove(nativeHandle);
                _mapsByID.Remove(mapId);
            }

            foreach (var newMap in newMaps)
            {
                var nativeHandle = nativeHandles[newMap];
                _nativeHandlesByID.Add(newMap.Id, nativeHandle);
                _mapsByNativeHandle.Add(nativeHandle, newMap);
                _mapsByID.Add(newMap.Id, newMap);
            }
        }

        static public bool TryGetMap(int nativeHandle, out LocalizationMapModel map)
            => _mapsByNativeHandle.TryGetValue(nativeHandle, out map);

        static public bool TryGetMap(Guid id, out LocalizationMapModel map)
            => _mapsByID.TryGetValue(id, out map);

        static public LocalizationMapModel GetMap(int nativeHandle)
            => _mapsByNativeHandle[nativeHandle];

        static public LocalizationMapModel GetMap(Guid id)
            => _mapsByID[id];

        static public bool HasMap(int nativeHandle)
            => _mapsByNativeHandle.ContainsKey(nativeHandle);

        static public bool HasMap(Guid id)
            => _mapsByID.ContainsKey(id);
    }
}