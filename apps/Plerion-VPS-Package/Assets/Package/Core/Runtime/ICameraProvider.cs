using Cysharp.Threading.Tasks;
using UnityEngine;
using PinholeCameraConfig = PlerionApiClient.Model.PinholeCameraConfig;

namespace Plerion.Core
{
    public interface ICameraProvider
    {
        void Start();
        void Stop();
        UniTask<PinholeCameraConfig> GetCameraConfig();
        UniTask<(byte[], Vector3, Quaternion)> GetFrameJPG();
    }

    public class NoOpCameraProvider : ICameraProvider
    {
        public void Start() { }

        public void Stop() { }

        public async UniTask<PinholeCameraConfig> GetCameraConfig() =>
            new PinholeCameraConfig(
                model: PinholeCameraConfig.ModelEnum.PINHOLE,
                orientation: PinholeCameraConfig.OrientationEnum.TOPLEFT,
                width: 0,
                height: 0,
                fx: 0,
                fy: 0,
                cx: 0,
                cy: 0
            );

        public UniTask<(byte[], Vector3, Quaternion)> GetFrameJPG() =>
            UniTask.FromResult((default(byte[]), default(Vector3), default(Quaternion)));
    }
}
