using System.Threading.Tasks;
using Cysharp.Threading.Tasks;

namespace Plerion.VPS
{
    public interface ICameraProvider
    {
        void Start();
        void Stop();
        UniTask<CameraImage?> GetFrame();
    }
}
