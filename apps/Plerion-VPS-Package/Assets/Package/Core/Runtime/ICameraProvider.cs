using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Plerion.Core
{
    public interface ICameraProvider
    {
        void Start();
        void Stop();
        UniTask<(byte[], Vector3, Quaternion)> GetFrameJPG();
    }

    public class NoOpCameraProvider : ICameraProvider
    {
        public void Start() { }

        public void Stop() { }

        public UniTask<(byte[], Vector3, Quaternion)> GetFrameJPG() =>
            UniTask.FromResult((default(byte[]), default(Vector3), default(Quaternion)));
    }
}
