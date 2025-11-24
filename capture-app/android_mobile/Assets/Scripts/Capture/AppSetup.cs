using UnityEngine;
using FofX.Stateful;
using Nessle;
using Cysharp.Threading.Tasks;

namespace PlerionClient.Client
{
    public class AppSetup : MonoBehaviour
    {
        public SceneReferences sceneReferences;
        public CaptureController captureControllerPrefab;
        public LocalizationManager localizationManager;
        public UIPrimitiveSet uiPrimitives;
        public UIElementSet uiElements;

        private void Awake()
        {
            UniTaskScheduler.UnobservedTaskException += Debug.LogException;

            sceneReferences.Initialize();

            Application.targetFrameRate = 120;
            UIBuilder.primitives = uiPrimitives;
            UIElements.elements = uiElements;

            gameObject.AddComponent<App>();

            var env = UnityEnv.GetOrCreateInstance();

            var plerionAPIBaseUrl = string.IsNullOrEmpty(env.deviceName) ?
                "https://api.outernetfoundation.org" : $"https://{env.deviceName}-api.outernetfoundation.org";

            var keycloakBaseUrl = string.IsNullOrEmpty(env.deviceName) ?
                "https://keycloak.outernetfoundation.org" : $"https://{env.deviceName}-keycloak.outernetfoundation.org";

            App.state.plerionAPIBaseUrl.ExecuteSet(plerionBaseURL);

            if (env.loginAutomatically)
                App.ExecuteAction(new LogInAction(env.username, env.password));

            LocalCaptureController.Initialize();
            ZedCaptureController.Initialize();
            Instantiate(captureControllerPrefab);
            Instantiate(localizationManager);

            Destroy(this);
        }
    }
}