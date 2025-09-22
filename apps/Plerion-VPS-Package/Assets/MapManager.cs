using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Android;

namespace Plerion.VPS
{
    static class MapManager
    {
        static private Dictionary<int, LocalizationMapModel> _mapsByHandle = new Dictionary<int, LocalizationMapModel>();

        static private CancellationTokenSource _pollCancellationTokenSource = new CancellationTokenSource();

        static public void Initialize()
        {
            _pollCancellationTokenSource = new CancellationTokenSource();
            PollMaps(_pollCancellationTokenSource.Token).Forget();
        }

        static public void Terminate()
        {
            _mapsByHandle.Clear();

            _pollCancellationTokenSource?.Cancel();
            _pollCancellationTokenSource?.Dispose();
            _pollCancellationTokenSource = null;
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
            List<(int handle, LocalizationMapModel map)> nativeHandles = new List<(int handle, LocalizationMapModel map)>();

            await UniTask.WhenAll(maps.Where(map => !_mapsByHandle.ContainsValue(map)).Select(async x =>
            {
                Log.Info(LogGroup.Localizer, $"Loading map {x.Name}");

                try
                {
                    var bytes = await PlerionAPI.DownloadMapBytes(x.Id, x.Name);
                    var nativeHandle = await ImmersalNative.LoadMap(bytes);
                    nativeHandles.Add((nativeHandle, x));
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
                foreach (var nativeHandle in nativeHandles)
                    ImmersalNative.FreeMap(nativeHandle.handle).Forget();

                cancellationToken.ThrowIfCancellationRequested();
            }

            var removedMaps = _mapsByHandle.Values
                .Where(map => !maps.Any(x => x.Id == map.Id))
                .ToList();

            foreach (var loadedMap in _mapsByHandle.ToList())
            {
                bool found = false;

                foreach (var map in maps)
                {
                    if (loadedMap.Value.Id == map.Id)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                    _mapsByHandle.Remove(loadedMap.Key);
            }

            foreach (var nativeHandle in nativeHandles)
                _mapsByHandle.Add(nativeHandle.handle, nativeHandle.map);
        }

        static public bool TryGetMap(int nativeHandle, out LocalizationMapModel map)
            => _mapsByHandle.TryGetValue(nativeHandle, out map);

        static public LocalizationMapModel GetMap(int nativeHandle)
            => _mapsByHandle[nativeHandle];

        static public bool HasMap(int nativeHandle)
            => _mapsByHandle.ContainsKey(nativeHandle);
    }
}