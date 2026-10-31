using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;
namespace OrangLauncher.Managers
{
    public static class LegacySkinProxy
    {
        private static readonly object Gate = new();
        private static TcpListener? _listener;
        private static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
        private static readonly Dictionary<string, (DateTime At, SkinData? Data)> Cache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Regex SkinPath = new(@"^/(?:MinecraftSkins|skin)/([^/]+)\.png$", RegexOptions.IgnoreCase);
        private static readonly Regex CapePath = new(@"^/(?:MinecraftCloaks|cloak)/([^/]+)\.png$", RegexOptions.IgnoreCase);
        private static readonly string[] SkipHeaders = { "Host", "Connection", "Proxy-Connection", "Proxy-Authorization", "Keep-Alive", "Content-Length", "Content-Type", "Transfer-Encoding" };
        public static Action<string>? Log { get; set; }
        public static bool IsNeeded(string? version) =>
            McVersion.Below(version, 1, 7) || Regex.IsMatch(version ?? "", @"^(rd-|inf-|[abc]\d)");
        public static List<string> JvmArgs()
        {
            var port = EnsureStarted();
            return port == 0 ? new List<string>() : new List<string> { "-Dhttp.proxyHost=127.0.0.1", $"-Dhttp.proxyPort={port}" };
        }
        private static int EnsureStarted()
        {
            lock (Gate)
            {
                if (_listener != null) return ((IPEndPoint)_listener.LocalEndpoint).Port;
                try
                {
                    var listener = new TcpListener(IPAddress.Loopback, 0);
                    listener.Start();
                    _listener = listener;
                    _ = Task.Run(AcceptLoop);
                    return ((IPEndPoint)listener.LocalEndpoint).Port;
                }
                catch (Exception ex)
                {
                    Log?.Invoke($"[SkinFix] Could not start: {ex.Message}");
                    return 0;
                }
            }
        }
        private static async Task AcceptLoop()
        {
            while (_listener != null)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleAsync(client));
                }
                catch { await Task.Delay(200); }
            }
        }
        private static async Task HandleAsync(TcpClient client)
        {
            using (client)
            {
                try
                {
                    var stream = client.GetStream();
                    var (method, target, headers, body) = await ReadRequestAsync(stream);
                    if (method == null || !Uri.TryCreate(target, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttp)
                    {
                        await WriteAsync(stream, 400, "text/plain", Encoding.ASCII.GetBytes("Bad request"));
                        return;
                    }
                    var name = MatchName(SkinPath, uri) ?? QueryUser(uri, "/skin/get.jsp");
                    if (name != null)
                    {
                        var png = LegacySkin((await LookupAsync(name))?.Skin);
                        await WriteAsync(stream, png == null ? 404 : 200, "image/png", png ?? Array.Empty<byte>());
                        return;
                    }
                    name = MatchName(CapePath, uri) ?? QueryUser(uri, "/cloak/get.jsp");
                    if (name != null)
                    {
                        var cape = (await LookupAsync(name))?.Cape;
                        await WriteAsync(stream, cape == null ? 404 : 200, "image/png", cape ?? Array.Empty<byte>());
                        return;
                    }
                    await ForwardAsync(stream, method, uri, headers, body);
                }
                catch { }
            }
        }
        private static string? MatchName(Regex re, Uri uri)
        {
            var m = re.Match(uri.AbsolutePath);
            return m.Success ? Uri.UnescapeDataString(m.Groups[1].Value) : null;
        }
        private static string? QueryUser(Uri uri, string path)
        {
            if (!uri.AbsolutePath.Equals(path, StringComparison.OrdinalIgnoreCase)) return null;
            foreach (var part in uri.Query.TrimStart('?').Split('&'))
                if (part.StartsWith("user=", StringComparison.OrdinalIgnoreCase)) return Uri.UnescapeDataString(part[5..]);
            return null;
        }
        private static async Task<SkinData?> LookupAsync(string name)
        {
            lock (Cache)
                if (Cache.TryGetValue(name, out var hit) && DateTime.UtcNow - hit.At < TimeSpan.FromMinutes(5)) return hit.Data;
            var data = await SkinService.LoadByNameAsync(name);
            lock (Cache) Cache[name] = (DateTime.UtcNow, data);
            Log?.Invoke(data?.Skin != null ? $"[SkinFix] Served skin for {name}" : $"[SkinFix] No skin found for {name}");
            return data;
        }
        private static byte[]? LegacySkin(byte[]? modern)
        {
            if (modern == null) return null;
            try
            {
                using var src = SKBitmap.Decode(modern);
                if (src == null) return null;
                using var dst = new SKBitmap(64, 32, SKColorType.Rgba8888, SKAlphaType.Unpremul);
                using (var canvas = new SKCanvas(dst))
                {
                    canvas.Clear(SKColors.Transparent);
                    var h = src.Height >= src.Width ? src.Height / 2 : src.Height;
                    using var paint = new SKPaint { FilterQuality = SKFilterQuality.None, BlendMode = SKBlendMode.Src };
                    canvas.DrawBitmap(src, new SKRect(0, 0, src.Width, h), new SKRect(0, 0, 64, 32), paint);
                }
                using var img = SKImage.FromBitmap(dst);
                using var enc = img.Encode(SKEncodedImageFormat.Png, 100);
                return enc.ToArray();
            }
            catch { return modern; }
        }
        private static async Task ForwardAsync(NetworkStream stream, string method, Uri uri, List<(string, string)> headers, byte[] body)
        {
            using var req = new HttpRequestMessage(new HttpMethod(method), uri);
            if (body.Length > 0)
            {
                req.Content = new ByteArrayContent(body);
                var ct = headers.FirstOrDefault(h => h.Item1.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)).Item2;
                if (!string.IsNullOrEmpty(ct)) req.Content.Headers.TryAddWithoutValidation("Content-Type", ct);
            }
            foreach (var (k, v) in headers)
                if (!SkipHeaders.Contains(k, StringComparer.OrdinalIgnoreCase)) req.Headers.TryAddWithoutValidation(k, v);
            try
            {
                using var resp = await Http.SendAsync(req);
                var data = await resp.Content.ReadAsByteArrayAsync();
                var extra = resp.Headers.Location != null ? $"Location: {resp.Headers.Location}\r\n" : "";
                await WriteAsync(stream, (int)resp.StatusCode, resp.Content.Headers.ContentType?.ToString() ?? "application/octet-stream", data, extra);
            }
            catch
            {
                await WriteAsync(stream, 502, "text/plain", Encoding.ASCII.GetBytes("Bad gateway"));
            }
        }
        private static async Task<(string? Method, string Target, List<(string, string)> Headers, byte[] Body)> ReadRequestAsync(NetworkStream stream)
        {
            var buf = new List<byte>();
            var one = new byte[1];
            while (buf.Count < 65536)
            {
                if (await stream.ReadAsync(one) == 0) break;
                buf.Add(one[0]);
                var n = buf.Count;
                if (n >= 4 && buf[n - 4] == '\r' && buf[n - 3] == '\n' && buf[n - 2] == '\r' && buf[n - 1] == '\n') break;
            }
            var lines = Encoding.ASCII.GetString(buf.ToArray()).Split("\r\n");
            var first = lines[0].Split(' ');
            var headers = new List<(string, string)>();
            foreach (var line in lines.Skip(1))
            {
                var i = line.IndexOf(':');
                if (i > 0) headers.Add((line[..i].Trim(), line[(i + 1)..].Trim()));
            }
            var body = Array.Empty<byte>();
            var len = headers.FirstOrDefault(h => h.Item1.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)).Item2;
            if (int.TryParse(len, out var size) && size > 0 && size < 16 * 1024 * 1024)
            {
                body = new byte[size];
                var read = 0;
                while (read < size)
                {
                    var r = await stream.ReadAsync(body.AsMemory(read, size - read));
                    if (r == 0) break;
                    read += r;
                }
            }
            return first.Length >= 2 ? (first[0], first[1], headers, body) : (null, "", headers, body);
        }
        private static async Task WriteAsync(NetworkStream stream, int status, string contentType, byte[] body, string extraHeaders = "")
        {
            var reason = status switch { 200 => "OK", 400 => "Bad Request", 404 => "Not Found", 502 => "Bad Gateway", _ => ((HttpStatusCode)status).ToString() };
            var head = $"HTTP/1.1 {status} {reason}\r\nContent-Type: {contentType}\r\nContent-Length: {body.Length}\r\n{extraHeaders}Connection: close\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(head));
            if (body.Length > 0) await stream.WriteAsync(body);
            await stream.FlushAsync();
        }
    }
}
