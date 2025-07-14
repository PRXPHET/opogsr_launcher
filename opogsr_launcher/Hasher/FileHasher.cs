using System;
using System.IO;
using System.IO.Hashing;
using System.Threading.Tasks;

namespace opogsr_launcher.Hasher
{
    public class FileHasher
    {
        public static async Task<String> XxHashFromFile(string filename)
        {
            FileStream stream = File.OpenRead(filename);

            XxHash3 hash = new();
            await hash.AppendAsync(stream);
            stream.Close();
            return BitConverter.ToString(hash.GetHashAndReset()).Replace("-", "").ToLowerInvariant();
        }

        public static async Task<String> XxHashFromFile(FileStream stream)
        {
            stream.Position = 0;

            XxHash3 hash = new();
            await hash.AppendAsync(stream);
            return BitConverter.ToString(hash.GetHashAndReset()).Replace("-", "").ToLowerInvariant();
        }
    }
}
