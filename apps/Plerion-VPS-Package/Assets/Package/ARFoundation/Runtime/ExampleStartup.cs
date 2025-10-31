using System.Linq;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Plerion.VPS;
using Cysharp.Threading.Tasks;

namespace Plerion.VPS.ARFoundation
{
    public class ExampleStartup : MonoBehaviour
    {
        public ARCameraManager arCameraManager;
        public string username;
        public string password;

        private void Start()
        {
            EnableVPS(username, password).Forget();
        }

        private async UniTask EnableVPS(string username, string password)
        {
            // First we need a camera provider. This will be used initially to provide
            // the camera intrinsics, then at the end of the method to start visual
            // localization. The GetCameraProvider() method is a utility method that 
            // decides which camera provider to use based on the target platform.
            var cameraProvider = GetCameraLocalizationManager();

            // First, we initialize the VPS with the username and password for your
            // project. This allows the VPS to communicate with our servers and access
            // the maps you have set up. Eventually this username and password combination 
            // will be set up by the end user, but for now we just use project-wide ones.
            VisualPositioningSystem.Initialize(username, password);

            // Now we start the localization session. This is a structure used 
            // by the server to load the maps you want to localize against. 
            // Generally speaking, everyinstance of an appusing the Plerion VPS
            // will have exactly one localization session.
            await VisualPositioningSystem.StartLocalizationSession();

            // Once the session has started, we're ready to load the localization 
            // maps we want to localize against. For now, we're going to get all
            // the maps we have set up with the GetLocalizationMapsAsync() method
            // and load them.  
            var maps = await VisualPositioningSystem.GetLocalizationMapsAsync();
            await VisualPositioningSystem.LoadLocalizationMaps(maps.Select(x => x.id).ToList());
            await UniTask.SwitchToMainThread();

            // Finally, we start the camera. Plerion's VPS library offers several 
            // camera managers, as well as an interface for you to write your
            // own (ICameraLocalizationManager). The GetCameraProvider() method is
            // a utility method that decides which camera manager to use based
            // on what platform you're using.
            GetCameraLocalizationManager().Start();
        }

        private ICameraLocalizationManager GetCameraLocalizationManager()
        {
#if UNITY_EDITOR
            return new NoOpCameraLocalizationManager(); // In the editor there is no camera, so use an empty manager
#elif UNITY_ANDROID
            return new ARFoundationCameraLocalizationManager(arCameraManager, manageCameraEnabledState: false); // On Android, use the ARFoundation camera manager
#endif
        }
    }
}