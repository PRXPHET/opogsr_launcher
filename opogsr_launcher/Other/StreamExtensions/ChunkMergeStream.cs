using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace opogsr_launcher.Other.StreamExtensions
{
    public class ChunkMergeStream
    {
        public static void Merge(List<string> paths, string output)
        {
            using var outputStream = File.Create(output);

            foreach (string p in paths)
            {
                using var inputStream = File.OpenRead(p);
                inputStream.CopyTo(outputStream);
            }
        }
    }
}
