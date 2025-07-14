using opogsr_launcher;
using opogsr_launcher.Managers;
using opogsr_launcher.Other.StreamExtensions;
using System.Net.Http.Headers;

namespace opogsr_uploader
{
    public class GithubUploadManager : GithubManager
    {
        private async Task UploadFile(string url, Stream stream, IProgress<(ulong Sent, ulong Total)>? progress)
        {
            var content = new HttpProgressStreamContent(stream, progress);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");

            var response = await api_client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
        }

        public async Task UploadFile(IndexData d, IProgress<(ulong Sent, ulong Total)>? progress = null)
        {
            await RepoTask();

            string directory = Path.Combine(Directory.GetCurrentDirectory(), d.directory ?? "");

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string path = Path.Combine(directory, d.name);

            string name = Path.GetFileName(path);
            using var fileStream = File.OpenRead(path);

            if (fileStream.Length > StaticGlobals.Variables.LargeChunkSize)
            {
                ChunkReadStream chunkStream = new(fileStream, StaticGlobals.Variables.LargeChunkSize);

                uint chunk = 1;

                do
                {
                    string part_name = name + ".part_" + chunk.ToString("D3");

                    await UploadFile(release.upload_url + "?name=" + part_name, chunkStream, progress);
                    chunk++;
                    Console.WriteLine("File part uploaded: " + part_name);
                }
                while (chunkStream.ReadNext());
            }
            else
            {
                await UploadFile(release.upload_url + "?name=" + name, fileStream, progress);
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
                });
            }
            else
            {
                GithubAsset? asset = release.assets.Find(x => x.name == name);
                if (asset != null)
                {
                    var response = await api_client.DeleteAsync(asset.url);
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        public GithubUploadManager(string Token, string Repo) : base(Token, Repo)
        {
            api_client.Timeout = TimeSpan.FromHours(1);
        }
    }
}
