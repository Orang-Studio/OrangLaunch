using System.Text.Json;
namespace OrangLauncher.Managers
{
    public static class MclogsClient
    {
        private const string ApiUrl = "https://api.mclo.gs/1/log";
        private static readonly HttpClient Http = CreateClient();
        private static HttpClient CreateClient()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            c.DefaultRequestHeaders.Add("User-Agent", "Orang-Studio/OrangLaunch (https://github.com/Orang-Studio/OrangLaunch)");
            return c;
        }
        public static async Task<(bool Success, string UrlOrError)> UploadAsync(string logContent)
        {
            if (string.IsNullOrWhiteSpace(logContent))
                return (false, "Log is empty.");
            try
            {
                // mclo.gs limits uploads to 10 MB / 25k lines
                const int maxChars = 10_000_000;
                if (logContent.Length > maxChars)
                    logContent = logContent[^maxChars..];
                using var form = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("content", logContent)
                });
                var response = await Http.PostAsync(ApiUrl, form);
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("success", out var ok) && ok.GetBoolean() &&
                    root.TryGetProperty("url", out var url))
                    return (true, url.GetString() ?? "");
                var error = root.TryGetProperty("error", out var err) ? err.GetString() : null;
                return (false, error ?? "Upload failed.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
}   }   }   }