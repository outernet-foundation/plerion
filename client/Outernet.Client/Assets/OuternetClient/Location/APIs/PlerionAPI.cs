using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CesiumForUnity;
using Cysharp.Threading.Tasks;
using Outernet.Shared;
using Unity.Mathematics;
using UnityEngine;

namespace Outernet.Client
{
    public class LocalizationMapRecord
    {
        public int id { get; set; }
        public Guid uuid { get; set; }
        public string name { get; set; }
        public Lighting lighting { get; set; }
        public long color { get; set; }
        public bool active { get; set; }
        public double position_x { get; set; }
        public double position_y { get; set; }
        public double position_z { get; set; }
        public double rotation_x { get; set; }
        public double rotation_y { get; set; }
        public double rotation_z { get; set; }
        public double rotation_w { get; set; }
        public PoseDataRaw input_image_positions { get; set; }
        public PoseDataRaw input_image_positions_ecef { get; set; }
        public Point position { get; set; }
    }

    public class PoseDataRaw
    {
        public string type { get; set; }
        public double[][] coordinates { get; set; }
    }

    [Serializable]
    public struct Point
    {
        public string type { get; set; }
        public double[] coordinates { get; set; }
    }

    [Serializable]
    public class WifiGeolocationRecord
    {
        public string bssid { get; set; }
        public double latitude { get; set; }
        public double longitude { get; set; }
    }

    static public class PlerionAPI
    {
        private static readonly string SUPABASE_URL = "https://ysugcfsqhmladpfnqgvs.supabase.co";
        private static readonly string SUPABASE_API_KEY = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InlzdWdjZnNxaG1sYWRwZm5xZ3ZzIiwicm9sZSI6ImFub24iLCJpYXQiOjE3MjcyNzU2MzksImV4cCI6MjA0Mjg1MTYzOX0.cIdcZuyN28iFbiUChId4fc-6YjleIkAUeAQs7UQ6gRE";

        private static Dictionary<string, string> SUPABASE_HEADERS = new Dictionary<string, string>
        {
            { "apiKey", SUPABASE_API_KEY },
            { "Authorization", $"Bearer {SUPABASE_API_KEY}" }
        };

        private static Dictionary<string, string> SUPABASE_HEADERS_INSERT = new Dictionary<string, string>
        {
            { "apiKey", SUPABASE_API_KEY },
            { "Authorization", $"Bearer {SUPABASE_API_KEY}" },
            { "Prefer", "return=representation" }
        };

        private static Dictionary<string, string> SUPABASE_HEADERS_UPDATE = new Dictionary<string, string>
        {
            { "apiKey", SUPABASE_API_KEY },
            { "Authorization", $"Bearer {SUPABASE_API_KEY}" },
            { "Prefer", "return=representation" }
        };

        private static Dictionary<string, string> SUPABASE_HEADERS_UPSERT = new Dictionary<string, string>
        {
            { "apiKey", SUPABASE_API_KEY },
            { "Authorization", $"Bearer {SUPABASE_API_KEY}" },
            { "Prefer", "return=representation,resolution=merge-duplicates" }
        };

        private static Dictionary<string, string> SUPABASE_HEADERS_DELETE = new Dictionary<string, string>
        {
            { "apiKey", SUPABASE_API_KEY },
            { "Authorization", $"Bearer {SUPABASE_API_KEY}" },
            { "Prefer", "return=minimal" },
        };

        private static Dictionary<string, string> AddSchemaHeaders(Dictionary<string, string> headers)
        {
            return new Dictionary<string, string>(headers)
            {
                { "Accept-Profile", App.environmentSchema },
                { "Content-Profile", App.environmentSchema }
            };
        }

        private static async UniTask<T> Get<T>(string url)
        {
            return await RestClient.Get<T>(url, AddSchemaHeaders(SUPABASE_HEADERS));
        }

        private static async UniTask<T> Post<T, U>(string url, U content)
        {
            return await RestClient.Post<U, T>(url, content, AddSchemaHeaders(SUPABASE_HEADERS));
        }

        public static async UniTask<LocalizationMapRecord> GetMapMetadata(Guid id)
        {
            var response = await Get<List<LocalizationMapRecord>>($"{SUPABASE_URL}/rest/v1/localization_maps?select=*&id=eq.{id}");

            return response.FirstOrDefault();
        }

        public static async UniTask<List<LocalizationMapRecord>> GetMapsWithinRadiusAsync(double latitude, double longitude, double height, double radius, Lighting lighting)
        {
            try
            {
                var ecefPosition = CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(new double3(longitude, latitude, height));

                var maps = await Get<List<LocalizationMapRecord>>(
                    $"{SUPABASE_URL}/rest/v1/rpc/get_maps_within_radius?ecef_x={ecefPosition.x}&ecef_y={ecefPosition.y}&ecef_z={ecefPosition.z}&radius={radius}");

                maps = maps
                    .Where(map => map.lighting == lighting)
                    .Where(map => map.active)
                    .ToList();

                Log.Info(LogGroup.Localizer, "Plerion API nearby maps response: {Maps}", string.Join(", ", maps.Select(map => map.name)));

                return maps;
            }
            catch (Exception exception)
            {
                Log.Warn(LogGroup.Localizer, exception, "Plerion API nearby maps request failed");
                return null;
            }
        }

        public static async UniTask<byte[]> DownloadMapBytes(int id, string name, bool skipCache = false)
        {
            string cachePath = Path.Combine(Application.persistentDataPath, "cache");

            if (!Directory.Exists(cachePath))
            {
                Directory.CreateDirectory(cachePath);
            }

            string mapDirectory = Path.Combine(cachePath, id.ToString());
            string bytesPath = Path.Combine(mapDirectory, $"map.bytes");

            if (File.Exists(bytesPath) && !skipCache)
            {
                return File.ReadAllBytes(bytesPath);
            }

            Toast.ShowToast($"Downloading map {name}...");

            var bytes = await Get<byte[]>($"{SUPABASE_URL}/storage/v1/object/public/LocalizationMaps/{id}.bytes");

            Directory.CreateDirectory(mapDirectory);
            File.WriteAllBytes(bytesPath, bytes);

            return bytes;
        }

        public static async UniTask<WifiGeolocationRecord[]> GetWifiGeolocation(params string[] bssids)
        {
            try
            {
                return await TableGet<WifiGeolocationRecord>(
                    "wifi_geolocations",
                    "bssid",
                    bssids.Select(x => x.ToString()).ToArray());
            }
            catch (Exception exception)
            {
                Log.Warn(LogGroup.Localizer, exception, "Plerion API wifi geolocation request failed");
                return null;
            }
        }

        private static UniTask<T[]> TableGet<T>(string table, string keyName, params string[] keys)
        {
            return RestClient.Get<T[]>(
                $"{SUPABASE_URL}/rest/v1/{table}?{keyName}=eq(any).{{{string.Join(",", keys)}}}",
                AddSchemaHeaders(SUPABASE_HEADERS)
            );
        }

        private static UniTask<T[]> TableGetAll<T>(string table)
        {
            return RestClient.Get<T[]>(
                $"{SUPABASE_URL}/rest/v1/{table}",
                AddSchemaHeaders(SUPABASE_HEADERS)
            );
        }

        private static UniTask<T[]> TableInsert<T>(string table, params T[] records)
        {
            return RestClient.Post<T[], T[]>(
                $"{SUPABASE_URL}/rest/v1/{table}",
                records,
                AddSchemaHeaders(SUPABASE_HEADERS_INSERT)
            );
        }

        private static UniTask<T> TableUpdate<T>(string table, string keyName, string key, T record)
        {
            return RestClient.Patch<T, T[]>(
                $"{SUPABASE_URL}/rest/v1/{table}?{keyName}=eq.{key}",
                record,
                AddSchemaHeaders(SUPABASE_HEADERS_UPDATE)
            ).ContinueWith(x => x[0]);
        }

        private static UniTask<T[]> TableUpsert<T>(string table, params T[] records)
        {
            return RestClient.Post<T[], T[]>(
                $"{SUPABASE_URL}/rest/v1/{table}",
                records,
                AddSchemaHeaders(SUPABASE_HEADERS_UPSERT)
            );
        }

        private static UniTask TableDelete(string table, string keyName, params string[] keys)
        {
            return RestClient.Delete(
                $"{SUPABASE_URL}/rest/v1/{table}?{keyName}=eq(any).{{{string.Join(",", keys)}}}",
                AddSchemaHeaders(SUPABASE_HEADERS_DELETE)
            );
        }
    }
}