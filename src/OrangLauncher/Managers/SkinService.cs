using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OrangLauncher.Models;
namespace OrangLauncher.Managers
{
    public class CapeInfo
    {
        public string Id { get; set; } = "";
        public string Alias { get; set; } = "";
        public string Url { get; set; } = "";
        public bool Active { get; set; }
        public byte[]? Image { get; set; }
    }
    public class SkinData
    {
        public string Username { get; set; } = "";
        public string Uuid { get; set; } = "";
        public byte[]? Skin { get; set; }
        public bool Slim { get; set; }
        public byte[]? Cape { get; set; }
        public List<CapeInfo> Capes { get; set; } = new();
        public bool FromAccount { get; set; }
        public string? SkinUrl { get; set; }
    }
    public static class SkinService
    {
        private const string ProfileUrl = "https://api.minecraftservices.com/minecraft/profile";
        private static readonly HttpClient Http = CreateClient();
        private static readonly Dictionary<string, byte[]> ImageCache = new();
        private static HttpClient CreateClient()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            c.DefaultRequestHeaders.Add("User-Agent", "OrangLauncher");
            return c;
        }
        public static async Task<byte[]> GetImageAsync(string url)
        {
            lock (ImageCache) if (ImageCache.TryGetValue(url, out var cached)) return cached;
            var bytes = await Http.GetByteArrayAsync(url);
            lock (ImageCache) ImageCache[url] = bytes;
            return bytes;
        }
        private static async Task<HttpResponseMessage?> AuthorizedAsync(UserProfile account, Func<HttpRequestMessage> build)
        {
            if (!account.IsMicrosoft) return null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var token = account.MinecraftToken ?? account.AccessToken;
                if (!string.IsNullOrEmpty(token) && token != "0")
                {
                    var req = build();
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    var resp = await Http.SendAsync(req);
                    if (resp.StatusCode != HttpStatusCode.Unauthorized) return resp;
                }
                if (attempt > 0 || string.IsNullOrEmpty(account.RefreshToken)) return null;
                var result = await new MicrosoftAuthManager().RefreshTokenAsync(account.RefreshToken!, account.MsClientId);
                MicrosoftAuthManager.Apply(account, result);
                ProfileManager.Instance.AddOrUpdateProfile(account);
            }
            return null;
        }
        public static async Task<SkinData?> LoadAsync(UserProfile account)
        {
            if (account.IsMicrosoft)
            {
                try
                {
                    var resp = await AuthorizedAsync(account, () => new HttpRequestMessage(HttpMethod.Get, ProfileUrl));
                    if (resp is { IsSuccessStatusCode: true })
                    {
                        var data = ParseProfile(await resp.Content.ReadAsStringAsync());
                        await FillImagesAsync(data);
                        data.FromAccount = true;
                        return data;
                    }
                }
                catch { }
            }
            return await LoadPublicAsync(account);
        }
        private static SkinData ParseProfile(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var data = new SkinData
            {
                Username = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                Uuid = root.TryGetProperty("id", out var i) ? i.GetString() ?? "" : ""
            };
            if (root.TryGetProperty("skins", out var skins))
            {
                JsonElement? active = null;
                foreach (var s in skins.EnumerateArray())
                {
                    active ??= s;
                    if (s.TryGetProperty("state", out var st) && st.GetString() == "ACTIVE") { active = s; break; }
                }
                if (active is JsonElement a)
                {
                    data.Slim = a.TryGetProperty("variant", out var v) && string.Equals(v.GetString(), "SLIM", StringComparison.OrdinalIgnoreCase);
                    if (a.TryGetProperty("url", out var u)) data.SkinUrl = u.GetString();
                }
            }
            if (root.TryGetProperty("capes", out var capes))
            {
                foreach (var c in capes.EnumerateArray())
                {
                    data.Capes.Add(new CapeInfo
                    {
                        Id = c.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                        Alias = c.TryGetProperty("alias", out var al) ? al.GetString() ?? "" : "",
                        Url = c.TryGetProperty("url", out var cu) ? cu.GetString() ?? "" : "",
                        Active = c.TryGetProperty("state", out var cs) && cs.GetString() == "ACTIVE"
                    });
                }
            }
            return data;
        }
        private static async Task FillImagesAsync(SkinData data)
        {
            if (!string.IsNullOrEmpty(data.SkinUrl))
                data.Skin = await GetImageAsync(data.SkinUrl);
            foreach (var cape in data.Capes)
            {
                try { if (!string.IsNullOrEmpty(cape.Url)) cape.Image = await GetImageAsync(cape.Url); } catch { }
                if (cape.Active) data.Cape = cape.Image;
            }
        }
        public static Task<SkinData?> LoadByNameAsync(string username) => LoadPublicAsync(new UserProfile { Username = username, Type = "Offline" });
        private static async Task<SkinData?> LoadPublicAsync(UserProfile account)
        {
            try
            {
                var uuid = (account.Uuid ?? "").Replace("-", "");
                if (!account.IsMicrosoft || uuid.Trim('0').Length == 0)
                {
                    var r = await Http.GetAsync($"https://api.mojang.com/users/profiles/minecraft/{Uri.EscapeDataString(account.Username)}");
                    if (!r.IsSuccessStatusCode) return null;
                    using var d = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
                    uuid = d.RootElement.GetProperty("id").GetString() ?? "";
                }
                var json = await Http.GetStringAsync($"https://sessionserver.mojang.com/session/minecraft/profile/{uuid}");
                using var doc = JsonDocument.Parse(json);
                var data = new SkinData { Username = doc.RootElement.GetProperty("name").GetString() ?? account.Username, Uuid = uuid };
                foreach (var prop in doc.RootElement.GetProperty("properties").EnumerateArray())
                {
                    if (prop.GetProperty("name").GetString() != "textures") continue;
                    var tex = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(prop.GetProperty("value").GetString() ?? "")));
                    var textures = tex.RootElement.GetProperty("textures");
                    if (textures.TryGetProperty("SKIN", out var skin))
                    {
                        data.Skin = await GetImageAsync(skin.GetProperty("url").GetString() ?? "");
                        data.Slim = skin.TryGetProperty("metadata", out var md) && md.TryGetProperty("model", out var model) && model.GetString() == "slim";
                    }
                    if (textures.TryGetProperty("CAPE", out var cape))
                    {
                        try { data.Cape = await GetImageAsync(cape.GetProperty("url").GetString() ?? ""); } catch { }
                    }
                }
                return data;
            }
            catch { return null; }
        }
        public static bool IsValidSkinFile(string path)
        {
            try
            {
                var decoded = Rendering.SkinTextureLoader.DecodePng(File.ReadAllBytes(path));
                return decoded is { Width: 64 } d && (d.Height == 64 || d.Height == 32);
            }
            catch { return false; }
        }
        private static async Task EnsureOk(HttpResponseMessage? resp)
        {
            if (resp == null) throw new Exception(LocalizationManager.GetString("QT_NOT_SIGNED_IN", "Not signed in"));
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                throw new Exception($"HTTP {(int)resp.StatusCode}: {(body.Length > 200 ? body[..200] : body)}");
            }
        }
        public static async Task UploadSkinAsync(UserProfile account, string path, bool slim)
        {
            if (!IsValidSkinFile(path))
                throw new Exception(LocalizationManager.GetString("QT_SKIN_BAD_FILE", "A skin must be a 64x64 or 64x32 PNG image."));
            var bytes = await File.ReadAllBytesAsync(path);
            var resp = await AuthorizedAsync(account, () =>
            {
                var content = new MultipartFormDataContent();
                content.Add(new StringContent(slim ? "slim" : "classic"), "variant");
                var file = new ByteArrayContent(bytes);
                file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                content.Add(file, "file", Path.GetFileName(path));
                return new HttpRequestMessage(HttpMethod.Post, ProfileUrl + "/skins") { Content = content };
            });
            await EnsureOk(resp);
        }
        public static async Task ResetSkinAsync(UserProfile account)
        {
            var resp = await AuthorizedAsync(account, () => new HttpRequestMessage(HttpMethod.Delete, ProfileUrl + "/skins/active"));
            await EnsureOk(resp);
        }
        public static async Task SetActiveCapeAsync(UserProfile account, string? capeId)
        {
            var resp = await AuthorizedAsync(account, () => string.IsNullOrEmpty(capeId)
                ? new HttpRequestMessage(HttpMethod.Delete, ProfileUrl + "/capes/active")
                : new HttpRequestMessage(HttpMethod.Put, ProfileUrl + "/capes/active")
                {
                    Content = new StringContent(JsonSerializer.Serialize(new { capeId }), Encoding.UTF8, "application/json")
                });
            await EnsureOk(resp);
        }
    }
}
