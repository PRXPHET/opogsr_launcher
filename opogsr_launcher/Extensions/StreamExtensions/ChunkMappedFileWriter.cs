using DynamicData;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace opogsr_launcher.Extensions.StreamExtensions
{
    internal class ChunkMappedFileWriter : IDisposable
    {
        private struct MappedMemory
        {
            public byte[] memory { get; set; }
            public long position { get; set; }
            public int count { get; set; }
        }

        private readonly FileStream _fileStream;

        private readonly long _size;

        private Task WriteTask;

        private bool IsWorking = false;

        public bool IsDisposed { get; private set; } = false;

        public bool FileCreated { get; private set; } = false;
        public bool FileUpdated { get; private set; } = false;

        public bool DateChanged { get; private set; } = false;

        private readonly BlockingCollection<MappedMemory> Data = [];

        private readonly CancellationToken _ct;

        public ChunkMappedFileWriter(string path, long size, DateTime dt,  CancellationToken ct = default)
        {
            _size = size;
            _fileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);

            if (_fileStream.Length == 0)
            {
                _fileStream.SetLength(_size);
                FileCreated = true;
            }
            else if (File.GetCreationTimeUtc(path) != dt || File.GetLastWriteTimeUtc(path) != dt)
            {
                _fileStream.SetLength(_size);
                FileUpdated = true;
            }

            _fileStream.Position = 0;
            _ct = ct;

            File.SetCreationTimeUtc(_fileStream.SafeFileHandle, dt);
            File.SetLastWriteTimeUtc(_fileStream.SafeFileHandle, dt);
            
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        }

        public FileStream GetStream()
        {
            return IsDisposed
                   ? throw new ObjectDisposedException(nameof(ChunkMappedFileWriter))
                   : _fileStream ?? throw new InvalidOperationException("Internal stream is null.");
        }

        public void Start()
        {
            if (IsWorking)
                return;

            WriteTask = Task.Run(ConsumeAsync, _ct);
            IsWorking = true;
        }

        public void Produce(byte[] mem, long pos, int cnt)
        {
            Trace.Assert(IsWorking);

            ArgumentNullException.ThrowIfNull(mem);
            ArgumentOutOfRangeException.ThrowIfNegative(pos);

            Data.Add(new ()
            {
                memory = mem,
                position = pos,
                count = cnt
            });
        }

        private async Task ConsumeAsync()
        {
            foreach (var item in Data.GetConsumingEnumerable(_ct))
            {
                try
                {
                    _fileStream.Position = item.position;
                    await _fileStream.WriteAsync(item.memory, 0, item.count);
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

        public void UpdateDate(DateTime dt)
        {
            if (DateChanged)
                return;

            DateChanged = true;

            File.SetCreationTimeUtc(_fileStream.SafeFileHandle, dt);
            File.SetLastAccessTimeUtc(_fileStream.SafeFileHandle, dt);
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsWorking = false;

            Data.CompleteAdding();

            WriteTask?.Wait();

            _fileStream.Close();

            IsDisposed = true;

            GC.SuppressFinalize(this);
        }

        private void OnProcessExit(object? sender, EventArgs e) => Dispose();
    }
}
