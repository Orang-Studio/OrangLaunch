using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrangLauncher.Managers
{
    // The API sends numeric ids and relative icon paths, e.g.
    // {"id":1,"icon_url":"/modpacks/1/icon","owner_username":"adasjusk",...}
    public class OrangLibModpack
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("description")] public string Description { get; set; } = "";
        [JsonPropertyName("owner_username")] public string Author { get; set; } = "";
        [JsonPropertyName("downloads")] public long Downloads { get; set; }
        [JsonPropertyName("game_version")] public string? GameVersion { get; set; }
        [JsonPropertyName("icon_url")] public string? IconUrl { get; set; }
        /// <summary>icon_url is relative to the API host.</summary>
        public string? AbsoluteIconUrl =>
            string.IsNullOrEmpty(IconUrl) ? null :
            IconUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? IconUrl : OrangLibClient.BaseUrl + IconUrl;
    }

    public class OrangLibVersion
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("version_number")] public string VersionNumber { get; set; } = "";
        [JsonPropertyName("changelog")] public string? Changelog { get; set; }
        [JsonPropertyName("file_name")] public string FileName { get; set; } = "";
        [JsonPropertyName("file_size")] public long FileSize { get; set; }
        [JsonPropertyName("scan_verdict")] public string? ScanVerdict { get; set; }
    }

    /// <summary>
    /// Client for the OrangLib modpack repository (same API the Linux launcher uses).
    /// </summary>
    public static class OrangLibClient
    {
        public static string BaseUrl { get; } =
            Environment.GetEnvironmentVariable("ORANGLIB_API_URL") ?? "https://api.oranges.lt";

        private static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            c.DefaultRequestHeaders.Add("User-Agent", "Orang-Studio/OrangLaunch (https://github.com/Orang-Studio/OrangLaunch)");
            return c;
        }

        public static async Task<List<OrangLibModpack>> GetModpacksAsync(int page = 1, int pageSize = 100)
        {
            var json = await Http.GetStringAsync($"{BaseUrl}/modpacks?page={page}&page_size={pageSize}&sort_by=updated");
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("items", out var items)) return new List<OrangLibModpack>();
            return JsonSerializer.Deserialize<List<OrangLibModpack>>(items.GetRawText()) ?? new List<OrangLibModpack>();
        }

        public static async Task<List<OrangLibVersion>> GetVersionsAsync(string modpackId)
        {
            var json = await Http.GetStringAsync($"{BaseUrl}/modpacks/{modpackId}");
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("versions", out var versions)) return new List<OrangLibVersion>();
            return JsonSerializer.Deserialize<List<OrangLibVersion>>(versions.GetRawText()) ?? new List<OrangLibVersion>();
        }

        /// <summary>Downloads a modpack version file into destDir and returns the file path.</summary>
        public static async Task<string> DownloadVersionAsync(string modpackId, OrangLibVersion version, string destDir,
            IProgress<double>? progress = null)
        {
            Directory.CreateDirectory(destDir);
            var fileName = string.IsNullOrEmpty(version.FileName) ? $"modpack_{modpackId}_{version.Id}" : version.FileName;
            var destPath = Path.Combine(destDir, fileName);
            await ModrinthClient.DownloadFileAsync(
                $"{BaseUrl}/modpacks/{modpackId}/versions/{version.Id}/download", destPath, version.FileSize, progress);
            return destPath;
        }
    }
}
