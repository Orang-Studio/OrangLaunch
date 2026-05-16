using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
namespace OrangLauncher.Managers
{
    public class PlayerSkinInfo
    {
        public string Username { get; set; } = "";
        public string Uuid { get; set; } = "";
        public string SkinUrl { get; set; } = "";
        public string CapeUrl { get; set; } = "";
        public bool IsSlim { get; set; }
        public BitmapImage? SkinImage { get; set; }
        public BitmapImage? HeadImage { get; set; }
    }
    public static class SkinManager
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        static SkinManager()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "OrangLauncher/1.0");
        }        public static async Task<string?> GetUuidFromUsernameAsync(string username)
        {
            try
            {
                var response = await _httpClient.GetStringAsync($"https://api.mojang.com/users/profiles/minecraft/{username}");
                var json = JsonDocument.Parse(response);
                return json.RootElement.GetProperty("id").GetString();
            }
            catch
            {
                return null;
            }
        }        
        public static async Task<PlayerSkinInfo?> GetPlayerSkinWithTokenAsync(string accessToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/minecraft/profile");
                request.Headers.Add("Authorization", $"Bearer {accessToken}");
                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    return null;
                var content = await response.Content.ReadAsStringAsync();
                var profileJson = JsonDocument.Parse(content);
                var skinInfo = new PlayerSkinInfo
                {
                    Username = profileJson.RootElement.GetProperty("name").GetString() ?? "",
                    Uuid = profileJson.RootElement.GetProperty("id").GetString() ?? ""
                };
                if (profileJson.RootElement.TryGetProperty("skins", out var skins))
                {
                    foreach (var skin in skins.EnumerateArray())
                    {
                        if (skin.TryGetProperty("state", out var state) && state.GetString() == "ACTIVE")
                        {
                            skinInfo.SkinUrl = skin.GetProperty("url").GetString() ?? "";                            
                            if (skin.TryGetProperty("variant", out var variant))
                            {
                                skinInfo.IsSlim = variant.GetString()?.ToUpper() == "SLIM";
                            }
                            break;
                        }
                    }
                }
                if (profileJson.RootElement.TryGetProperty("capes", out var capes))
                {
                    foreach (var cape in capes.EnumerateArray())
                    {
                        if (cape.TryGetProperty("state", out var state) && state.GetString() == "ACTIVE")
                        {
                            skinInfo.CapeUrl = cape.GetProperty("url").GetString() ?? "";
                            break;
                        }
                    }
                }
                if (!string.IsNullOrEmpty(skinInfo.SkinUrl))
                {
                    try
                    {
                        var skinBytes = await _httpClient.GetByteArrayAsync(skinInfo.SkinUrl);
                        skinInfo.SkinImage = BytesToBitmapImage(skinBytes);
                        skinInfo.HeadImage = ExtractHead(skinBytes);
                    }
                    catch { }
                }
                return skinInfo;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get skin with token: {ex.Message}");
                return null;
            }
        }
        public static async Task<PlayerSkinInfo?> GetPlayerSkinAsync(string username)
        {
            try
            {
                var uuid = await GetUuidFromUsernameAsync(username);
                if (string.IsNullOrEmpty(uuid)) return null;
                var profileResponse = await _httpClient.GetStringAsync($"https://sessionserver.mojang.com/session/minecraft/profile/{uuid}");
                var profileJson = JsonDocument.Parse(profileResponse);
                var skinInfo = new PlayerSkinInfo
                {
                    Username = profileJson.RootElement.GetProperty("name").GetString() ?? username,
                    Uuid = uuid
                };
                if (profileJson.RootElement.TryGetProperty("properties", out var properties))
                {
                    foreach (var prop in properties.EnumerateArray())
                    {
                        if (prop.GetProperty("name").GetString() == "textures")
                        {
                            var texturesBase64 = prop.GetProperty("value").GetString();
                            if (!string.IsNullOrEmpty(texturesBase64))
                            {
                                var texturesJson = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(texturesBase64));
                                var textures = JsonDocument.Parse(texturesJson);
                                if (textures.RootElement.TryGetProperty("textures", out var texturesObj))
                                {
                                    if (texturesObj.TryGetProperty("SKIN", out var skin))
                                    {
                                        skinInfo.SkinUrl = skin.GetProperty("url").GetString() ?? "";
                                        if (skin.TryGetProperty("metadata", out var meta) && meta.TryGetProperty("model", out var model))
                                        {
                                            skinInfo.IsSlim = model.GetString() == "slim";
                                        }
                                    }
                                    if (texturesObj.TryGetProperty("CAPE", out var cape))
                                    {
                                        skinInfo.CapeUrl = cape.GetProperty("url").GetString() ?? "";
                                    }
                                }
                            }
                        }
                    }
                }
                if (!string.IsNullOrEmpty(skinInfo.SkinUrl))
                {
                    try
                    {
                        var skinBytes = await _httpClient.GetByteArrayAsync(skinInfo.SkinUrl);
                        skinInfo.SkinImage = BytesToBitmapImage(skinBytes);
                        skinInfo.HeadImage = ExtractHead(skinBytes);
                    }
                    catch { }
                }
                return skinInfo;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get skin: {ex.Message}");
                return null;
            }
        }
        public static async Task<BitmapImage?> GetSkinImageAsync(string url)
        {
            try
            {
                var bytes = await _httpClient.GetByteArrayAsync(url);
                return BytesToBitmapImage(bytes);
            }
            catch
            {
                return null;
            }
        }
        public static async Task<BitmapImage?> GetHeadAvatarAsync(string uuidOrUsername, int size = 64)
        {
            try
            {
                var uuid = uuidOrUsername.Contains("-") || uuidOrUsername.Length == 32 
                    ? uuidOrUsername 
                    : await GetUuidFromUsernameAsync(uuidOrUsername);
                if (string.IsNullOrEmpty(uuid)) return null;
                var avatarUrl = $"https://crafatar.com/avatars/{uuid}?size={size}&overlay";
                var bytes = await _httpClient.GetByteArrayAsync(avatarUrl);
                return BytesToBitmapImage(bytes);
            }
            catch
            {
                return null;
            }
        }
        public static async Task<BitmapImage?> GetHead3DAsync(string uuidOrUsername, int size = 64)
        {
            try
            {
                var uuid = uuidOrUsername.Contains("-") || uuidOrUsername.Length == 32 
                    ? uuidOrUsername 
                    : await GetUuidFromUsernameAsync(uuidOrUsername);
                if (string.IsNullOrEmpty(uuid)) return null;
                var avatarUrl = $"https://crafatar.com/renders/head/{uuid}?scale={Math.Max(1, size / 64)}&overlay";
                var bytes = await _httpClient.GetByteArrayAsync(avatarUrl);
                return BytesToBitmapImage(bytes);
            }
            catch
            {
                return null;
            }
        }
        public static async Task<BitmapImage?> GetBodyRenderAsync(string uuidOrUsername, int scale = 4)
        {
            try
            {
                var uuid = uuidOrUsername.Contains("-") || uuidOrUsername.Length == 32 
                    ? uuidOrUsername 
                    : await GetUuidFromUsernameAsync(uuidOrUsername);
                if (string.IsNullOrEmpty(uuid)) return null;
                var renderUrl = $"https://crafatar.com/renders/body/{uuid}?scale={scale}&overlay";
                var bytes = await _httpClient.GetByteArrayAsync(renderUrl);
                return BytesToBitmapImage(bytes);
            }
            catch
            {
                return null;
            }
        }
        private static BitmapImage BytesToBitmapImage(byte[] bytes)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = new MemoryStream(bytes);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        private static BitmapImage? ExtractHead(byte[] skinBytes)
        {
            try
            {
                using var ms = new MemoryStream(skinBytes);
                var decoder = new PngBitmapDecoder(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                var source = decoder.Frames[0];
                var croppedBitmap = new CroppedBitmap(source, new System.Windows.Int32Rect(8, 8, 8, 8));
                var scaledBitmap = new TransformedBitmap(croppedBitmap, new System.Windows.Media.ScaleTransform(8, 8));
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(scaledBitmap));
                using var outMs = new MemoryStream();
                encoder.Save(outMs);
                outMs.Position = 0;
                var result = new BitmapImage();
                result.BeginInit();
                result.CacheOption = BitmapCacheOption.OnLoad;
                result.StreamSource = outMs;
                result.EndInit();
                result.Freeze();
                return result;
            }
            catch
            {
                return null;
            }
        }
        public static async Task<(bool? canChange, DateTime? blockedUntil, string? error)> CheckUsernameChangeCooldownAsync(string accessToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/minecraft/profile/namechange");
                request.Headers.Add("Authorization", $"Bearer {accessToken}");
                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    return (null, null, "Failed to check cooldown - authentication may have expired");
                }
                var content = await response.Content.ReadAsStringAsync();
                var json = JsonDocument.Parse(content);
                var nameChangeAllowed = json.RootElement.GetProperty("nameChangeAllowed").GetBoolean();
                DateTime? blockedUntil = null;
                if (!nameChangeAllowed && json.RootElement.TryGetProperty("changedAt", out var changedAt))
                {
                    var lastChanged = DateTime.Parse(changedAt.GetString()!);
                    blockedUntil = lastChanged.AddDays(30);
                }
                return (nameChangeAllowed, blockedUntil, null);
            }
            catch (Exception ex)
            {
                return (null, null, ex.Message);
            }
        }
        public static string GetUsernameChangeInfo()
        {
            return "Username changes can only be performed through the official Minecraft website:\n" +
                   "https://www.minecraft.net/en-us/profile\n\n" +
                   "Requirements:\n" +
                   "• Must have a valid Microsoft/Mojang account\n" +
                   "• Can only change username once every 30 days\n" +
                   "• The new username must not be taken\n" +
                   "• Old username becomes available 37 days after change";
        }
        public static async Task<bool> UploadSkinAsync(string accessToken, string skinFilePath, bool isSlim = false)
        {
            try
            {
                var skinBytes = await File.ReadAllBytesAsync(skinFilePath);
                using var content = new MultipartFormDataContent();
                var fileContent = new ByteArrayContent(skinBytes);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                content.Add(fileContent, "file", Path.GetFileName(skinFilePath));
                content.Add(new StringContent(isSlim ? "slim" : "classic"), "variant");
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.minecraftservices.com/minecraft/profile/skins");
                request.Headers.Add("Authorization", $"Bearer {accessToken}");
                request.Content = content;
                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine("Skin uploaded successfully");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Skin upload failed: {response.StatusCode} - {errorContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Skin upload error: {ex.Message}");
                return false;
            }
        }
        public static async Task<bool> ResetSkinAsync(string accessToken)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Delete, "https://api.minecraftservices.com/minecraft/profile/skins/active");
                request.Headers.Add("Authorization", $"Bearer {accessToken}");
                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}