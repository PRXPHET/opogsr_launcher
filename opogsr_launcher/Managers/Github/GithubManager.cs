using Avalonia.Controls;
using opogsr_launcher.JsonContext;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Authentication;

namespace opogsr_launcher.Managers.Github
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
        [JsonPropertyName("created_at")]
        public DateTime createdAtTime { get; set; }
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
        public string? hash { get; set; }
        [JsonPropertyName("hash_list")]
        public List<string>? hash_list { get; set; }
        [JsonPropertyName("directory")]
        public string? directory { get; set; }
    }

    public class GithubManager
    {
        protected HttpClient api_client_unauthorized = new(ConfigureClientHandler());
        protected HttpClient api_client = new(ConfigureClientHandler());

        protected GithubRelease release = new();

        protected Task ReadRepoTask;

        public bool IsPublic { get; private set; } = false;

        public List<IndexData> data { get; private set; } = new();

        private async Task<HttpClient> GetClient()
        {
            if (!IsPublic)
                return api_client;

            HttpResponseMessage msg = await api_client_unauthorized.GetAsync(StaticGlobals.Links.GithubRateLimit);

            msg.EnsureSuccessStatusCode();

            if (msg.Headers.TryGetValues("x-ratelimit-remaining", out var limits) && long.TryParse(limits.First(), out long left) && left != 0)
                return api_client_unauthorized;
            else
                return api_client;
        }

        protected static HttpClientHandler ConfigureClientHandler() => new()
        {
            // Enable TLS 1.3 if we are on Windows 11 as it's supported on it
            // Enabling it without checking the OS version is not a safe system configuration
            SslProtocols = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) ? (SslProtocols.Tls13 | SslProtocols.Tls12) : SslProtocols.Tls12
        };

        protected async Task ReadConfig(bool no_assert)
        {
            GithubAsset asset = release.assets.Find(x => x.name == "index.json");

            if (asset is null)
            {
                if (!no_assert)
                    Logger.Exception(new Exception("Can't find index.json file in release."));
                return;
            }

            HttpRequestMessage msg = new();
            msg.Headers.Add("Accept", "application/octet-stream");
            msg.RequestUri = new Uri(asset.url);

            var client = await GetClient();
            var httpResult = await client.SendAsync(msg);
            httpResult.EnsureSuccessStatusCode();
            string contents = await httpResult.Content.ReadAsStringAsync();
            data = JsonSerializer.Deserialize(contents, SourceGenerationContext.Default.ListIndexData);
        }

        protected virtual async Task ReadConfig() => await ReadConfig(false);

        private async Task ReadRelease(string repo)
        {
            var client = await GetClient();
            var httpResult = await client.GetAsync($"https://api.github.com/repos/{repo}/releases/tags/base");
            httpResult.EnsureSuccessStatusCode();
            string contents = await httpResult.Content.ReadAsStringAsync();
            release = JsonSerializer.Deserialize(contents, SourceGenerationContext.Default.GithubRelease);
            release.upload_url = release.upload_url.Replace("{?name,label}", "");
        }

        public async Task WaitAsync() => await ReadRepoTask.WaitAsync(CancellationToken.None);

        public async Task<ulong> Size(string name, bool no_assert = false)
        {
            await WaitAsync();

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

        public GithubManager(string Token, string Repo, bool isPublic)
        {
            IsPublic = isPublic;

            api_client_unauthorized.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("product", "1"));
            api_client_unauthorized.Timeout = TimeSpan.FromSeconds(15);

            api_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            api_client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("product", "1"));
            api_client.Timeout = TimeSpan.FromSeconds(15);

            ReadRepoTask = Task.Run(async () =>
            {
                if (Design.IsDesignMode)
                    return;
                await ReadRelease(Repo);
                await ReadConfig();
            });
        }
    }
}
