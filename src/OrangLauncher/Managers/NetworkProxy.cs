using System.Net;
namespace OrangLauncher.Managers
{
    public static class NetworkProxy
    {
        public static (bool Ok, string Message) Apply()
        {
            var url = LauncherSettings.ProxyUrl.Trim();
            if (string.IsNullOrEmpty(url))
            {
                HttpClient.DefaultProxy = WebRequest.GetSystemWebProxy();
                return (true, LocalizationManager.GetString("QT_PROXY_CLEARED", "Proxy cleared."));
            }
            try
            {
                HttpClient.DefaultProxy = Create(url);
                return (true, LocalizationManager.T("QT_PROXY_ACTIVE", "Launcher downloads go through {proxy}", ("proxy", Mask(url))));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
        private static WebProxy Create(string url)
        {
            if (!url.Contains("://")) url = "http://" + url;
            var uri = new Uri(url);
            var proxy = new WebProxy(new Uri($"{uri.Scheme.Replace("socks5h", "socks5")}://{uri.Host}:{uri.Port}"));
            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var parts = uri.UserInfo.Split(':', 2);
                proxy.Credentials = new NetworkCredential(Uri.UnescapeDataString(parts[0]), parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "");
            }
            return proxy;
        }
        private static string Mask(string url)
        {
            try
            {
                var uri = new Uri(url.Contains("://") ? url : "http://" + url);
                return $"{uri.Scheme}://{uri.Host}:{uri.Port}";
            }
            catch { return url; }
        }
        public static async Task<(bool Ok, string Message)> TestAsync(string url)
        {
            try
            {
                var handler = new HttpClientHandler();
                if (!string.IsNullOrWhiteSpace(url))
                {
                    handler.Proxy = Create(url.Trim());
                    handler.UseProxy = true;
                }
                using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(12) };
                var sw = System.Diagnostics.Stopwatch.StartNew();
                using var resp = await client.GetAsync("https://api.modrinth.com/v2/tag/loader");
                resp.EnsureSuccessStatusCode();
                return (true, LocalizationManager.T("QT_PROXY_OK", "Proxy works ({ms} ms).", ("ms", sw.ElapsedMilliseconds)));
            }
            catch (Exception ex)
            {
                return (false, LocalizationManager.T("QT_PROXY_FAIL", "Proxy test failed: {error}", ("error", ex.Message)));
}   }   }   }