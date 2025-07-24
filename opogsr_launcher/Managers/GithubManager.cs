using opogsr_launcher.Extensions;
using opogsr_launcher.Extensions.StreamExtensions;
using opogsr_launcher.Hasher;
using opogsr_launcher.JsonContext;
using opogsr_launcher.Properties;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        [JsonPropertyName("upload_url")]
        public string upload_url { get; set; }
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
        public string? directory { get; set; }
    }

    public class GithubDownloadManager : GithubManager
    {
        private ConcurrentBag<IndexData> not_validated_data = new();

        public bool ValidateBase(IndexData d, Stream stream)
        {
            List<GithubAsset> assets = [.. release.assets.FindAll(x => x.name.StartsWith(d.name + ".part_", StringComparison.InvariantCultureIgnoreCase)).OrderBy(a => a.name)];

            ulong size = 0;
            assets.ForEach(a => size += a.size);

            return size == Convert.ToUInt64(stream.Length);
        }

        public async Task<FileStates> Validate()
        {
            await RepoTask();

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

                using var stream = File.OpenRead(path);

                if (d.base_file)
                {
                    if (!ValidateBase(d, stream))
                        not_validated_data.Add(d);

                    return;
                }

                Logger.Info(Resources.CalculatingHashForFile, d.name);
                if (d.hash != await FileHasher.XxHashFromFile(stream))
                    not_validated_data.Add(d);

            });
            return no_json_data.Count == not_validated_data.Count ? FileStates.NoFiles : not_validated_data.Count != 0 ? FileStates.NeedUpdate : FileStates.Every;
        }

        public async Task<bool> DownloadInvalid(IProgress<(string name, ulong Total)>? progress)
        {
            static async Task EnsureHash(IndexData d, string path)
            {
                Logger.Info(Resources.CalculatingHashForFile, d.name);
                string hash = await FileHasher.XxHashFromFile(path);
                if (d.hash != hash)
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                    Logger.Exception(new Exception("Hash of downloaded file doesn't match with cached hash."));
                }
            }

            var cts = new CancellationTokenSource();

            var semaphore = new SemaphoreSlim(4);

            var tasks = not_validated_data.WithMaxConcurrency(semaphore, async (d) =>
            {
                List<GithubAsset> assets = [.. release.assets.FindAll(x => x.name.StartsWith(d.name + ".part_", StringComparison.InvariantCultureIgnoreCase)).OrderBy(a => a.name)];

                bool success = false;

                string directory = Path.Combine(StaticGlobals.Locations.Start, d.directory ?? "");

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (assets.Count != 0)
                {
                    string output = Path.Combine(directory, d.name);
                    success = await DownloadFile(d, assets, directory, output, progress, cts.Token, semaphore);

                    if (success)
                        await EnsureHash(d, output);
                }
                else
                {
                    GithubAsset asset = release.assets.Find(x => x.name == d.name);

                    string path = Path.Combine(directory, d.name);

                    success = await DownloadFile(asset, path, progress, cts.Token);

                    if (success)
                        await EnsureHash(d, path);
                }

                if (!success && !cts.IsCancellationRequested)
                    cts.Cancel();

                return success;
            });

            await Task.WhenAll(tasks);

            if (!cts.IsCancellationRequested)
            {
                not_validated_data.Clear();
                return true;
            }

            return false;
        }

        private static void EnsureFile(string path)
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
        }

        public async Task<bool> DownloadFile(IndexData d, List<GithubAsset> assets, string directory, string output, IProgress<(string name, ulong Total)>? progress, CancellationToken ct, SemaphoreSlim semaphore)
        {
            long size = 0;
            assets.ForEach(a => size += Convert.ToInt64(a.size));

            string path = Path.Combine(directory, d.name);

            EnsureFile(path);

            ChunkMappedFileWriter writer = new(Path.Combine(directory, d.name), size, ct);

            var tasks = assets.WithMaxConcurrency(semaphore, async (asset) => 
            {
                try
                {
                    var response = await download_client.GetAsync(asset.url, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    await using var ms = await response.Content.ReadAsStreamAsync();

                    var buffer = new byte[32768];

                    int total_bytes_read = 0;
                    int bytes_read;

                    long position = assets.IndexOf(asset) * StaticGlobals.Variables.LargeChunkSize;

                    writer.Start();

                    while ((bytes_read = await ms.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        if (ct.IsCancellationRequested)
                        {
                            await writer.DestroyAsync();
                            return false;
                        }

                        var read_memory = new byte[bytes_read];
                        Buffer.BlockCopy(buffer, 0, read_memory, 0, bytes_read);

                        writer.Data.Add(new MappedMemory() 
                        { 
                            memory = read_memory,
                            position = position,
                        });

                        position += bytes_read;
                        total_bytes_read += bytes_read;

                        progress?.Report((asset.name, Convert.ToUInt64(total_bytes_read)));
                    }

                    progress?.Report((asset.name, Convert.ToUInt64(total_bytes_read)));

                    return true;
                }
                catch (Exception ex)
                {
                    await writer.DestroyAsync();
                    Logger.Error($"Error file during download. Name: {asset.name}");
                    Logger.Exception(ex);
                }

                return false;

            }).ToList();

            var results = await Task.WhenAll(tasks);

            await writer.DisposeAsync();

            foreach (bool result in results)
                if (!result)
                    return false;

            return true;
        }

        public async Task<bool> DownloadFile(GithubAsset asset, string path, IProgress<(string name, ulong Total)>? progress, CancellationToken ct)
        {
            int retry = 0;

            while (retry < 5)
            {
                try
                {
                    EnsureFile(path);

                    var response = await download_client.GetAsync(asset.url, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    await using var ms = await response.Content.ReadAsStreamAsync();
                    await using var fs = File.Create(path);

                    var buffer = new byte[32768];

                    int total_bytes_read = 0;
                    int bytes_read;

                    while ((bytes_read = await ms.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        if (ct.IsCancellationRequested)
                        {
                            EnsureFile(path);
                            return false;
                        }

                        await fs.WriteAsync(buffer.AsMemory(0, bytes_read));

                        total_bytes_read += bytes_read;

                        progress?.Report((asset.name, Convert.ToUInt64(total_bytes_read)));
                    }

                    progress?.Report((asset.name, Convert.ToUInt64(total_bytes_read)));

                    return true;
                }
                catch (Exception ex)
                {
                    retry++;

                    Logger.Error($"Error file during download. Exception: {ex.Message}, Name: {asset.name}, Attempt {retry}.");
                }
                finally
                {
                    if (retry >= 5)
                    {
                        EnsureFile(path);
                    }
                }
            }

            return false;
        }

        public async Task<ulong> Size()
        {
            ulong size = 0;

            foreach (IndexData d in not_validated_data)
                size += await Size(d.name);

            return size;
        }

        public GithubDownloadManager(string Token, string Repo) : base(Token, Repo) {}
    }

    public class GithubManager
    {
        private readonly string token;
        private readonly string repo;

        protected HttpClient api_client = new();
        protected HttpClient download_client = new();

        protected GithubRelease release = new();
        public List<IndexData> data { get; private set; } = new();

        protected Task ReadRepoTask;

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
            var httpResult = await api_client.GetAsync("releases/tags/base");
            httpResult.EnsureSuccessStatusCode();
            string contents = await httpResult.Content.ReadAsStringAsync();
            release = JsonSerializer.Deserialize(contents, SourceGenerationContext.Default.GithubRelease);
            release.upload_url = release.upload_url.Replace("{?name,label}", "");
        }

        private async Task ReadRepo()
        {
            await ReadRelease();
            await ReadConfig();
        }

        public async Task RepoTask() => await ReadRepoTask.WaitAsync(CancellationToken.None);

        public async Task<ulong> Size(string name, bool no_assert = false)
        {
            await RepoTask();

            ulong size = 0;
            List<GithubAsset> assets = [.. release.assets.FindAll(x => x.name.StartsWith(name + ".part_", StringComparison.InvariantCultureIgnoreCase)).OrderBy(a => a.name)];

            if (assets.Count != 0)
                assets.ForEach(a => size += a.size);
            else
            {
                GithubAsset? a = release.assets.Find(x => x.name == name);

                if (a is null)
                {
                    if (no_assert)
                        return 0;
                    else
                        Logger.Exception(new Exception($"Github asset is null. Name: {name}"));
                }

                size += a.size;
            }

            return size;
        }

        public GithubManager(string Token, string Repo)
        {
            token = Token;
            repo = Repo;

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
