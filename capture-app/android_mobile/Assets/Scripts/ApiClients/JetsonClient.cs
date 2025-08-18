using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class USBEthernetTest : MonoBehaviour
{
    private static AndroidJavaObject ethernetNetwork;
    private static AndroidJavaObject connectivityManager;

    async void Start()
    {
        // Get Android connectivity manager
        var activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
            .GetStatic<AndroidJavaObject>("currentActivity");
        connectivityManager = activity.Call<AndroidJavaObject>("getSystemService", "connectivity");

        // Request USB Ethernet network
        var builder = new AndroidJavaObject("android.net.NetworkRequest$Builder");
        builder.Call<AndroidJavaObject>("addTransportType", 3); // TRANSPORT_ETHERNET
        builder.Call<AndroidJavaObject>("removeCapability", 12); // NO INTERNET
        var request = builder.Call<AndroidJavaObject>("build");

        connectivityManager.Call("requestNetwork", request, new NetworkCallback());

        // Wait for network discovery
        await Task.Delay(2000);

        // Test the connection
        await TestJetsonConnection();
    }

    async Task TestJetsonConnection()
    {
        var handler = new HttpClientHandler();
        var client = new HttpClient(handler);

        try
        {
            if (ethernetNetwork != null)
            {
                // Bind to USB network
                connectivityManager.Call<bool>("bindProcessToNetwork", ethernetNetwork);
                Debug.Log("[TEST] Bound to USB Ethernet");
            }
            else
            {
                Debug.LogWarning("[TEST] No USB Ethernet found");
            }

            // Make request to Jetson
            var response = await client.GetAsync("http://192.168.42.1:5000/health");
            var content = await response.Content.ReadAsStringAsync();

            Debug.Log($"[TEST] Success! Status: {response.StatusCode}, Body: {content}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[TEST] Failed: {e.Message}");
        }
        finally
        {
            // Unbind to restore internet
            connectivityManager.Call<bool>("bindProcessToNetwork", null);
            Debug.Log("[TEST] Unbound from USB");

            client.Dispose();
        }
    }

    class NetworkCallback : AndroidJavaProxy
    {
        public NetworkCallback() : base("android.net.ConnectivityManager$NetworkCallback") { }

        public void onAvailable(AndroidJavaObject network)
        {
            Debug.Log("[TEST] USB Ethernet found!");
            ethernetNetwork = network;
        }
    }
}