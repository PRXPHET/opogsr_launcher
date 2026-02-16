using opogsr_common.Log;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace opogsr_common.Managers
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
        public required string name { get; set; }
        [JsonPropertyName("size")]
        public required ulong size { get; set; }
        [JsonPropertyName("url")]
        public required string url { get; set; }
        [JsonPropertyName("created_at")]
        public required DateTime createdAtTime { get; set; }
    }

    public class GithubRelease
    {
        [JsonPropertyName("assets")] 
        public required List<GithubAsset> assets { get; set; }

        [JsonPropertyName("upload_url")] 
        public required string upload_url { get; set; }
    }

    public class IndexData
    {
        [JsonPropertyName("name")]
        public required string name { get; set; }
        [JsonPropertyName("directory")]
        public string? directory { get; set; }
        [JsonPropertyName("game_file")]
        public bool game_file { get; set; } = true;
        [JsonPropertyName("hash")]
        public string? hash { get; set; }
        [JsonPropertyName("hash_list")]
        public List<string>? hash_list { get; set; }
    }

    public class GithubManager
    {
        protected GithubRelease release = new()
        {
            assets = [],
            upload_url = ""
        };

        protected HttpClient api_client_unauthorized;
        protected HttpClient api_client;

        public string RepoName { get; private set; }

        public bool IsPublic { get; private set; }

        public bool IsInitialized { get; protected set; } = false;

        public List<IndexData> data { get; private set; } = [];

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

        protected HttpClientHandler ConfigureClientHandler() => new()
        {
            // Enable TLS 1.3 if we are on Windows 11 as it's supported on it
            // Enabling it without checking the OS version is not a safe system configuration
            SslProtocols = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) ? (SslProtocols.Tls13 | SslProtocols.Tls12) : SslProtocols.Tls12,
            ServerCertificateCustomValidationCallback = ServerCertificateCallback
        };

        protected virtual bool ServerCertificateCallback(HttpRequestMessage sender, X509Certificate2? cert, X509Chain? chain, SslPolicyErrors policy)
        {
            return policy == SslPolicyErrors.None;
        }

        protected async Task ReadConfig(bool no_assert)
        {
            GithubAsset? asset = release.assets.Find(x => x.name == "index.json");

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
            data = JsonSerializer.Deserialize(contents, SourceGenerationContext.Default.ListIndexData)!;
        }

        protected virtual async Task ReadConfig() => await ReadConfig(false);

        protected async Task ReadRelease()
        {
            var client = await GetClient();
            var httpResult = await client.GetAsync($"https://api.github.com/repos/{RepoName}/releases/tags/base");
            httpResult.EnsureSuccessStatusCode();
            string contents = await httpResult.Content.ReadAsStringAsync();
            release = JsonSerializer.Deserialize(contents, SourceGenerationContext.Default.GithubRelease)!;
            release.upload_url = release.upload_url.Replace("{?name,label}", "");
        }

        public ulong Size(string name, bool no_assert = false)
        {
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

                    Logger.Exception(new Exception($"Github asset is null. Name: {name}"));
                }

                size += a!.size;
            }

            return size;
        }

        public virtual async Task<bool> TryInitialize()
        {
            if (IsInitialized)
                return true;

            try
            {
                await ReadRelease();
                await ReadConfig();

                IsInitialized = true;

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Github attemption connect failed.");
                Logger.Error("Exception message: {0}", ex.Message);

                if (ex.InnerException != null)
                    Logger.Error("Inner exception message: {0}", ex.InnerException.Message);

                return false;
            }
        }

        public GithubManager(string Token, string Repo, bool isPublic)
        {
            RepoName = Repo;

            IsPublic = isPublic;

            api_client_unauthorized = new(ConfigureClientHandler());
            api_client_unauthorized.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("product", "1"));
            api_client_unauthorized.Timeout = TimeSpan.FromSeconds(15);

            api_client = new(ConfigureClientHandler());
            api_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            api_client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("product", "1"));
            api_client.Timeout = TimeSpan.FromSeconds(15);
        }
    }
}
