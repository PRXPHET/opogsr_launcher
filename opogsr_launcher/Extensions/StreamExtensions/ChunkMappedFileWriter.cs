using DynamicData;
using HarfBuzzSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace opogsr_launcher.Extensions.StreamExtensions
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

        public bool IsDestroyed { get; private set; } = false;
        public bool FileCreated { get; private set; } = false;

        public BlockingCollection<MappedMemory> Data = new();

        CancellationToken _ct;

        public ChunkMappedFileWriter(string path, long size, CancellationToken ct = default)
        {
            _path = path;
            _fileStream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.None);

            if (_fileStream.Length == 0)
            {
                _fileStream.SetLength(size);
                FileCreated = true;
            }

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

                while (Data.TryTake(out var item))
                {
                    await Task.Delay(100);
                    _fileStream.Position = item.position;
                    await _fileStream.WriteAsync(item.memory, _ct);
                }
            }
        }

        public async Task ZeroFill(long start, long end)
        {
            if (!IsWorking)
            {
                throw new InvalidOperationException("Attempt to fill with zeros when writer is not working.");
            }

            if (start < 0 || end < start || end > _fileStream.Length)
            {
                throw new ArgumentOutOfRangeException("Invalid start or end position.");
            }

            var memory = new MappedMemory()
            {
                memory = new byte[end - start],
                position = start,
            };

            Data.Add(memory);

            while (Data.IndexOf(memory) != -1)
            {
                await Task.Delay(100);
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
            if (IsDestroyed)
            {
                await DisposeAsync();

                File.Delete(_path);

                IsDestroyed = true;
            }
        }
    }
}
