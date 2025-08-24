using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Threading.Tasks;

namespace opogsr_launcher.Hasher
{
    public class FileHasher
    {
        public static async Task<string> XxHashFromFile(string filename)
        {
            FileStream stream = File.OpenRead(filename);

            XxHash3 hash = new();
            await hash.AppendAsync(stream);
            stream.Close();
            return BitConverter.ToString(hash.GetHashAndReset()).Replace("-", "").ToLowerInvariant();
        }

        public static async Task<string> XxHashFromFile(FileStream stream)
        {
            stream.Position = 0;

            XxHash3 hash = new();
            await hash.AppendAsync(stream);
            return BitConverter.ToString(hash.GetHashAndReset()).Replace("-", "").ToLowerInvariant();
        }

        public static async Task<List<string>> XxHashFromChunkedFile(FileStream stream, uint chunk_count)
        {
            List<string> list = [];
            byte[] buffer = new byte[64 * 1024];
            for (uint i = 1; i <= chunk_count; i++)
            {
                long position = Math.Min(stream.Length, StaticGlobals.Variables.LargeChunkSize * i) - (buffer.Length);
                stream.Seek(position, SeekOrigin.Begin);
                await stream.ReadExactlyAsync(buffer.AsMemory(0, buffer.Length));
                XxHash3 hash = new();
                hash.Append(buffer.AsSpan(0, buffer.Length));
                list.Add(BitConverter.ToString(hash.GetHashAndReset()).Replace("-", "").ToLowerInvariant());
                Array.Clear(buffer);
            }
            return list;
        }
    }
}
