using System.IO;
using Cysharp.Threading.Tasks;
using ICSharpCode.SharpZipLib.Tar;
using System.Text;

public static class TarUtils
{
    public static async UniTask<Stream> CreateTarAsync(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException(directoryPath);

        var baseDirectory = new DirectoryInfo(directoryPath);
        var memoryStream = new MemoryStream();

        using (var tarStream = new TarOutputStream(memoryStream, Encoding.UTF8))
        {
            tarStream.IsStreamOwner = false;

            foreach (var file in baseDirectory.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(baseDirectory.FullName, file.FullName).Replace('\\', '/');
                var entry = TarEntry.CreateTarEntry(relativePath);
                entry.Size = file.Length;
                entry.ModTime = file.LastWriteTimeUtc;

                tarStream.PutNextEntry(entry);
                using (var fileStream = file.OpenRead())
                {
                    await fileStream.CopyToAsync(tarStream).ConfigureAwait(false);
                }
                tarStream.CloseEntry();
            }

            tarStream.Close();
        }

        memoryStream.Position = 0;
        return memoryStream;
    }
}
