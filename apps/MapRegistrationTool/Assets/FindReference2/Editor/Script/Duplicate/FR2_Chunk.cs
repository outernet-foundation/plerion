using System.IO;
namespace vietlabs.fr2
{
    internal class FR2_Chunk
    {
        public byte[] buffer;
        public string file;
        public long size;
        public FileStream stream;
        public bool streamError;
        public bool streamInited;
    }
}
