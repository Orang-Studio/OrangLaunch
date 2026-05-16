using System.Text.Json;
namespace OrangLauncher.Managers
{
    public class MicrosoftAuthManager
    {
        private const string ClientId = "00000000402b5328";
        private const string RedirectUri = "https://login.live.com/oauth20_desktop.srf";
        private const string Scope = "XboxLive.signin offline_access";
        private readonly HttpClient _httpClient = new();
        public string GetLoginUrl()
        {
            return $"https://login.live.com/oauth20_authorize.srf" +
                   $"?client_id={ClientId}" +
                   $"&response_type=code" +
                   $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                   $"&scope={Uri.EscapeDataString(Scope)}";
        }
        public string GetRedirectUri() => RedirectUri;
        public async Task<AuthResult> AuthenticateWithCodeAsync(string authCode)
        {
            var tokenResponse = await ExchangeCodeForTokensAsync(authCode);
            var xboxToken = await GetXboxLiveTokenAsync(tokenResponse.AccessToken);
            var xstsToken = await GetXstsTokenAsync(xboxToken.Token);
            var minecraftToken = await GetMinecraftTokenAsync(xstsToken.Token, xstsToken.UserHash);
            var profile = await GetMinecraftProfileAsync(minecraftToken.AccessToken);
            return new AuthResult
            {
                Username = profile.Name,
                Uuid = profile.Id,
                AccessToken = minecraftToken.AccessToken,
                RefreshToken = tokenResponse.RefreshToken ?? ""
            };
        }
        public async Task<AuthResult> RefreshTokenAsync(string refreshToken)
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string>("client_id", ClientId),
                new System.Collections.Generic.KeyValuePair<string, string>("refresh_token", refreshToken),
                new System.Collections.Generic.KeyValuePair<string, string>("grant_type", "refresh_token"),
                new System.Collections.Generic.KeyValuePair<string, string>("redirect_uri", RedirectUri),
                new System.Collections.Generic.KeyValuePair<string, string>("scope", Scope)
            });
            var response = await _httpClient.PostAsync("https://login.live.com/oauth20_token.srf", content);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(json);
            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
                throw new Exception("Failed to refresh token");
            var newRefreshToken = !string.IsNullOrEmpty(tokenResponse.RefreshToken) 
                ? tokenResponse.RefreshToken 
                : refreshToken;
            var xboxToken = await GetXboxLiveTokenAsync(tokenResponse.AccessToken);
            var xstsToken = await GetXstsTokenAsync(xboxToken.Token);
            var minecraftToken = await GetMinecraftTokenAsync(xstsToken.Token, xstsToken.UserHash);
            var profile = await GetMinecraftProfileAsync(minecraftToken.AccessToken);
            return new AuthResult
            {
                Username = profile.Name,
                Uuid = profile.Id,
                AccessToken = minecraftToken.AccessToken,
                RefreshToken = newRefreshToken
            };
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
        private async Task<TokenResponse> ExchangeCodeForTokensAsync(string code)
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string>("client_id", ClientId),
                new System.Collections.Generic.KeyValuePair<string, string>("code", code),
                new System.Collections.Generic.KeyValuePair<string, string>("grant_type", "authorization_code"),
                new System.Collections.Generic.KeyValuePair<string, string>("redirect_uri", RedirectUri),
                new System.Collections.Generic.KeyValuePair<string, string>("scope", Scope)
            });
            var response = await _httpClient.PostAsync("https://login.live.com/oauth20_token.srf", content);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TokenResponse>(json) ?? throw new Exception("Token exchange failed");
        }
        private async Task<XboxTokenResponse> GetXboxLiveTokenAsync(string accessToken)
        {
            var requestBody = new
            {
                Properties = new
                {
                    AuthMethod = "RPS",
                    SiteName = "user.auth.xboxlive.com",
                    RpsTicket = $"d={accessToken}"
                },
                RelyingParty = "http://auth.xboxlive.com",
                TokenType = "JWT"
            };
            var request = new HttpRequestMessage(HttpMethod.Post, "https://user.auth.xboxlive.com/user/authenticate")
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json")
            };
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<XboxTokenResponse>(json) ?? throw new Exception("Xbox Live auth failed");
        }
        private async Task<XstsTokenResponse> GetXstsTokenAsync(string xboxToken)
        {
            var requestBody = new
            {
                Properties = new
                {
                    SandboxId = "RETAIL",
                    UserTokens = new[] { xboxToken }
                },
                RelyingParty = "rp://api.minecraftservices.com/",
                TokenType = "JWT"
            };
            var request = new HttpRequestMessage(HttpMethod.Post, "https://xsts.auth.xboxlive.com/xsts/authorize")
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json")
            };
            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var xsts = JsonSerializer.Deserialize<XboxTokenResponse>(json) ?? throw new Exception("XSTS auth failed");
            return new XstsTokenResponse
            {
                Token = xsts.Token,
                UserHash = xsts.DisplayClaims?.Xui?[0]?.Uhs ?? ""
            };
        }
        private async Task<MinecraftTokenResponse> GetMinecraftTokenAsync(string xstsToken, string userHash)
        {
            var requestBody = new { identityToken = $"XBL3.0 x={userHash};{xstsToken}" };
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.minecraftservices.com/authentication/login_with_xbox")
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json")
            };
            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<MinecraftTokenResponse>(json) ?? throw new Exception("Minecraft auth failed");
        }
        private async Task<MinecraftProfile> GetMinecraftProfileAsync(string accessToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/minecraft/profile");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<MinecraftProfile>(json) ?? throw new Exception("Failed to get profile");
        }
        public class AuthResult
        {
            public string Username { get; set; } = "";
            public string Uuid { get; set; } = "";
            public string AccessToken { get; set; } = "";
            public string RefreshToken { get; set; } = "";
        }
        private class TokenResponse
        {
            public string? access_token { get; set; }
            public string? refresh_token { get; set; }
            public string AccessToken => access_token ?? "";
            public string? RefreshToken => refresh_token;
        }
        private class XboxTokenResponse
        {
            public string Token { get; set; } = "";
            public DisplayClaims? DisplayClaims { get; set; }
        }
        private class DisplayClaims
        {
            public XuiItem[]? xui { get; set; }
            public XuiItem[]? Xui => xui;
        }
        private class XuiItem
        {
            public string? uhs { get; set; }
            public string? Uhs => uhs;
        }
        private class XstsTokenResponse
        {
            public string Token { get; set; } = "";
            public string UserHash { get; set; } = "";
        }
        private class MinecraftTokenResponse
        {
            public string? access_token { get; set; }
            public string AccessToken => access_token ?? "";
        }
        private class MinecraftProfile
        {
            public string? id { get; set; }
            public string? name { get; set; }
            public string Id => id ?? "";
            public string Name => name ?? "";
        }
    }
}