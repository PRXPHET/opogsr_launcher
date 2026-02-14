using opogsr_launcher.Extensions;
using opogsr_launcher.Extensions.StreamExtensions;
using opogsr_launcher.Hasher;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace opogsr_launcher.Managers.Github
{
    internal class RateLimitInfo
    {
        public long Remaining;
        public DateTime Reset;
    }

    internal class GithubDownloadManager : GithubManager
    {
        private readonly ConcurrentBag<IndexData> not_validated_data = [];

        private RateLimitInfo? RateInfoUnauthorized = null;

        protected HttpClient download_client_unauthorized;
        protected HttpClient download_client;

        public bool IsRateLimited { get; private set; } = false;

        private bool ValidateChunked(IndexData d, FileStream stream)
        {
            List<GithubAsset> assets = [.. release.assets.FindAll(x => x.name.StartsWith(d.name + ".part_", StringComparison.InvariantCultureIgnoreCase)).OrderBy(a => a.name)];
            List<string> hash_list = FileHasher.XxHashFromChunkedFile(stream, Convert.ToUInt32(assets.Count)).GetAwaiter().GetResult();
            if (!hash_list.SequenceEqual(d.hash_list))
                return false;

            return true;
        }

        private bool ValidateDate(IndexData d, FileStream stream)
        {
            List<GithubAsset> assets = [.. release.assets.FindAll(x => x.name.StartsWith(d.name + ".part_", StringComparison.InvariantCultureIgnoreCase)).OrderBy(a => a.name)];

            if (assets.Count > 0)
            {
                DateTime dt = assets.First().createdAtTime;
                if (File.GetCreationTimeUtc(stream.SafeFileHandle) == dt && File.GetLastWriteTimeUtc(stream.SafeFileHandle) == dt)
                    return true;
            }
            else
            {
                GithubAsset a = release.assets.Find(x => x.name == d.name);
                if (File.GetCreationTimeUtc(stream.SafeFileHandle) == a.createdAtTime && File.GetLastWriteTimeUtc(stream.SafeFileHandle) == a.createdAtTime)
                    return true;
            }

            return false;
        }

        private bool ValidateSize(IndexData d, FileStream stream)
        {
            List<GithubAsset> assets = [.. release.assets.FindAll(x => x.name.StartsWith(d.name + ".part_", StringComparison.InvariantCultureIgnoreCase)).OrderBy(a => a.name)];

            ulong size = 0;
            if (assets.Count > 0)
                assets.ForEach(a => size += a.size);
            else
            {
                GithubAsset a = release.assets.Find(x => x.name == d.name);
                size = a.size;
            }

            return size == Convert.ToUInt64(stream.Length);
        }

        public async Task<bool> ValidateLauncher()
        {
            Trace.Assert(IsInitialized);

#if DEBUG
            if (Debugger.IsAttached)
                return true;
#endif

            IndexData ld = data.Find(x => !x.game_file && x.name.Equals(StaticGlobals.Names.Executable, StringComparison.InvariantCulture));

            using FileStream stream = new(Path.Combine(StaticGlobals.Locations.Start, StaticGlobals.Names.Executable), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            string selfHash = await FileHasher.XxHashFromFile(stream);

            return selfHash.Equals(ld.hash, StringComparison.InvariantCultureIgnoreCase);
        }

        public async Task UpdateLauncher(IProgress<(string name, ulong Total)>? progress)
        {
            IndexData ld = data.Find(x => !x.game_file && x.name.Equals(StaticGlobals.Names.Executable, StringComparison.InvariantCulture));

            GithubAsset asset = release.assets.Find(x => x.name == ld.name);

            string cur_name = StaticGlobals.Names.Executable;
            string cur_path = Path.Combine(StaticGlobals.Locations.Start, cur_name);

            string temp_name = StaticGlobals.Names.Executable + "_old";
            string temp_path = Path.Combine(Path.GetTempPath(), temp_name);

            int retry = 0;

            int tries = (int)Other.Settings.Storage.GetValue("max_amount_of_tries");

            while (retry < tries)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, asset.url);

                    using var response = await PrepareDownloadAsync(request);
                    response.EnsureSuccessStatusCode();

                    await using var ms = await response.Content.ReadAsStreamAsync();
                    await using var fs = new FileStream(temp_path, FileMode.Create, FileAccess.Write, FileShare.None);

                    var buffer = new byte[32768];

                    int total_bytes_read = 0;
                    int bytes_read;

                    Logger.Info("File download started: {0}", asset.name);

                    while ((bytes_read = await ms.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fs.WriteAsync(buffer.AsMemory(0, bytes_read));

                        total_bytes_read += bytes_read;

                        progress?.Report((asset.name, Convert.ToUInt64(total_bytes_read)));
                    }

                    progress?.Report((asset.name, Convert.ToUInt64(total_bytes_read)));

                    break;
                }
                catch (Exception ex)
                {
                    retry++;

                    Logger.Error($"Error file during download. Exception: {ex.Message}, Name: {asset.name}, Attempt {retry}.");
                }
            }

            var bat_name = Guid.NewGuid().ToString() + ".bat";

            using (var SW = new StreamWriter(bat_name))
            {
                SW.WriteLine(
                    $"ren \"{cur_path}\" \"{temp_name}\"  \r\n " + // rename current executable to old
                    $"move \"{temp_path}\" \"{cur_path}\" \r\n " + // move new file to needed path with the name of the current executable
                    $"start \"\" \"{cur_path}\" \r\n " + // start new executable
                    $"del \"{cur_path + "_old"}\" \r\n " + // delete old file
                    $"del \"{bat_name}\"" // delete bat file
                    );
                SW.Flush();
                SW.Close();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Process.Start(new ProcessStartInfo() { UseShellExecute = false, FileName = bat_name, CreateNoWindow = true });
            Environment.Exit(0);
        }

        public async Task<FileStates> Validate()
        {
            Trace.Assert(IsInitialized);

            List<IndexData> no_json_data = [.. data.FindAll(x => x.game_file)];

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

                if (!ValidateDate(d, stream) || !ValidateSize(d, stream))
                {
                    not_validated_data.Add(d);
                    return;
                }

                if (stream.Length > StaticGlobals.Variables.LargeChunkSize)
                {
                    Logger.Info("Calculating hashes for chunked file [{0}]", d.name);

                    if (!ValidateChunked(d, stream))
                        not_validated_data.Add(d);

                    return;
                }

                Logger.Info("Calculating hash for file [{0}]", d.name);
                if (d.hash != await FileHasher.XxHashFromFile(stream))
                    not_validated_data.Add(d);

            });
            return no_json_data.Count == not_validated_data.Count ? FileStates.NoFiles : not_validated_data.Count != 0 ? FileStates.NeedUpdate : FileStates.Every;
        }

        public async Task<bool> DownloadInvalid(IProgress<(string name, ulong Total)>? progress)
        {
            static async Task EnsureHash(IndexData d, string path)
            {
                FileStream stream = File.OpenRead(path);
                bool result = true;
                if (stream.Length > StaticGlobals.Variables.LargeChunkSize)
                {
                    long length = stream.Length;
                    uint chunk_count = 1;
                    while (length > StaticGlobals.Variables.LargeChunkSize)
                    {
                        chunk_count++;
                        length -= StaticGlobals.Variables.LargeChunkSize;
                    }
                    Logger.Info("Calculating hashes for chunked file [{0}]", d.name);
                    List<string> hash_list = await FileHasher.XxHashFromChunkedFile(stream, chunk_count);
                    result = hash_list.SequenceEqual(d.hash_list);
                }
                else
                {
                    Logger.Info("Calculating hash for file [{0}]", d.name);
                    string hash = await FileHasher.XxHashFromFile(path);
                    result = d.hash == hash;
                }

                stream.Close();

                if (!result)
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                    Logger.Exception(new Exception($"Hash of downloaded file doesn't match with cached hash: {d.name}"));
                }
            }

            var cts = new CancellationTokenSource();

            int threads = (int)Other.Settings.Storage.GetValue("max_download_threads");
            var semaphore = new SemaphoreSlim(threads);

            Logger.Info("Initialize {0} download threads...", threads.ToString());

            List<IndexData> data = [.. not_validated_data];
            not_validated_data.Clear();

            ChunkMetadataManager manager = ChunkMetadataManager.GetInstance();

            if (progress is not null && !manager.Files.IsEmpty)
            {
                foreach (var file in manager.Files)
                {
                    IndexData? d = data.Find(x => x.name == file.Key);
                    bool exists_not_validated = d is not null && File.Exists(Path.Combine(StaticGlobals.Locations.Start, d.directory ?? "", d.name));
                    foreach (var chunk in file.Value.Chunks)
                    {
                        if (exists_not_validated)
                            progress.Report((chunk.Key, Convert.ToUInt64(chunk.Value.Length)));
                        else
                            chunk.Value.Length = 0;
                    }
                }
            }

            var tasks = data.Select(async (d) =>
            {
                bool success = false;

                string directory = Path.Combine(StaticGlobals.Locations.Start, d.directory ?? "");

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string path = Path.Combine(directory, d.name);

                List<GithubAsset> assets = [.. release.assets.FindAll(x => x.name.StartsWith(d.name + ".part_", StringComparison.InvariantCultureIgnoreCase)).OrderBy(a => a.name)];

                if (assets.Count != 0)
                {
                    success = await DownloadFile(d, assets, directory, path, progress, cts.Token, semaphore);
                }
                else
                {
                    GithubAsset asset = release.assets.Find(x => x.name == d.name);

                    success = await DownloadFile(asset, path, progress, semaphore, cts.Token);
                }

                if (success)
                    await EnsureHash(d, path);
                else
                    not_validated_data.Add(d);

                return success;
            });

            var results = await Task.WhenAll(tasks);

            foreach (bool result in results)
                if (!result)
                    return false;

            ChunkMetadataManager.DestroyInstance();

            return true;
        }

        private static async Task<RateLimitInfo> GetRateLimitsAsync(HttpClient client)
        {
            HttpResponseMessage msg = await client.GetAsync(StaticGlobals.Links.GithubRateLimit);
            msg.EnsureSuccessStatusCode();

            return new()
            {
                Remaining = msg.Headers.TryGetValues("x-ratelimit-remaining", out var limitsEnum) && long.TryParse(limitsEnum.First(), out var left) ? left : 0,
                Reset = msg.Headers.TryGetValues("x-ratelimit-reset", out var resetEnum) && DateTimeOffset.TryParse(resetEnum.First(), out var dt) ? dt.UtcDateTime.ToLocalTime() : DateTime.Now,
            };
        }

        private async Task<HttpResponseMessage> PrepareDownloadAsync(HttpRequestMessage msg)
        {
            if (RateInfoUnauthorized is null || DateTime.UtcNow > RateInfoUnauthorized.Reset)
                RateInfoUnauthorized = await GetRateLimitsAsync(download_client_unauthorized);

            if (IsPublic && RateInfoUnauthorized.Remaining != 0)
            {
                HttpResponseMessage response = await download_client_unauthorized.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead);
                RateInfoUnauthorized.Remaining--;
                return response;
            }
            else
            {
                HttpResponseMessage response = await download_client.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead);

                if (!response.IsSuccessStatusCode && (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.TooManyRequests))
                {
                    if (response.Headers.TryGetValues("x-ratelimit-reset", out var resetEnum) && DateTimeOffset.TryParse(resetEnum.First(), out var dtf))
                    {
                        bool use_unauthorized = dtf.UtcDateTime > RateInfoUnauthorized.Reset && IsPublic;
                        TimeSpan span = (use_unauthorized ? RateInfoUnauthorized.Reset : dtf.UtcDateTime) - DateTime.UtcNow + TimeSpan.FromSeconds(10);
                        if (span > TimeSpan.Zero)
                        {
                            Logger.Warn("Rate limit exceeded. Waiting for reset until: {0}", dtf.UtcDateTime.ToLocalTime().ToString());
                            IsRateLimited = true;
                            await Task.Delay(span);
                            IsRateLimited = false;
                            var client = use_unauthorized ? download_client_unauthorized : download_client;
                            response = await client.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead);
                            return response;
                        }
                    }
                }

                return response;
            }
        }

        private async Task<bool> AddRangeRequestAsync(HttpRequestMessage msg, string url, string name, long position)
        {
            using var head = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await download_client.SendAsync(head, HttpCompletionOption.ResponseHeadersRead);

            bool supports = response.Headers.AcceptRanges.Contains("bytes");
            long? length = response.Content.Headers.ContentLength;

            if (!supports)
            {
                Logger.Warn("Response header doesn't contain bytes value. File: {0}", name);
                return false;
            }

            if (!length.HasValue)
            {
                Logger.Warn("Content length value is empty. File: {0}", name);
                return false;
            }

            if (position >= length.Value || position < 0)
            {
                Logger.Warn("Invalid Range values. Position: [{0}], Content length: [{1}]. Skipping Range. File: {2}", position, length.Value, name);
                return false;
            }

            msg.Headers.Range = new RangeHeaderValue(position, null);
            return true;
        }

        public async Task<bool> DownloadFile(IndexData d, List<GithubAsset> assets, string directory, string output, IProgress<(string name, ulong Total)>? progress, CancellationToken ct, SemaphoreSlim semaphore)
        {
            long size = 0;
            assets.ForEach(a => size += Convert.ToInt64(a.size));

            string path = Path.Combine(directory, d.name);

            using ChunkMappedFileWriter writer = new(path, size, assets.First().createdAtTime, ct);

            writer.Start();

            using FileStream readStream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            ChunkMetadataManager manager = ChunkMetadataManager.GetInstance();

            int tries = (int)Other.Settings.Storage.GetValue("max_amount_of_tries");

            var tasks = assets.WithMaxConcurrency(semaphore, async (asset) =>
            {
                int chunk_index = assets.IndexOf(asset);
                ChunkMetadata metadata = manager.GetOrCreateChunk(d.name, asset.name);

                if (!writer.FileCreated && !writer.FileUpdated && metadata.Length == Convert.ToInt64(asset.size))
                {
                    string hash = await FileHasher.XxHashFromChunk(readStream, Convert.ToUInt32(chunk_index + 1));
                    if (d.hash_list[chunk_index] == hash)
                    {
                        Logger.Ok("File chunk is already downloaded and valid: {0}", asset.name);
                        return true;
                    }
                    else
                        metadata.Length = 0;
                }

                long start_position = chunk_index * StaticGlobals.Variables.LargeChunkSize;
                long position = start_position;

                int retry = 0;

                while (retry < tries)
                {
                    try
                    {
                        using var request = new HttpRequestMessage(HttpMethod.Get, asset.url);

                        if (retry >= 1 || (!writer.FileCreated && !writer.FileUpdated))
                        {
                            bool supports = await AddRangeRequestAsync(request, asset.url, asset.name, metadata.Length);
                            if (!supports)
                            {
                                Logger.Error("Range request not supported. Redownloading chunk: {0}", asset.name);
                                position = start_position;
                                metadata.Length = 0;
                            }
                            else
                            {
                                Logger.Info("Trying to download with range request: {0}", asset.name);
                                position = start_position + metadata.Length;
                            }
                        }
                        else
                        {
                            if (writer.FileUpdated)
                                Logger.Info("Chunked file was updated. Redownloading part: {0}", asset.name);
                            else
                                Logger.Info("New chunked file part download initialization: {0}", asset.name);

                            position = start_position;
                            metadata.Length = 0;
                        }

                        using var response = await PrepareDownloadAsync(request);
                        response.EnsureSuccessStatusCode();

                        await using var ms = await response.Content.ReadAsStreamAsync();

                        var buffer = new byte[32768];

                        int total_bytes_read = Convert.ToInt32(metadata.Length);
                        int bytes_read;

                        Logger.Info("File part download started: {0}", asset.name);

                        while ((bytes_read = await ms.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            if (ct.IsCancellationRequested || writer.IsDisposed)
                            {
                                writer.Dispose();
                                return false;
                            }

                            var read_memory = ArrayPool<byte>.Shared.Rent(bytes_read);
                            Buffer.BlockCopy(buffer, 0, read_memory, 0, bytes_read);

                            writer.Produce(read_memory, position, bytes_read);

                            total_bytes_read += bytes_read;
                            position += bytes_read;
                            metadata.Length += bytes_read;

                            progress?.Report((asset.name, Convert.ToUInt64(total_bytes_read)));
                        }

                        progress?.Report((asset.name, Convert.ToUInt64(total_bytes_read)));

                        Logger.Ok("File part downloaded successfully: {0}", asset.name);

                        return true;
                    }
                    catch (Exception ex)
                    {
                        retry++;

                        Logger.Error($"Error file during download. Exception: {ex.Message}, Name: {asset.name}, Attempt {retry}.");
                    }
                }

                return false;

            }).ToList();

            var results = await Task.WhenAll(tasks);

            foreach (bool result in results)
                if (!result)
                    return false;

            return true;
        }

        public async Task<bool> DownloadFile(GithubAsset asset, string path, IProgress<(string name, ulong Total)>? progress, SemaphoreSlim semaphore, CancellationToken ct)
        {
            int retry = 0;

            int tries = (int)Other.Settings.Storage.GetValue("max_amount_of_tries");

            while (retry < tries)
            {
                try
                {
                    await semaphore.WaitAsync(ct);

                    using var request = new HttpRequestMessage(HttpMethod.Get, asset.url);

                    long position = File.Exists(path) ? new FileInfo(path).Length : 0;

                    if (position > 0)
                    {
                        if (File.GetCreationTimeUtc(path) == asset.createdAtTime && File.GetLastWriteTimeUtc(path) == asset.createdAtTime)
                        {
                            bool supports = await AddRangeRequestAsync(request, asset.url, asset.name, position);
                            if (!supports)
                            {
                                Logger.Error("Range request not supported. Starting again: {0}", asset.name);
                                File.Delete(path);
                            }
                            else
                                Logger.Info("Trying to download with range request: {0}", asset.name);
                        }
                        else
                        {
                            Logger.Info("File was updated. Deleting it for redownload: {0}", asset.name);
                            position = 0;
                            File.Delete(path);
                        }
                    }
                    else
                    {
                        Logger.Info("New file download initialization: {0}", asset.name);
                    }

                    using var response = await PrepareDownloadAsync(request);
                    response.EnsureSuccessStatusCode();

                    await using var ms = await response.Content.ReadAsStreamAsync();
                    await using var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.None);

                    File.SetCreationTimeUtc(fs.SafeFileHandle, asset.createdAtTime);
                    File.SetLastWriteTimeUtc(fs.SafeFileHandle, asset.createdAtTime);

                    var buffer = new byte[32768];

                    int total_bytes_read = Convert.ToInt32(position);
                    int bytes_read;

                    Logger.Info("File download started: {0}", asset.name);

                    while ((bytes_read = await ms.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        if (ct.IsCancellationRequested)
                        {
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
                    semaphore.Release();
                }
            }

            return false;
        }

        public ulong Size()
        {
            ulong size = 0;

            foreach (IndexData d in not_validated_data)
                size += Size(d.name);

            return size;
        }

        public GithubDownloadManager(string Token, string Repo, bool isPublic) : base(Token, Repo, isPublic)
        {
            download_client_unauthorized = new(ConfigureClientHandler());
            download_client_unauthorized.DefaultRequestHeaders.Add("Accept", "application/octet-stream");
            download_client_unauthorized.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("product", "1"));
            download_client_unauthorized.DefaultRequestHeaders.ExpectContinue = false;
            download_client_unauthorized.Timeout = TimeSpan.FromHours(1);

            download_client = new(ConfigureClientHandler());
            download_client.DefaultRequestHeaders.Add("Accept", "application/octet-stream");
            download_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            download_client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("product", "1"));
            download_client.DefaultRequestHeaders.ExpectContinue = false;
            download_client.Timeout = TimeSpan.FromHours(1);
        }
    }
}
