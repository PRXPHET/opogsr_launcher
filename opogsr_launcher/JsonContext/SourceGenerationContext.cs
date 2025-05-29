using opogsr_launcher.Managers;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace opogsr_launcher.JsonContext;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(GithubAsset))]
[JsonSerializable(typeof(GithubRelease))]
[JsonSerializable(typeof(List<IndexData>))]
[JsonSerializable(typeof(IndexData))]
public partial class SourceGenerationContext : JsonSerializerContext { }
