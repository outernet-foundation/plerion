
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Plerion.VPS
{
    public class NoOpCameraProvider : ICameraProvider
    {
        public UniTask<byte[]> GetFrameJPG()
            => UniTask.FromResult(default(byte[]));

        public void Start() { }
        public void Stop() { }
    }
}