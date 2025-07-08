using Avalonia.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using opogsr_launcher.Hasher;
using opogsr_launcher.JsonContext;
using opogsr_launcher.Other;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace opogsr_launcher.Managers
{
    public enum FileStates
    {
        NoFiles = 2,
        NeedUpdate = 1,
        Every = 0
    }

    public class GithubAsset
    {
        [JsonPropertyName("name")]
        public string name { get; set; }
        [JsonPropertyName("size")]
        public ulong size { get; set; }
        [JsonPropertyName("url")]
        public string url { get; set; }
    }

    public class GithubRelease
    {
        [JsonPropertyName("assets")]
        public List<GithubAsset> assets { get; set; }
    }

    public class IndexData
    {
        [JsonPropertyName("name")]
        public string name { get; set; }
        [JsonPropertyName("hash")]
        public string hash { get; set; }
        [JsonPropertyName("base_file")]
        public bool base_file { get; set; }
        [JsonPropertyName("directory")]
        public string directory { get; set; }
    }

    public class DownloadState
    {
        public ConcurrentDictionary<string, ulong> bytesRead = new();
        public ulong totalSize = 0;
    }

    public class GithubManager
    {
        private HttpClient api_client = new();
        private HttpClient download_client = new();

        private GithubRelease release = new();

        private List<IndexData> data = new();
        private ConcurrentBag<IndexData> not_validated_data = new();

        private Task ReadRepoTask;

        private readonly string token;
        private readonly string repo;

        private async Task ReadConfig()
        {
            GithubAsset asset = release.assets.Find(x => x.name == "index.json");

            if (asset is null)
            {
                Logger.Exception(new Exception("Can't find index.json file in release."));
                return;
            }

            var httpResult = await download_client.GetAsync(asset.url);
            httpResult.EnsureSuccessStatusCode();
            string contents = await httpResult.Content.ReadAsStringAsync();
            data = JsonSerializer.Deserialize(contents, SourceGenerationContext.Default.ListIndexData);
        }

        private async Task ReadRelease()
        {
            var httpResult = await api_client.GetAsync("releases/latest");
            httpResult.EnsureSuccessStatusCode();
            string contents = await httpResult.Content.ReadAsStringAsync();
            release = JsonSerializer.Deserialize(contents, SourceGenerationContext.Default.GithubRelease);
        }

        private async Task ReadRepo()
        {
            await ReadRelease();
            await ReadConfig();
        }

        public async Task<FileStates> Validate()
        {
            await ReadRepoTask.WaitAsync(CancellationToken.None);

            List<IndexData> no_json_data = [.. data.FindAll(x => !x.name.EndsWith(".json"))];

            if (no_json_data.Count == 0)
                Logger.Exception(new Exception("Validate data count is 0. Something strange."));

            await Parallel.ForEachAsync(no_json_data, new ParallelOptions() { MaxDegreeOfParallelism = 8 }, async (d, ct) =>
            {
                string directory = Path.Combine(StaticGlobals.Locations.Start, d.directory ?? "");
                if (!Directory.Exists(directory))
                {
                    not_validated_data.Add(d);
                    return;
                }

                string path = Path.Combine(directory, d.name);
                if (!File.Exists(path))
                {
                    not_validated_data.Add(d);
                    return;
                }
                else
                    File.SetAttributes(path, FileAttributes.Normal);

                if (d.hash != await FileHasher.XxHashFromFile(path))
                {
                    not_validated_data.Add(d);
                }
            });
            return no_json_data.Count == not_validated_data.Count ? FileStates.NoFiles : not_validated_data.Count != 0 ? FileStates.NeedUpdate : FileStates.Every;
        }

        public async Task<bool> DownloadInvalid(IProgress<(string name, ulong Total)>? progress)
        {
            var cts = new CancellationTokenSource();

            var task = Parallel.ForEachAsync(not_validated_data, new ParallelOptions() { MaxDegreeOfParallelism = 4, CancellationToken = cts.Token }, async (d, ct) =>
            {
                bool success = await DownloadFile(d, progress);
                if (!success)
                    cts.Cancel();
            });

            await task;

            if (!cts.IsCancellationRequested)
            {
                not_validated_data.Clear();
                return true;
            }

            return false;
        }

        public async Task<ulong> Size()
        {
            await ReadRepoTask.WaitAsync(CancellationToken.None);

            ulong size = 0;
            foreach (IndexData d in not_validated_data)
            {
                GithubAsset a = release.assets.Find(x => x.name == d.name);

                if (a is null)
                    Logger.Exception(new Exception($"Github asset is null. Name: {d.name}"));

                size += a.size;
            }
            return size;
        }

        public async Task<bool> DownloadFile(IndexData d, IProgress<(string name, ulong Total)>? progress)
        {
            GithubAsset asset = release.assets.Find(x => x.name == d.name);

            string directory = Path.Combine(StaticGlobals.Locations.Start, d.directory ?? "");

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string path = Path.Combine(directory, d.name);

            int retry = 0;

            while (retry < 5)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.SetAttributes(path, FileAttributes.Normal);
                        File.Delete(path);
                    }

                    var response = await download_client.GetAsync(asset.url, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    await using var ms = await response.Content.ReadAsStreamAsync();
                    await using var fs = File.Create(path);

                    var buffer = new byte[32768];

                    int total_bytes_read = 0;
                    int bytes_read;

                    while ((bytes_read = await ms.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fs.WriteAsync(buffer.AsMemory(0, bytes_read));

                        total_bytes_read += bytes_read;

                        progress?.Report((asset.name, Convert.ToUInt64(total_bytes_read)));
                    }

                    progress?.Report((asset.name, Convert.ToUInt64(total_bytes_read)));

                    if (d.hash != await FileHasher.XxHashFromFile(fs))
                    {
                        Logger.Exception(new Exception("Hash of downloaded file doesn't match with cached hash."));
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    retry++;

                    Logger.Error($"Error file during download. Exception: {ex.Message}, Name: {d.name}, Attempt {retry}.");
                }
                finally
                {
                    if (retry >= 5)
                    {
                        if (File.Exists(path))
                        {
                            File.SetAttributes(path, FileAttributes.Normal);
                            File.Delete(path);
                        }
                    }
                }
            }

            return false;
        }

        public GithubManager(IServiceProvider serviceProvider)
        {
            IConfiguration config = serviceProvider.GetRequiredService<IConfiguration>();

            token = config["GithubToken"];
            repo = config["GithubRepo"];

            api_client.BaseAddress = new Uri($"https://api.github.com/repos/{repo}/");
            api_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            api_client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("product", "1"));
            api_client.Timeout = TimeSpan.FromSeconds(15);

            download_client.DefaultRequestHeaders.Add("Accept", "application/octet-stream");
            download_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            download_client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("product", "1"));
            download_client.DefaultRequestHeaders.ExpectContinue = false;
            download_client.Timeout = TimeSpan.FromHours(1);

            ReadRepoTask = Task.Run(ReadRepo);
        }
    }
}
