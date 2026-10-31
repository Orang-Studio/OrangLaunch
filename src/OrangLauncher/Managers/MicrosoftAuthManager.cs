using System.Text.Json;
using OrangLauncher.Models;
namespace OrangLauncher.Managers
{
    public class MicrosoftAuthManager
    {
        public const string ClientId = "00000000402b5328";
        public const string DeviceClientId = "00000000441cc96b";
        private const string RedirectUri = "https://login.live.com/oauth20_desktop.srf";
        private const string Scope = "XboxLive.signin offline_access";
        private const string DeviceScope = "service::user.auth.xboxlive.com::MBI_SSL";
        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(45) };
        private static readonly Dictionary<string, (string Key, string Text)> XstsErrors = new()
        {
            ["2148916233"] = ("QT_XSTS_NO_XBOX", "This Microsoft account has no Xbox profile. Sign in once at https://www.xbox.com to create one, then try again."),
            ["2148916235"] = ("QT_XSTS_REGION", "Xbox Live is not available in your country or region."),
            ["2148916236"] = ("QT_XSTS_ADULT", "This account needs adult verification on xbox.com (South Korea)."),
            ["2148916237"] = ("QT_XSTS_ADULT", "This account needs adult verification on xbox.com (South Korea)."),
            ["2148916238"] = ("QT_XSTS_CHILD", "This is a child account. Add it to a Microsoft family group at https://account.microsoft.com/family first."),
        };
        public string GetLoginUrl()
        {
            return $"https://login.live.com/oauth20_authorize.srf" +
                   $"?client_id={ClientId}" +
                   $"&response_type=code" +
                   $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                   $"&scope={Uri.EscapeDataString(Scope)}";
        }
        public string GetRedirectUri() => RedirectUri;
        public static string? TryExtractCodeFromRedirect(string redirectUrlOrCode)
        {
            if (string.IsNullOrWhiteSpace(redirectUrlOrCode)) return null;
            var text = redirectUrlOrCode.Trim();
            var idx = text.IndexOf("code=", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var code = text[(idx + 5)..];
                var amp = code.IndexOf('&');
                if (amp >= 0) code = code[..amp];
                return Uri.UnescapeDataString(code);
            }
            return text.Contains("://") || text.Contains(' ') ? null : text;
        }
        public async Task<AuthResult> AuthenticateWithCodeAsync(string authCode)
        {
            var tokenResponse = await PostTokenAsync(new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["code"] = authCode,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = RedirectUri,
                ["scope"] = Scope
            });
            var result = await CompleteAsync(tokenResponse.AccessToken, tokenResponse.RefreshToken ?? "");
            result.ClientId = ClientId;
            return result;
        }
        public async Task<AuthResult> RefreshTokenAsync(string refreshToken, string? clientId = null)
        {
            var device = clientId == DeviceClientId;
            var form = new Dictionary<string, string>
            {
                ["client_id"] = device ? DeviceClientId : ClientId,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token",
                ["scope"] = device ? DeviceScope : Scope
            };
            if (!device) form["redirect_uri"] = RedirectUri;
            var tokenResponse = await PostTokenAsync(form);
            var result = await CompleteAsync(tokenResponse.AccessToken, string.IsNullOrEmpty(tokenResponse.RefreshToken) ? refreshToken : tokenResponse.RefreshToken!);
            result.ClientId = device ? DeviceClientId : ClientId;
            return result;
        }
        public async Task<DeviceCodeInfo> StartDeviceCodeAsync()
        {
            var response = await _httpClient.PostAsync("https://login.live.com/oauth20_connect.srf", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = DeviceClientId,
                ["scope"] = DeviceScope,
                ["response_type"] = "device_code"
            }));
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("device_code", out var dc))
                throw new Exception(Str(root, "error_description") ?? Str(root, "error") ?? "device code request failed");
            return new DeviceCodeInfo
            {
                DeviceCode = dc.GetString() ?? "",
                UserCode = Str(root, "user_code") ?? "",
                VerificationUri = Str(root, "verification_uri") ?? "https://www.microsoft.com/link",
                Interval = root.TryGetProperty("interval", out var i) && i.TryGetInt32(out var iv) ? iv : 5,
                ExpiresIn = root.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var ev) ? ev : 900
            };
        }
        public async Task<(string State, AuthResult? Result, string? Error)> PollDeviceCodeAsync(string deviceCode)
        {
            var response = await _httpClient.PostAsync("https://login.live.com/oauth20_token.srf", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = DeviceClientId,
                ["device_code"] = deviceCode,
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code"
            }));
            var json = await response.Content.ReadAsStringAsync();
            JsonDocument? doc = null;
            try { doc = JsonDocument.Parse(json); } catch { }
            using (doc)
            {
                var root = doc?.RootElement;
                if (response.IsSuccessStatusCode && root.HasValue && Str(root.Value, "access_token") is string access)
                {
                    var result = await CompleteAsync(access, Str(root.Value, "refresh_token") ?? "");
                    result.ClientId = DeviceClientId;
                    return ("ok", result, null);
                }
                var err = root.HasValue ? Str(root.Value, "error") ?? "" : "";
                if (err is "authorization_pending" or "slow_down" or "") return (err == "slow_down" ? "slow" : "pending", null, null);
                return ("error", null, root.HasValue ? Str(root.Value, "error_description") ?? err : err);
            }
        }
        public async Task<bool> ValidateMinecraftToken(string token)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/minecraft/profile");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
        public static async Task<UserProfile> EnsureValidAsync(UserProfile profile)
        {
            if (!profile.IsMicrosoft) return profile;
            var auth = new MicrosoftAuthManager();
            var token = profile.MinecraftToken ?? profile.AccessToken;
            if (!string.IsNullOrEmpty(token) && await auth.ValidateMinecraftToken(token)) return profile;
            if (string.IsNullOrEmpty(profile.RefreshToken))
                throw new Exception(LocalizationManager.GetString("QT_MS_RELOGIN", "This Microsoft account has to sign in again (Settings > Accounts)."));
            var result = await auth.RefreshTokenAsync(profile.RefreshToken, profile.MsClientId);
            Apply(profile, result);
            ProfileManager.Instance.AddOrUpdateProfile(profile);
            return profile;
        }
        public static void Apply(UserProfile profile, AuthResult result)
        {
            profile.Username = result.Username;
            profile.Uuid = result.Uuid;
            profile.AccessToken = result.AccessToken;
            profile.MinecraftToken = result.AccessToken;
            profile.RefreshToken = result.RefreshToken;
            profile.MsClientId = result.ClientId;
            profile.LastUsed = DateTime.Now;
        }
        public static UserProfile ToProfile(AuthResult result)
        {
            var profile = new UserProfile { Type = "Microsoft" };
            Apply(profile, result);
            return profile;
        }
        private static string? Str(JsonElement e, string name)
            => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        private async Task<TokenResponse> PostTokenAsync(Dictionary<string, string> form)
        {
            var response = await _httpClient.PostAsync("https://login.live.com/oauth20_token.srf", new FormUrlEncodedContent(form));
            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new Exception("Failed to get Microsoft token: " + json);
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(json);
            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
                throw new Exception("Failed to get Microsoft token");
            return tokenResponse;
        }
        private async Task<AuthResult> CompleteAsync(string microsoftToken, string refreshToken)
        {
            var xbl = await GetXboxLiveTokenAsync(microsoftToken);
            var xsts = await GetXstsTokenAsync(xbl);
            var minecraftToken = await GetMinecraftTokenAsync(xsts.Token, xsts.UserHash);
            var profile = await GetMinecraftProfileAsync(minecraftToken);
            return new AuthResult
            {
                Username = profile.Name,
                Uuid = profile.Id,
                AccessToken = minecraftToken,
                RefreshToken = refreshToken
            };
        }
        private async Task<string> GetXboxLiveTokenAsync(string accessToken)
        {
            string last = "";
            foreach (var ticket in new[] { $"d={accessToken}", accessToken, $"t={accessToken}" })
            {
                var requestBody = new
                {
                    Properties = new { AuthMethod = "RPS", SiteName = "user.auth.xboxlive.com", RpsTicket = ticket },
                    RelyingParty = "http://auth.xboxlive.com",
                    TokenType = "JWT"
                };
                var response = await _httpClient.PostAsync("https://user.auth.xboxlive.com/user/authenticate",
                    new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json"));
                last = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode) continue;
                using var doc = JsonDocument.Parse(last);
                if (Str(doc.RootElement, "Token") is string token) return token;
            }
            throw new Exception("Failed to get Xbox Live token: " + last);
        }
        private async Task<(string Token, string UserHash)> GetXstsTokenAsync(string xboxToken)
        {
            var requestBody = new
            {
                Properties = new { SandboxId = "RETAIL", UserTokens = new[] { xboxToken } },
                RelyingParty = "rp://api.minecraftservices.com/",
                TokenType = "JWT"
            };
            var response = await _httpClient.PostAsync("https://xsts.auth.xboxlive.com/xsts/authorize",
                new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json"));
            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    using var err = JsonDocument.Parse(json);
                    if (err.RootElement.TryGetProperty("XErr", out var xerr))
                    {
                        var code = xerr.ToString();
                        if (XstsErrors.TryGetValue(code, out var msg))
                            throw new Exception(LocalizationManager.GetString(msg.Key, msg.Text));
                    }
                }
                catch (JsonException) { }
                throw new Exception("Failed to get XSTS token: " + json);
            }
            using var doc = JsonDocument.Parse(json);
            var token = Str(doc.RootElement, "Token") ?? "";
            var uhs = doc.RootElement.GetProperty("DisplayClaims").GetProperty("xui")[0].GetProperty("uhs").GetString() ?? "";
            return (token, uhs);
        }
        private async Task<string> GetMinecraftTokenAsync(string xstsToken, string userHash)
        {
            var requestBody = new { identityToken = $"XBL3.0 x={userHash};{xstsToken}" };
            var response = await _httpClient.PostAsync("https://api.minecraftservices.com/authentication/login_with_xbox",
                new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json"));
            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) throw new Exception("Failed to get Minecraft token: " + json);
            using var doc = JsonDocument.Parse(json);
            return Str(doc.RootElement, "access_token") ?? throw new Exception("Minecraft auth failed");
        }
        private async Task<(string Id, string Name)> GetMinecraftProfileAsync(string accessToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/minecraft/profile");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                throw new Exception(LocalizationManager.GetString("QT_MS_NO_GAME", "This Microsoft account does not own Minecraft: Java Edition (no profile found)."));
            if (!response.IsSuccessStatusCode) throw new Exception("Failed to get Minecraft profile: " + json);
            using var doc = JsonDocument.Parse(json);
            return (Str(doc.RootElement, "id") ?? "", Str(doc.RootElement, "name") ?? "");
        }
        public class DeviceCodeInfo
        {
            public string DeviceCode { get; set; } = "";
            public string UserCode { get; set; } = "";
            public string VerificationUri { get; set; } = "";
            public int Interval { get; set; } = 5;
            public int ExpiresIn { get; set; } = 900;
        }
        public class AuthResult
        {
            public string Username { get; set; } = "";
            public string Uuid { get; set; } = "";
            public string AccessToken { get; set; } = "";
            public string RefreshToken { get; set; } = "";
            public string ClientId { get; set; } = MicrosoftAuthManager.ClientId;
        }
        private class TokenResponse
        {
            public string? access_token { get; set; }
            public string? refresh_token { get; set; }
            public string AccessToken => access_token ?? "";
            public string? RefreshToken => refresh_token;
        }
    }
}