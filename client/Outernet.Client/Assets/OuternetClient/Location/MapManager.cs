using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Outernet.Shared;
using R3;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Android;
using static Outernet.Shared.Assert;

namespace Outernet.Client.Location
{
    static class MapManager
    {
        static readonly List<(string roomName, double latitude, double longitude)> hackRooms =
            new List<(string roomName, double latitude, double longitude)>()
            {
                ("Chase Plaza", 40.707579253067756, -74.00920098315065),
                ("East Village", 40.729621262027955, -73.98768528648537),
                ("Sag Harbor", 41.000603805381026, -72.2953353870656),
                ("Tyler's Office", 40.710882619863504, -74.00844618336362),
                ("Max's House", 41.38289248602659, -73.87528141482522),
                ("Bronxville", 40.93818554605524, -73.83196408701227)
            };

        static bool enabled = false;
        static public double Latitude { get; private set; }
        static public double Longitude { get; private set; }
        static public double Height { get; private set; }
        static public float Accuracy { get; private set; }

        static public Dictionary<int, Map> maps = new Dictionary<int, Map>();
        static private Dictionary<int, int> nativeHandles = new Dictionary<int, int>();

        static public Map GetMapFromNativeHandle(int nativeHandle) => maps[nativeHandles[nativeHandle]];
        static public bool HasNativeHandle(int nativeHandle) => nativeHandles.ContainsKey(nativeHandle);

        static public bool renderVisualizations
        {
            set
            {
                foreach (var map in maps.Values)
                {
                    map.renderVisualization = value;
                }
            }
        }

        static public void Enable() => EnableAsync().Forget();

        static public void Terminate()
        {
            Maps.Clear();

            enabled = false;
        }

        static private async UniTask EnableAsync()
        {
            enabled = true;

            Input.location.Start();

            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                Permission.RequestUserPermission(Permission.FineLocation);
            }

            await UniTask.WaitUntil(() => Permission.HasUserAuthorizedPermission(Permission.FineLocation));

            var wifiScanner = new WifiScanner();

            while (enabled)
            {
                var wifiAccessPoints = await wifiScanner.ScanAsync();

#if UNITY_EDITOR
                Latitude = 40.710882619863504;
                Longitude = -74.00844618336362;
                Accuracy = 10;
#else
                var geolocation = await GeolocationAPI.Geolocate(wifiAccessPoints);
                if (geolocation == null) continue;
                Latitude = geolocation.Value.latitude;
                Longitude = geolocation.Value.longitude;
                Accuracy = geolocation.Value.accuracy;
#endif

                // Find the closest hack room
                var (roomName, _, _) = hackRooms
                    .OrderBy(room => Math.Sqrt(Math.Pow(room.latitude - Latitude, 2) + Math.Pow(room.longitude - Longitude, 2)))
                    .First();

                if (ConnectionManager.RoomConnectionRequested.Value == null)
                {
                    ConnectionManager.RoomConnectionRequested.EnqueueSet(roomName);
                }

                Height = await TilesetManager.GetHeight(Latitude, Longitude);
                Log.Info(LogGroup.Localizer, "Height: {height}", Height);

                var radius = 25;

                if (roomName == "Tyler's Office")
                {
                    Settings.lightingCondition = Lighting.Day;
                    radius = 50;
                }
                else if (Settings.lightingConditionOverride.HasValue)
                {
                    Settings.lightingCondition = Settings.lightingConditionOverride.Value;
                }
                else
                {
                    // #if UNITY_EDITOR
                    Settings.lightingCondition = Lighting.Day;
                    // #else
                    //                     // Determine if it is day or night
                    //                     var result = await RestClient.Get<string>($"https://api.sunrise-sunset.org/json?lat={Latitude}&lng={Longitude}&date=today&formatted=0");
                    //                     var resultJson = JObject.Parse(result);
                    //                     var sunrise = DateTime.Parse(resultJson["results"]["sunrise"].Value<string>());
                    //                     var sunset = DateTime.Parse(resultJson["results"]["sunset"].Value<string>());
                    //                     var now = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.Local);
                    //                     Settings.lightingCondition = now > sunrise && now < sunset ? Lighting.Day : Lighting.Night;
                    // #endif
                }

                var maps = await PlerionAPI.GetMapsWithinRadiusAsync(Latitude, Longitude, Height, radius, Settings.lightingCondition);
                if (maps == null) continue;
                Maps = maps;

                await UniTask.Delay(10000);
            }
        }

        static bool localizerStarted = false;

        static public List<LocalizationMapRecord> Maps
        {
            get => maps.Values.Select(map => map.metadata).ToList();
            set
            {
                var newMaps = value
                    .Where(map => !maps.ContainsKey(map.id))
                    .ToList();

                // var recentlyLocalizedMaps = localizationHistory
                //     .Reverse()
                //     .Take(8)
                //     .Distinct();

                var removedMaps = maps.Keys
                    .Where(id => !value.Select(map => map.id).Contains(id))
                    // Don't remove any maps that are in the most recent 8 localizationHistory localizations
                    // .Where(id => !recentlyLocalizedMaps.Any(localization => localization.mapId == id))
                    .ToList();

                foreach (var newMap in newMaps)
                {
                    Log.Info(LogGroup.Localizer, "Loading map {Name}", newMap.name);

                    var map = new GameObject(newMap.name).AddComponent<Map>();
                    maps.Add(newMap.id, map);
                    map
                        .Load(newMap)
                        .ContinueWith(() =>
                        {
                            Log.Info(LogGroup.Localizer, "Map {Name} loaded", newMap.name);

                            ASSERT(maps.ContainsKey(newMap.id), $"Loaded map {newMap.id} is not in maps");

                            bool firstMap = nativeHandles.Count == 0;
                            nativeHandles.Add(map.NativeHandle, newMap.id);

                            if (!localizerStarted)
                            {
                                localizerStarted = true;
#if UNITY_EDITOR
                                // In the editor, just apply the first loaded map's transform as an initial "estimate"
                                LocalizedReferenceFrame.SetTargetLocalToEcefTransform(Double4x4.FromTranslationRotation(map.EcefPosition, map.EcefRotation));
#endif
                            }
                        })
                        .Forget();
                }


                foreach (var mapId in removedMaps)
                {
                    ASSERT(maps.ContainsKey(mapId), $"Map {mapId} is not in maps");

                    var map = maps[mapId];

                    if (map.status == MapRenderer.Status.Loaded)
                    {
                        ASSERT(nativeHandles.ContainsKey(map.NativeHandle), "Loaded map is not in nativeHandles");
                        nativeHandles.Remove(map.NativeHandle);
                    }

                    UnityEngine.Object.Destroy(map.gameObject);
                    maps.Remove(mapId);
                }
            }
        }
    }
}