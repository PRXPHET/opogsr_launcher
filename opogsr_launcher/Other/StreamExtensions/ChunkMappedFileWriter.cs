using HarfBuzzSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace opogsr_launcher.Other.StreamExtensions
{
    public class MappedMemory
    {
        public byte[] memory { get; set; }
        public long position;
    }

    public class ChunkMappedFileWriter
    {
        private readonly FileStream _fileStream;

        private readonly string _path;

        private Task WriteTask;

        private bool IsWorking = false;

        public BlockingCollection<MappedMemory> Data = new();

        CancellationToken _ct;

        public ChunkMappedFileWriter(string path, long size, CancellationToken ct = default)
        {
            _path = path;
            _fileStream = File.Create(path);
            _fileStream.SetLength(size);
            _fileStream.Position = 0;
            _ct = ct;
        }

        public void Start()
        {
            if (!IsWorking)
            {
                WriteTask = Task.Run(UpdateWrite);
                IsWorking = true;
            }
        }

        private async Task UpdateWrite()
        {
            while (IsWorking)
            {
                if (_ct.IsCancellationRequested && Data.Count == 0)
                    break;

                if (Data.Count == 0)
                    continue;

                var item = Data.Take(_ct);
                _fileStream.Position = item.position;
                await _fileStream.WriteAsync(item.memory, _ct);
            }
        }

        public async Task DisposeAsync()
        {
            IsWorking = false;

            await WriteTask?.WaitAsync(CancellationToken.None);

            _fileStream.Close();
        }

        public async Task DestroyAsync()
        {
            await DisposeAsync();

            File.Delete(_path);
        }
    }
}
