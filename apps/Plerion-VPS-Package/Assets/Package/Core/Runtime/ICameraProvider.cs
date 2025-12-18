using Cysharp.Threading.Tasks;
using UnityEngine;
using PinholeCameraConfig = PlerionApiClient.Model.PinholeCameraConfig;

namespace Plerion.Core
{
    public interface ICameraProvider
    {
        void Initialize();
        void Start();
        void Stop();
        bool GetCameraConfig(out PinholeCameraConfig cameraConfig);
        UniTask<(byte[], Vector3, Quaternion)> GetFrame();
    }

    public class NoOpCameraProvider : ICameraProvider
    {
        public void Initialize() { }

        public void Start() { }

        public void Stop() { }

        public bool GetCameraConfig(out PinholeCameraConfig cameraConfig)
        {
            cameraConfig = new PinholeCameraConfig(
                model: PinholeCameraConfig.ModelEnum.PINHOLE,
                orientation: PinholeCameraConfig.OrientationEnum.TOPLEFT,
                width: 0,
                height: 0,
                fx: 0,
                fy: 0,
                cx: 0,
                cy: 0
            );
            return true;
        }

        public async UniTask<(byte[], Vector3, Quaternion)> GetFrame() =>
            await UniTask.FromResult<(byte[], Vector3, Quaternion)>((null, default, default));
    }
}
