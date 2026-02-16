using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace opogsr_common.Managers
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

    public sealed class ChunkMetadataManager : IDisposable
    {
        private static readonly string MetaPath = Path.Combine(StaticGlobals.Locations.Launcher, "chunks.meta");

        public ConcurrentDictionary<string, ChunkedFileMetadata> Files { get; private set; }

        private static ChunkMetadataManager? Instance { get; set; } = null;

        public static ChunkMetadataManager GetInstance()
        {
            Instance ??= new ChunkMetadataManager();
            return Instance;
        }

        public ChunkMetadata GetOrCreateChunk(string name, string part_name)
        {
            if (!Files.TryGetValue(name, out var meta))
                meta = Files[name] = new ChunkedFileMetadata();

            if (!meta.Chunks.TryGetValue(part_name, out var chunk))
                chunk = meta.Chunks[part_name] = new ChunkMetadata();

            return chunk;
        }

        private ChunkMetadataManager()
        {
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            string? content = File.Exists(MetaPath) ? File.ReadAllText(MetaPath) : null;
            Files = !String.IsNullOrEmpty(content) ? JsonSerializer.Deserialize(content, SourceGenerationContext.Default.ConcurrentDictionaryStringChunkedFileMetadata)! : new();
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
