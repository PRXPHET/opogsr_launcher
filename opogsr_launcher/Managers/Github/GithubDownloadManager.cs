using opogsr_launcher.Extensions;
using opogsr_launcher.Extensions.StreamExtensions;
using opogsr_launcher.Hasher;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace opogsr_launcher.Managers.Github
{
    internal class GithubDownloadManager : GithubManager
    {
        private ConcurrentBag<IndexData> not_validated_data = new();
        protected HttpClient download_client = new();

        private bool ValidateChunked(IndexData d, FileStream stream)
        {
            List<GithubAsset> assets = [.. release.assets.FindAll(x => x.name.StartsWith(d.name + ".part_", StringComparison.InvariantCultureIgnoreCase)).OrderBy(a => a.name)];
            List<string> hash_list = FileHasher.XxHashFromChunkedFile(stream, Convert.ToUInt32(assets.Count)).GetAwaiter().GetResult();
            if (!hash_list.SequenceEqual(d.hash_list))
                return false;

            return true;
        }

        private bool ValidateSize(IndexData d, Stream stream)
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

                if (!ValidateSize(d, stream))
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

            ChunkMetadataManager manager = ChunkMetadataManager.GetInstance();

            if (progress is not null && !manager.Files.IsEmpty)
            {
                foreach (var file in manager.Files)
                {
                    foreach (var chunk in file.Value.Chunks)
                    {
                        progress.Report((chunk.Key, Convert.ToUInt64(chunk.Value.Length)));
                    }
                }
            }

            var cts = new CancellationTokenSource();

            var semaphore = new SemaphoreSlim(4);

            List<IndexData> data = [.. not_validated_data];
            not_validated_data.Clear();

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

        private static void EnsureFile(string path)
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
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

            ChunkMetadataManager manager = ChunkMetadataManager.GetInstance();

            var tasks = assets.WithMaxConcurrency(semaphore, async (asset) =>
            {
                int chunk_index = assets.IndexOf(asset);
                ChunkMetadata metadata = manager.GetOrCreateChunk(d.name, asset.name);

                if (!writer.FileCreated && !writer.FileUpdated && metadata.Length == Convert.ToInt64(asset.size))
                {
                    string hash = await FileHasher.XxHashFromChunk(writer.GetStream(), Convert.ToUInt32(chunk_index));
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

                while (retry < StaticGlobals.Variables.AmountOfTries)
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

                        using var response = await download_client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                        response.EnsureSuccessStatusCode();

                        await using var ms = await response.Content.ReadAsStreamAsync();

                        var buffer = new byte[32768];

                        int total_bytes_read = Convert.ToInt32(metadata.Length);
                        int bytes_read;

                        Logger.Info("File part download started: {0}", asset.name);

                        while ((bytes_read = await ms.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            if (ct.IsCancellationRequested || writer.IsDestroyed)
                            {
                                writer.Destroy();
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
                    finally
                    {
                        if (retry >= StaticGlobals.Variables.AmountOfTries)
                        {
                            EnsureFile(path);
                        }
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

            while (retry < StaticGlobals.Variables.AmountOfTries)
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

                    using var response = await download_client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
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
                    if (retry >= StaticGlobals.Variables.AmountOfTries)
                    {
                        EnsureFile(path);
                    }
                    semaphore.Release();
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

        public GithubDownloadManager(string Token, string Repo) : base(Token, Repo)
        {
            download_client.DefaultRequestHeaders.Add("Accept", "application/octet-stream");
            download_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            download_client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("product", "1"));
            download_client.DefaultRequestHeaders.ExpectContinue = false;
            download_client.Timeout = TimeSpan.FromHours(1);
        }
    }
}
