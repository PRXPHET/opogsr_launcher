using opogsr_launcher.Managers;
using opogsr_launcher.Managers.Github;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace opogsr_launcher.JsonContext;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(GithubAsset))]
[JsonSerializable(typeof(GithubRelease))]
[JsonSerializable(typeof(List<IndexData>))]
[JsonSerializable(typeof(IndexData))]
[JsonSerializable(typeof(ChunkMetadata))]
[JsonSerializable(typeof(ConcurrentDictionary<string, ChunkMetadata>))]
[JsonSerializable(typeof(ChunkedFileMetadata))]
[JsonSerializable(typeof(ConcurrentDictionary<string, ChunkedFileMetadata>))]
public partial class SourceGenerationContext : JsonSerializerContext { }
