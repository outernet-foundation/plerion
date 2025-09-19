using UnityEngine;
using Cysharp.Threading.Tasks;

namespace Plerion.VPS
{
    public class VisualPositioningSystem : MonoBehaviour
    {
        private void Awake()
        {
            ImmersalNative.Initialize();

            ImmersalNative.SetInteger("LocalizationMaxPixels", 960 * 720);
            ImmersalNative.SetInteger("NumThreads", 1);

            PlaneDetector.Initialize().Forget();
            LocalizedReferenceFrame.Initialize();
            MapManager.Enable();
            Localizer.Initialize();

            Localizer.StartCameraCapture();
        }

        private void Update()
        {
            PlaneDetector.Update();
        }

        private void OnDestroy()
        {

        }
    }
}