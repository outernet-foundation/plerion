using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Linq;

namespace Plerion.VPS
{
    static public class GeolocationAPI
    {
        [Serializable]
        private class GeolocationRequest
        {
            public bool considerIp = false;
            public List<WifiAccessPoint> wifiAccessPoints;
        }

        [Serializable]
        private class GeolocationResponse
        {
            public class LatLng
            {
                public double Lat;
                public double Lng;
            }

            public LatLng Location;
            public float Accuracy;
        }

        static readonly string googleGeolocationApiKey = "AIzaSyDzjw16iFtb4UMtsVBNfBo-pkTJ0q742M8";
        static readonly string googleGeolocationApiUrl = $"https://www.googleapis.com/geolocation/v1/geolocate?key={googleGeolocationApiKey}";

        public static async UniTask<(double latitude, double longitude, float accuracy)?> Geolocate(List<WifiAccessPoint> wifiAccessPoints)
        {
            try
            {
                var geolocations = await PlerionAPI.GetWifiGeolocation(
                    wifiAccessPoints.Select(accessPoint => accessPoint.macAddress).ToArray());

                // Temporary hack, we currently assume that PlerionAPI.GetWifiGeolocation returns at 
                // most one geolocation for a give set of wifi access points.
                if (geolocations.Count() != 0)
                {
                    Log.Info(LogGroup.Localizer, $"Using cached geolocation: {geolocations[0].bssid} ({geolocations[0].latitude}, {geolocations[0].longitude})");

                    return (geolocations[0].latitude, geolocations[0].longitude, 0.0f);
                }

                var googleAPIResponse = await RestClient.Post<GeolocationRequest, GeolocationResponse>(
                    googleGeolocationApiUrl,
                    new GeolocationRequest { wifiAccessPoints = wifiAccessPoints });

                Log.Info(
                    LogGroup.Localizer,
                    $"Geolocation API response: ({googleAPIResponse.Location.Lat}, {googleAPIResponse.Location.Lng}) with accuracy {googleAPIResponse.Accuracy}"
                );

                return (googleAPIResponse.Location.Lat, googleAPIResponse.Location.Lng, googleAPIResponse.Accuracy);
            }
            catch (Exception exception)
            {
                Log.Error(LogGroup.Localizer, "Geolocation API request failed", exception);
                return null;
            }
        }
    }
}