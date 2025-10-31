using UnityEngine;

namespace Plerion.VPS
{
    public interface ICameraLocalizationManager
    {
        bool isRunning { get; }
        void Start();
        void Stop();
    }
}