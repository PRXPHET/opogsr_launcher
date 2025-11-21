using opogsr_launcher.JsonContext;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace opogsr_launcher.Managers
{
    public class ChunkMetadata
    {
        [JsonPropertyName("length")]
        public long Length { get; set; } = 0;
    }

    public class ChunkedFileMetadata
    {
        [JsonPropertyName("chunks")]
        public ConcurrentDictionary<string, ChunkMetadata> Chunks { get; set; } = new();
    }

    public class ChunkMetadataManager : IDisposable
    {
        private static readonly string MetaPath = Path.Combine(StaticGlobals.Locations.Appdata, "chunks.meta");

        private ConcurrentDictionary<string, ChunkedFileMetadata> Files { get; set; }

        private static ChunkMetadataManager? Instance { get; set; } = null;

        public static ChunkMetadataManager GetInstance()
        {
            return Instance ??= new();
        }

        public ChunkMetadata GetOrCreateChunk(string name, string part_name)
        {
            ChunkedFileMetadata metadata = Files.GetOrAdd(name, c => { return new ChunkedFileMetadata(); });
            return metadata.Chunks.GetOrAdd(part_name, c => { return new ChunkMetadata(); });
        }

        private ChunkMetadataManager()
        {
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            string content = File.Exists(MetaPath) ? File.ReadAllText(MetaPath) : null;
            Files = !String.IsNullOrEmpty(content) ? JsonSerializer.Deserialize(content, SourceGenerationContext.Default.ConcurrentDictionaryStringChunkedFileMetadata) : new();
        }

        public void Dispose()
        {
            File.WriteAllText(MetaPath, JsonSerializer.Serialize(Files, SourceGenerationContext.Default.ConcurrentDictionaryStringChunkedFileMetadata));

            Files.Clear();

            Instance = null;

            GC.SuppressFinalize(this);
        }

        ~ChunkMetadataManager() => Dispose();

        private void OnProcessExit(object? sender, EventArgs e) => Dispose();

        private void Destroy()
        {
            Dispose();

            if (File.Exists(MetaPath))
                File.Delete(MetaPath);
        }

        public static void DestroyInstance()
        {
            Instance?.Destroy();
        }
    }
}
