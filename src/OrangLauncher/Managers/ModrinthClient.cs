using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrangLauncher.Managers
{
    public class ModrinthProject
    {
        [JsonPropertyName("project_id")] public string ProjectId { get; set; } = "";
        [JsonPropertyName("id")] public string Id { get; set; } = "";
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
        public string Key => !string.IsNullOrEmpty(Slug) ? Slug : !string.IsNullOrEmpty(ProjectId) ? ProjectId : Id;
    }

    public class ModrinthSearchResult
    {
        [JsonPropertyName("hits")] public List<ModrinthProject> Hits { get; set; } = new();
        [JsonPropertyName("total_hits")] public long TotalHits { get; set; }
    }

    public class ModrinthHashes
    {
        [JsonPropertyName("sha1")] public string? Sha1 { get; set; }
        [JsonPropertyName("sha512")] public string? Sha512 { get; set; }
    }

    public class ModrinthVersionFile
    {
        [JsonPropertyName("url")] public string Url { get; set; } = "";
        [JsonPropertyName("filename")] public string Filename { get; set; } = "";
        [JsonPropertyName("primary")] public bool Primary { get; set; }
        [JsonPropertyName("size")] public long Size { get; set; }
        [JsonPropertyName("hashes")] public ModrinthHashes Hashes { get; set; } = new();
    }

    public class ModrinthVersion
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("project_id")] public string ProjectId { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("version_number")] public string VersionNumber { get; set; } = "";
        [JsonPropertyName("game_versions")] public List<string> GameVersions { get; set; } = new();
        [JsonPropertyName("loaders")] public List<string> Loaders { get; set; } = new();
        [JsonPropertyName("files")] public List<ModrinthVersionFile> Files { get; set; } = new();
        [JsonPropertyName("date_published")] public DateTime DatePublished { get; set; }
        [JsonPropertyName("version_type")] public string VersionType { get; set; } = "release";
        public ModrinthVersionFile? PrimaryFile => Files.FirstOrDefault(f => f.Primary) ?? Files.FirstOrDefault();
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
            string sort = "relevance", int offset = 0, int limit = 20, string? category = null)
        {
            var facets = new List<List<string>> { new() { $"project_type:{projectType}" } };
            if (!string.IsNullOrEmpty(mcVersion)) facets.Add(new List<string> { $"versions:{mcVersion}" });
            if (!string.IsNullOrEmpty(loader))
                facets.Add(AcceptedLoaders(loader).Select(l => $"categories:{l}").ToList());
            if (!string.IsNullOrEmpty(category)) facets.Add(new List<string> { $"categories:{category}" });
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

        public static async Task<ModrinthVersion?> GetVersionAsync(string versionId)
        {
            var json = await Http.GetStringAsync($"{BaseUrl}/version/{versionId}");
            return JsonSerializer.Deserialize<ModrinthVersion>(json);
        }

        public static async Task<ModrinthProject?> GetProjectAsync(string idOrSlug)
        {
            var json = await Http.GetStringAsync($"{BaseUrl}/project/{idOrSlug}");
            return JsonSerializer.Deserialize<ModrinthProject>(json);
        }

        public static async Task<Dictionary<string, ModrinthProject>> GetProjectsAsync(IEnumerable<string> ids)
        {
            var result = new Dictionary<string, ModrinthProject>();
            var list = ids.Distinct().ToList();
            for (int i = 0; i < list.Count; i += 100)
            {
                var chunk = list.Skip(i).Take(100).ToList();
                try
                {
                    var json = await Http.GetStringAsync($"{BaseUrl}/projects?ids={Uri.EscapeDataString(JsonSerializer.Serialize(chunk))}");
                    foreach (var p in JsonSerializer.Deserialize<List<ModrinthProject>>(json) ?? new())
                        result[p.Id] = p;
                }
                catch { }
            }
            return result;
        }

        public static async Task<Dictionary<string, ModrinthVersion>> LookupHashesAsync(IEnumerable<string> hashes, string algorithm = "sha1")
        {
            var result = new Dictionary<string, ModrinthVersion>(StringComparer.OrdinalIgnoreCase);
            var list = hashes.ToList();
            for (int i = 0; i < list.Count; i += 400)
            {
                var body = JsonSerializer.Serialize(new { hashes = list.Skip(i).Take(400).ToList(), algorithm });
                try
                {
                    var response = await Http.PostAsync($"{BaseUrl}/version_files", new StringContent(body, Encoding.UTF8, "application/json"));
                    if (!response.IsSuccessStatusCode) continue;
                    var map = JsonSerializer.Deserialize<Dictionary<string, ModrinthVersion>>(await response.Content.ReadAsStringAsync());
                    if (map != null) foreach (var kv in map) result[kv.Key] = kv.Value;
                }
                catch { }
            }
            return result;
        }

        public static async Task<Dictionary<string, ModrinthVersion>> LatestForHashesAsync(IEnumerable<string> hashes, IEnumerable<string> loaders, IEnumerable<string> gameVersions, string algorithm = "sha1")
        {
            var result = new Dictionary<string, ModrinthVersion>(StringComparer.OrdinalIgnoreCase);
            var list = hashes.ToList();
            var loaderList = loaders.ToList();
            var gv = gameVersions.ToList();
            for (int i = 0; i < list.Count; i += 400)
            {
                var body = JsonSerializer.Serialize(new { hashes = list.Skip(i).Take(400).ToList(), algorithm, loaders = loaderList, game_versions = gv });
                try
                {
                    var response = await Http.PostAsync($"{BaseUrl}/version_files/update", new StringContent(body, Encoding.UTF8, "application/json"));
                    if (!response.IsSuccessStatusCode) continue;
                    var map = JsonSerializer.Deserialize<Dictionary<string, ModrinthVersion>>(await response.Content.ReadAsStringAsync());
                    if (map != null) foreach (var kv in map) result[kv.Key] = kv.Value;
                }
                catch { }
            }
            return result;
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
            var versions = await GetVersionsAsync(idOrSlug, mcVersion, loader);
            var version = PickBestVersion(versions);
            var file = version?.PrimaryFile;
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
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            var tmp = destPath + ".part";
            await using (var src = await response.Content.ReadAsStreamAsync())
            await using (var dst = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            {
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
            File.Move(tmp, destPath, true);
        }

        public static async Task<byte[]?> GetIconAsync(string? iconUrl)
        {
            if (string.IsNullOrEmpty(iconUrl)) return null;
            try { return await Http.GetByteArrayAsync(iconUrl); }
            catch { return null; }
        }

        public static string Sha1(string path)
        {
            using var s = File.OpenRead(path);
            return Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(s)).ToLowerInvariant();
        }

        public static string Sha512(string path)
        {
            using var s = File.OpenRead(path);
            return Convert.ToHexString(System.Security.Cryptography.SHA512.HashData(s)).ToLowerInvariant();
        }
    }
}