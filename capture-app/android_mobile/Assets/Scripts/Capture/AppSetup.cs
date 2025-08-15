using UnityEngine;
using FofX.Stateful;

namespace PlerionClient.Client
{
    public class AppSetup : MonoBehaviour
    {
        public CaptureController captureControllerPrefab;

        private void Awake()
        {
            gameObject.AddComponent<App>();
            var plerionAPIBaseUrl = "https://api.outernetfoundation.org";

#if UNITY_EDITOR
            var editorSettings = EditorSettings.GetOrCreateInstance();
            if (editorSettings.overrideEnvironment)
                plerionAPIBaseUrl = editorSettings.overrideEnvironmentURL;
#endif

            App.state.plerionAPIBaseUrl.ExecuteSet(plerionAPIBaseUrl);

            ZedCaptureController.Initialize();
            Instantiate(captureControllerPrefab);

            Destroy(this);
        }
    }
}