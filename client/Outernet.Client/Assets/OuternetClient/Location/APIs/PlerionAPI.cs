using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CesiumForUnity;
using Cysharp.Threading.Tasks;
using Outernet.Shared;
using PlerionClient.Api;
using PlerionClient.Client;
using PlerionClient.Model;
using Plerion.VPS;
using Unity.Mathematics;
using UnityEngine;

namespace Outernet.Client
{
    [Serializable]
    public class WifiGeolocationRecord
    {
        public string bssid { get; set; }
        public double latitude { get; set; }
        public double longitude { get; set; }
    }

    public struct NodesWithRadiusOfAnyUserRequest
    {
        public double[][] user_positions { get; set; }
        public double radius { get; set; }
        public int limit_count { get; set; }
    }

    static public class PlerionAPI
    {
        public static DefaultApi api { get; private set; }

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

        public static void Initialize(string apiBaseUrl)
        {
            api = new DefaultApi(apiBaseUrl);
        }

        private static async UniTask<T> Get<T>(string url)
        {
            return await RestClient.Get<T>(url, AddSchemaHeaders(SUPABASE_HEADERS));
        }

        private static async UniTask<T> Post<T, U>(string url, U content)
        {
            return await RestClient.Post<U, T>(url, content, AddSchemaHeaders(SUPABASE_HEADERS));
        }

        public static async UniTask<List<NodeRead>> GetNodes(IEnumerable<double3> userPositions, double radius, int limit_count)
        {
            return await api.GetNodesAsync();
            // try
            // {
            //     return await Post<List<NodeRecord>, NodesWithRadiusOfAnyUserRequest>(
            //         $"{SUPABASE_URL}/rest/v1/rpc/get_nodes_within_radius_of_any_user",
            //         new NodesWithRadiusOfAnyUserRequest
            //         {
            //             user_positions = userPositions
            //                 .Select(position => new double[] { position.x, position.y, position.z })
            //                 .ToArray(),
            //             radius = radius,
            //             limit_count = limit_count
            //         }
            //     );
            // }
            // catch (Exception exc)
            // {
            //     throw exc;
            // }
        }

        public static async UniTask<List<LocalizationMapRead>> GetMapsWithinRadiusAsync(double latitude, double longitude, double height, double radius, Lighting lighting)
        {
            return new List<LocalizationMapRead>();
            try
            {
                var ecefPosition = CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(new double3(longitude, latitude, height));

                var maps = await Get<List<LocalizationMapRead>>(
                    $"{SUPABASE_URL}/rest/v1/rpc/get_maps_within_radius?ecef_x={ecefPosition.x}&ecef_y={ecefPosition.y}&ecef_z={ecefPosition.z}&radius={radius}");

                maps = maps
                    .Where(map => map.Lighting == (int)lighting)
                    .Where(map => map.Active)
                    .ToList();

                Log.Info(LogGroup.Localizer, "Plerion API nearby maps response: {Maps}", string.Join(", ", maps.Select(map => map.Name)));

                return maps;
            }
            catch (Exception exception)
            {
                Log.Warn(LogGroup.Localizer, exception, "Plerion API nearby maps request failed");
                return null;
            }
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