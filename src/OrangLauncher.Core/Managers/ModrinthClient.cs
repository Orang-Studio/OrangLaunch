using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrangLauncher.Managers
{
    public class ModrinthProject
    {
        [JsonPropertyName("project_id")] public string ProjectId { get; set; } = "";
        [JsonPropertyName("slug")] public string Slug { get; set; } = "";
        [JsonPropertyName("title")] public string Title { get; set; } = "";
        [JsonPropertyName("description")] public string Description { get; set; } = "";
        [JsonPropertyName("icon_url")] public string? IconUrl { get; set; }
        [JsonPropertyName("author")] public string Author { get; set; } = "";
        [JsonPropertyName("downloads")] public long Downloads { get; set; }
        [JsonPropertyName("follows")] public long Follows { get; set; }
        [JsonPropertyName("categories")] public List<string> Categories { get; set; } = new();
        [JsonPropertyName("project_type")] public string ProjectType { get; set; } = "";
        [JsonPropertyName("date_modified")] public DateTime DateModified { get; set; }
    }

    public class ModrinthSearchResult
    {
        [JsonPropertyName("hits")] public List<ModrinthProject> Hits { get; set; } = new();
        [JsonPropertyName("total_hits")] public long TotalHits { get; set; }
    }

    public class ModrinthVersionFile
    {
        [JsonPropertyName("url")] public string Url { get; set; } = "";
        [JsonPropertyName("filename")] public string Filename { get; set; } = "";
        [JsonPropertyName("primary")] public bool Primary { get; set; }
        [JsonPropertyName("size")] public long Size { get; set; }
    }

    public class ModrinthVersion
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("version_number")] public string VersionNumber { get; set; } = "";
        [JsonPropertyName("game_versions")] public List<string> GameVersions { get; set; } = new();
        [JsonPropertyName("loaders")] public List<string> Loaders { get; set; } = new();
        [JsonPropertyName("files")] public List<ModrinthVersionFile> Files { get; set; } = new();
        [JsonPropertyName("date_published")] public DateTime DatePublished { get; set; }
        // "release" "beta" "alpha"
        [JsonPropertyName("version_type")] public string VersionType { get; set; } = "release";
    }

    public static class ModrinthClient
    {
        private const string BaseUrl = "https://api.modrinth.com/v2";
        private static readonly HttpClient Http = CreateClient();
        private static HttpClient CreateClient()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            c.DefaultRequestHeaders.Add("User-Agent", "Orang-Studio/OrangLaunch (https://github.com/Orang-Studio/OrangLaunch)");
            return c;
        }
        public static async Task<ModrinthSearchResult> SearchAsync(
            string query, string projectType, string? mcVersion = null, string? loader = null,
            string sort = "relevance", int offset = 0, int limit = 20)
        {
            var facets = new List<List<string>> { new() { $"project_type:{projectType}" } };
            if (!string.IsNullOrEmpty(mcVersion)) facets.Add(new List<string> { $"versions:{mcVersion}" });
            if (!string.IsNullOrEmpty(loader))
                facets.Add(AcceptedLoaders(loader).Select(l => $"categories:{l}").ToList());
            var facetsJson = Uri.EscapeDataString(JsonSerializer.Serialize(facets));
            var url = $"{BaseUrl}/search?query={Uri.EscapeDataString(query ?? "")}&facets={facetsJson}&index={sort}&offset={offset}&limit={limit}";
            var json = await Http.GetStringAsync(url);
            return JsonSerializer.Deserialize<ModrinthSearchResult>(json) ?? new ModrinthSearchResult();
        }

        public static async Task<List<ModrinthVersion>> GetVersionsAsync(string idOrSlug, string? mcVersion = null, string? loader = null)
        {
            var url = $"{BaseUrl}/project/{idOrSlug}/version";
            var q = new List<string>();
            if (!string.IsNullOrEmpty(mcVersion)) q.Add($"game_versions={Uri.EscapeDataString($"[\"{mcVersion}\"]")}");
            if (!string.IsNullOrEmpty(loader))
            {
                var list = string.Join(",", AcceptedLoaders(loader).Select(l => $"\"{l}\""));
                q.Add($"loaders={Uri.EscapeDataString($"[{list}]")}");
            }
            if (q.Count > 0) url += "?" + string.Join("&", q);
            var json = await Http.GetStringAsync(url);
            var versions = JsonSerializer.Deserialize<List<ModrinthVersion>>(json) ?? new List<ModrinthVersion>();
            return versions.Where(v => IsCompatible(v, mcVersion, loader)).ToList();
        }

        public static string[] AcceptedLoaders(string loader)
            => string.Equals(loader, "quilt", StringComparison.OrdinalIgnoreCase)
                ? new[] { "quilt", "fabric" }
                : new[] { loader };
        public static bool IsCompatible(ModrinthVersion version, string? mcVersion, string? loader)
        {
            if (!string.IsNullOrEmpty(mcVersion) && version.GameVersions.Count > 0 &&
                !version.GameVersions.Any(g => string.Equals(g, mcVersion, StringComparison.OrdinalIgnoreCase)))
                return false;
            if (string.IsNullOrEmpty(loader) || version.Loaders.Count == 0) return true;
            return version.Loaders.Any(l => AcceptedLoaders(loader!).Contains(l, StringComparer.OrdinalIgnoreCase));
        }

        public static ModrinthVersion? PickBestVersion(IEnumerable<ModrinthVersion> versions)
            => versions
                .OrderBy(v => (v.VersionType ?? "release").ToLowerInvariant() switch
                {
                    "release" => 0,
                    "beta" => 1,
                    "alpha" => 2,
                    _ => 3,
                })
                .ThenByDescending(v => v.DatePublished)
                .FirstOrDefault();

        public static async Task<string?> InstallAsync(string idOrSlug, string destDir,
            string? mcVersion = null, string? loader = null, IProgress<double>? progress = null)
        {
            // no fallback to unfiltered versions here installing file for the wrong loader is worse than reporting that nothing compatible exists
            var versions = await GetVersionsAsync(idOrSlug, mcVersion, loader);
            var version = PickBestVersion(versions);
            var file = version?.Files.FirstOrDefault(f => f.Primary) ?? version?.Files.FirstOrDefault();
            if (file == null) return null;
            Directory.CreateDirectory(destDir);
            var destPath = Path.Combine(destDir, file.Filename);
            await DownloadFileAsync(file.Url, destPath, file.Size, progress);
            return destPath;
        }

        public static async Task DownloadFileAsync(string url, string destPath, long expectedSize = 0, IProgress<double>? progress = null)
        {
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? expectedSize;
            await using var src = await response.Content.ReadAsStreamAsync();
            await using var dst = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            var buffer = new byte[81920];
            long readTotal = 0;
            int read;
            while ((read = await src.ReadAsync(buffer)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, read));
                readTotal += read;
                if (total > 0) progress?.Report((double)readTotal / total);
            }
        }

        public static async Task<byte[]?> GetIconAsync(string? iconUrl)
        {
            if (string.IsNullOrEmpty(iconUrl)) return null;
            try { return await Http.GetByteArrayAsync(iconUrl); }
            catch { return null; }
        }
    }
}
