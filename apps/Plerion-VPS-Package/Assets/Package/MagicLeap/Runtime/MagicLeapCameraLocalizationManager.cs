#if UNITY_LUMIN
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

using UnityEngine;

namespace Plerion.VPS.MagicLeap
{
    public class MagicLeapCameraLocalizationManager : ICameraLocalizationManager
    {
        public bool isRunning { get; private set; }
        public bool manageCameraEnabledState;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _intrinsicsSet = false;
        private bool _localizing = false;

        public MagicLeapCameraLocalizationManager(bool manageCameraEnabledState = true)
        {
            this.manageCameraEnabledState = manageCameraEnabledState;

            if (manageCameraEnabledState)
                MagicLeapCamera.Stop();
        }

        public void Start()
        {
            if (isRunning)
                return;

            isRunning = true;

            if (!MagicLeapCamera.initialized)
                MagicLeapCamera.Initialize();

            _cancellationTokenSource = new CancellationTokenSource();
            StartInternal(_cancellationTokenSource.Token).Forget();
        }

        public void Stop()
        {
            if (!isRunning)
                return;

            isRunning = false;

            if (manageCameraEnabledState)
                MagicLeapCamera.Stop();

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            _intrinsicsSet = false;
            _localizing = false;
        }

        private async UniTask StartInternal(CancellationToken cancellationToken = default)
        {
            if (manageCameraEnabledState)
                await MagicLeapCamera.Start();

            cancellationToken.ThrowIfCancellationRequested();

            await UniTask.WaitUntil(() => VisualPositioningSystem.LocalizationSessionActive, cancellationToken: cancellationToken);
            await UniTask.SwitchToMainThread(cancellationToken: cancellationToken);

            MagicLeapCamera.onFrameReceived += HandleFrameReceived;
        }

        private void HandleFrameReceived(byte[] frame, NativeBindings.MLCameraIntrinsicCalibrationParameters intrinsics)
        {
            if (_localizing)
                return;

            if (!_intrinsicsSet)
                SetIntrinsicsFromFrame(frame, intrinsics, _cancellationTokenSource.Token).Forget();

            LocalizeWithFrame(frame, _cancellationTokenSource.Token).Forget();
        }

        private async UniTask SetIntrinsicsFromFrame(byte[] frame, NativeBindings.MLCameraIntrinsicCalibrationParameters intrinsics, CancellationToken cancellationToken = default)
        {
            _localizing = true; //not actually localizing, but we need to stall localization until this is complete
            await VisualPositioningSystem.SetLocalizationSessionCameraIntrinsicsAsync(new CameraIntrinsics()
            {
                resolution = new Vector2Int((int)intrinsics.Width, (int)intrinsics.Height),
                focalLength = new Vector2(intrinsics.FocalLength.X, intrinsics.FocalLength.Y),
                principalPoint = new Vector2(intrinsics.PrincipalPoint.X, intrinsics.PrincipalPoint.Y),
            });

            await UniTask.SwitchToMainThread(cancellationToken: cancellationToken);
            _localizing = false;
        }

        private async UniTask LocalizeWithFrame(byte[] frame, CancellationToken cancellationToken = default)
        {
            _localizing = true;

            await VisualPositioningSystem.LocalizeFromCameraImage(frame, Camera.main.transform.position, Camera.main.transform.rotation);
            await UniTask.SwitchToMainThread(cancellationToken: cancellationToken);

            _localizing = false;
        }
    }
}
#endif