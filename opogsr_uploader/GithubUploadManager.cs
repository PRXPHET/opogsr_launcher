using opogsr_launcher;
using opogsr_launcher.Extensions.StreamExtensions;
using opogsr_launcher.JsonContext;
using opogsr_launcher.Managers.Github;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace opogsr_uploader
{
    public class GithubUploadManager : GithubManager
    {
        private async Task<GithubAsset> UploadFile(string url, Stream stream, IProgress<(ulong Sent, ulong Total)>? progress)
        {
            var content = new HttpProgressStreamContent(stream, progress);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");

            var response = await api_client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            return JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(), SourceGenerationContext.Default.GithubAsset)!;
        }

        protected override async Task ReadConfig() => await ReadConfig(true);

        public async Task UploadFile(IndexData d, IProgress<(ulong Sent, ulong Total)>? progress = null)
        {
            Trace.Assert(IsInitialized);

            await DeleteFile(d.name);

            string directory = Path.Combine(Directory.GetCurrentDirectory(), d.directory ?? "");

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string path = Path.Combine(directory, d.name);

            string name = Path.GetFileName(path);
            using var fileStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            if (fileStream.Length > StaticGlobals.Variables.LargeChunkSize)
            {
                ChunkReadStream chunkStream = new(fileStream, StaticGlobals.Variables.LargeChunkSize);

                uint chunk = 1;

                do
                {
                    string part_name = name + ".part_" + chunk.ToString("D3");

                    GithubAsset asset = await UploadFile(release.upload_url + "?name=" + part_name, chunkStream, progress);

                    if (chunk == 1)
                    {
                        File.SetCreationTimeUtc(fileStream.SafeFileHandle, asset.createdAtTime);
                        File.SetLastWriteTimeUtc(fileStream.SafeFileHandle, asset.createdAtTime);
                    }

                    chunk++;
                    Console.WriteLine("File part uploaded: " + part_name);
                }
                while (chunkStream.ReadNext());
            }
            else
            {
                GithubAsset asset = await UploadFile(release.upload_url + "?name=" + name, fileStream, progress);
                File.SetCreationTimeUtc(fileStream.SafeFileHandle, asset.createdAtTime);
                File.SetLastWriteTimeUtc(fileStream.SafeFileHandle, asset.createdAtTime);
            }
        }

        public async Task UpdateConfig(string path)
        {
            await DeleteFile("index.json");

            var content = new StringContent(File.ReadAllText(path));
            content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

            var response = await api_client.PostAsync(release.upload_url + "?name=index.json", content);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteFile(string name)
        {
            List<GithubAsset> assets = [.. release.assets.FindAll(x => x.name.StartsWith(name + ".part_", StringComparison.InvariantCultureIgnoreCase))];
            if (assets.Count != 0)
            {
                assets.ForEach(async a =>
                {
                    var response = await api_client.DeleteAsync(a.url);
                    response.EnsureSuccessStatusCode();
                    Console.WriteLine("Deleted file part: " + a.name);
                    release.assets.Remove(a);
                });
            }
            else
            {
                GithubAsset? asset = release.assets.Find(x => x.name == name);
                if (asset != null)
                {
                    var response = await api_client.DeleteAsync(asset.url);
                    response.EnsureSuccessStatusCode();
                    release.assets.Remove(asset);
                }
            }
        }

        protected override bool ServerCertificateCallback(HttpRequestMessage sender, X509Certificate2? cert, X509Chain? chain, SslPolicyErrors policy)
        {
            return policy == System.Net.Security.SslPolicyErrors.None;
        }

        public GithubUploadManager(string Token, string Repo) : base(Token, Repo, false)
        {
            api_client.Timeout = TimeSpan.FromHours(6);
        }
    }
}
