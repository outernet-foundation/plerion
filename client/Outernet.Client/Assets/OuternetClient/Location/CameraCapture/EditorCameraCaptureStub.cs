#if UNITY_EDITOR || AUTHORING_TOOLS_ENABLED
using System;
using Cysharp.Threading.Tasks;

namespace Outernet.Client.Location
{
    public static class CameraCapture
    {
        public static void Initialize(Func<CameraImage, CameraPoseEstimate?> _) { }
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public static async UniTask Start() { }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        public static void Stop() { }
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public static async UniTask<CameraPoseEstimate?> CaptureCameraImageAndEstimatePose() => null;
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }
}
#endif