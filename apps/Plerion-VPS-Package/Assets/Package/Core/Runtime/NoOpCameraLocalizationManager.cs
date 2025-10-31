using UnityEngine;

namespace Plerion.VPS
{
    public class NoOpCameraLocalizationManager : ICameraLocalizationManager
    {
        public bool isRunning { get; private set; }

        public void Start()
            => isRunning = true;

        public void Stop()
            => isRunning = false;
    }
}