using Avalonia.Vulkan;
using DynamicData;
using HarfBuzzSharp;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace opogsr_launcher.Extensions.StreamExtensions
{
    public class ChunkMappedFileWriter
    {
        private struct MappedMemory
        {
            public byte[] memory { get; set; }
            public long position { get; set; }
        }

        private readonly FileStream _fileStream;

        private readonly string _path;
        private readonly long _size;

        private Task WriteTask;

        private bool IsWorking = false;

        public bool IsDestroyed { get; private set; } = false;
        public bool FileCreated { get; private set; } = false;

        private readonly BlockingCollection<MappedMemory> Data = [];

        private readonly CancellationToken _ct;

        public ChunkMappedFileWriter(string path, long size, CancellationToken ct = default)
        {
            _path = path;
            _size = size;
            _fileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);

            if (_fileStream.Length == 0)
            {
                _fileStream.SetLength(_size);
                FileCreated = true;
            }

            _fileStream.Position = 0;
            _ct = ct;
        }

        public void Start()
        {
            if (IsWorking)
                return;

            WriteTask = Task.Run(ConsumeAsync, _ct);
            IsWorking = true;
        }

        public void Produce(byte[] mem, long pos)
        {
            Trace.Assert(IsWorking);

            ArgumentNullException.ThrowIfNull(mem);
            ArgumentOutOfRangeException.ThrowIfNegative(pos);

            Data.Add(new ()
            {
                memory = mem,
                position = pos
            });
        }

        private async Task ConsumeAsync()
        {
            foreach (var item in Data.GetConsumingEnumerable(_ct))
            {
                try
                {
                    _fileStream.Position = item.position;
                    await _fileStream.WriteAsync(item.memory, _ct);
                }
                catch
                {
                    throw;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(item.memory, true);
                }
            }

            IsWorking = false;
        }

        public async Task ZeroFill(long start)
        {
            if (!IsWorking)
            {
                throw new InvalidOperationException("Attempt to fill with zeros when writer is not working.");
            }

            ArgumentOutOfRangeException.ThrowIfNegative(start);

            var memory = new MappedMemory()
            {
                memory = new byte[Math.Min(StaticGlobals.Variables.LargeChunkSize, _size - start)],
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

            Data.CompleteAdding();

            await WriteTask?.WaitAsync(CancellationToken.None);

            _fileStream.Close();
        }

        public async Task DestroyAsync()
        {
            if (IsDestroyed)
                return;

            await DisposeAsync();

            File.Delete(_path);

            IsDestroyed = true;
        }
    }
}
