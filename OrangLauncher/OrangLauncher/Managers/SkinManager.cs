using System.Text.Json;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Security.Cryptography;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;
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
        private static readonly string _cacheDir = Path.Combine(Path.GetTempPath(), "OrangLauncherSkins");
        static SkinManager()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "OrangLauncher/1.0");
            try { Directory.CreateDirectory(_cacheDir); } catch { }
        }
        public static async Task<string?> GetUuidFromUsernameAsync(string username)
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
                        skinInfo.SkinImage = await GetSkinImageAsync(skinInfo.SkinUrl);
                        skinInfo.HeadImage = await GetHeadAvatarAsync(skinInfo.Uuid, 160);
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to assign skin image: {ex.Message}"); }
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
                        skinInfo.SkinImage = await GetSkinImageAsync(skinInfo.SkinUrl);
                        skinInfo.HeadImage = await GetHeadAvatarAsync(skinInfo.Uuid, 160);
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to assign skin image: {ex.Message}"); }
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
        public static async Task<byte[]?> GetSkinBytesAsync(string url)
        {
            try
            {
                return await _httpClient.GetByteArrayAsync(url);
            }
            catch
            {
                return null;
            }
        }
        public static async Task<BitmapImage?> GetHeadCropAsync(string skinUrl, int outputSize = 160)
        {
            try
            {
                var bytes = await GetSkinBytesAsync(skinUrl);
                if (bytes == null || bytes.Length == 0) return null;
                using var ms = new MemoryStream(bytes);
                var ras = ms.AsRandomAccessStream();
                var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(ras);
                uint headX = 8, headY = 8, headSize = 8;
                double scaleFactor = decoder.PixelWidth >= 64 ? 1.0 : (double)decoder.PixelWidth / 64.0;
                var transform = new Windows.Graphics.Imaging.BitmapTransform
                {
                    Bounds = new Windows.Graphics.Imaging.BitmapBounds
                    {
                        X = (uint)(headX * scaleFactor),
                        Y = (uint)(headY * scaleFactor),
                        Width = (uint)(headSize * scaleFactor),
                        Height = (uint)(headSize * scaleFactor)
                    },
                    ScaledWidth = (uint)outputSize,
                    ScaledHeight = (uint)outputSize,
                    InterpolationMode = Windows.Graphics.Imaging.BitmapInterpolationMode.Fant
                };
                var pixelData = await decoder.GetPixelDataAsync(decoder.BitmapPixelFormat, decoder.BitmapAlphaMode, transform, Windows.Graphics.Imaging.ExifOrientationMode.IgnoreExifOrientation, Windows.Graphics.Imaging.ColorManagementMode.DoNotColorManage);
                var pixels = pixelData.DetachPixelData();
                var outStream = new InMemoryRandomAccessStream();
                var enc = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, outStream);
                enc.SetPixelData(decoder.BitmapPixelFormat, decoder.BitmapAlphaMode, (uint)outputSize, (uint)outputSize, decoder.DpiX, decoder.DpiY, pixels);
                await enc.FlushAsync();
                outStream.Seek(0);
                byte[] pngBytes;
                try
                {
                    using (var stream = outStream.AsStreamForRead())
                    {
                        pngBytes = new byte[stream.Length];
                        await stream.ReadAsync(pngBytes, 0, pngBytes.Length);
                    }
                }
                catch
                {
                    return null;
                }
                var cached = SaveBytesToCache(pngBytes);
                if (!string.IsNullOrEmpty(cached)) return new BitmapImage(new Uri(cached));
                return BytesToBitmapImage(pngBytes);
            }
            catch
            {
                return null;
            }
        }
        public static async Task<BitmapImage?> GetFrontCompositeAsync(string skinUrl, int outputWidth = 160)
        {
            try
            {
                var bytes = await GetSkinBytesAsync(skinUrl);
                if (bytes == null || bytes.Length == 0) return null;
                using var ms = new MemoryStream(bytes);
                var ras = ms.AsRandomAccessStream();
                var decoder = await BitmapDecoder.CreateAsync(ras);
                double s = decoder.PixelWidth >= 64 ? 1.0 : (double)decoder.PixelWidth / 64.0;
                uint headX = (uint)(8 * s), headY = (uint)(8 * s), headW = (uint)(8 * s), headH = (uint)(8 * s);
                uint bodyX = (uint)(20 * s), bodyY = (uint)(20 * s), bodyW = (uint)(8 * s), bodyH = (uint)(12 * s);
                uint outHeadW = (uint)outputWidth;
                uint outHeadH = outHeadW; 
                uint outBodyW = outHeadW;
                uint outBodyH = (uint)(outHeadW * (12.0 / 8.0));
                uint outTotalH = outHeadH + outBodyH;
                var headTransform = new BitmapTransform
                {
                    Bounds = new BitmapBounds { X = headX, Y = headY, Width = headW, Height = headH },
                    ScaledWidth = outHeadW,
                    ScaledHeight = outHeadH,
                    InterpolationMode = BitmapInterpolationMode.Fant
                };
                var headPixelsInfo = await decoder.GetPixelDataAsync(decoder.BitmapPixelFormat, decoder.BitmapAlphaMode, headTransform, ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
                var headPixels = headPixelsInfo.DetachPixelData();
                var bodyTransform = new BitmapTransform
                {
                    Bounds = new BitmapBounds { X = bodyX, Y = bodyY, Width = bodyW, Height = bodyH },
                    ScaledWidth = outBodyW,
                    ScaledHeight = outBodyH,
                    InterpolationMode = BitmapInterpolationMode.Fant
                };
                var bodyPixelsInfo = await decoder.GetPixelDataAsync(decoder.BitmapPixelFormat, decoder.BitmapAlphaMode, bodyTransform, ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
                var bodyPixels = bodyPixelsInfo.DetachPixelData();
                int bytesPerPixel = 4;
                int totalPixels = (int)(outTotalH * outHeadW);
                var composed = new byte[totalPixels * bytesPerPixel];
                int rowWidthBytes = (int)(outHeadW * bytesPerPixel);
                for (int row = 0; row < (int)outHeadH; row++)
                {
                    int srcOffset = row * rowWidthBytes;
                    int dstOffset = row * rowWidthBytes;
                    System.Buffer.BlockCopy(headPixels, srcOffset, composed, dstOffset, rowWidthBytes);
                }
                for (int row = 0; row < (int)outBodyH; row++)
                {
                    int srcOffset = row * (int)(outBodyW * bytesPerPixel);
                    int dstOffset = ((int)outHeadH + row) * rowWidthBytes;
                    System.Buffer.BlockCopy(bodyPixels, srcOffset, composed, dstOffset, (int)(outBodyW * bytesPerPixel));
                }
                using var outStream = new InMemoryRandomAccessStream();
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outStream);
                encoder.SetPixelData(decoder.BitmapPixelFormat, decoder.BitmapAlphaMode, outHeadW, outTotalH, decoder.DpiX, decoder.DpiY, composed);
                await encoder.FlushAsync();
                outStream.Seek(0);
                byte[] pngBytes;
                try
                {
                    using (var stream = outStream.AsStreamForRead())
                    {
                        pngBytes = new byte[stream.Length];
                        await stream.ReadAsync(pngBytes, 0, pngBytes.Length);
                    }
                }
                catch
                {
                    return null;
                }
                var cached = SaveBytesToCache(pngBytes);
                if (!string.IsNullOrEmpty(cached)) return new BitmapImage(new Uri(cached));
                return BytesToBitmapImage(pngBytes);
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
                return await GetSkinImageAsync(avatarUrl);
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
                return await GetSkinImageAsync(avatarUrl);
            }
            catch
            {
                return null;
            }
        }
        public static async Task<BitmapImage?> GetBodyRenderAsync(string uuidOrUsername, int scale = 10)
        {
            try
            {
                var uuid = uuidOrUsername.Contains("-") || uuidOrUsername.Length == 32 
                    ? uuidOrUsername 
                    : await GetUuidFromUsernameAsync(uuidOrUsername);
                if (string.IsNullOrEmpty(uuid)) return null;
                var renderUrl = $"https://crafatar.com/renders/body/{uuid}?scale={scale}&overlay&default=true";
                return await GetSkinImageAsync(renderUrl);
            }
            catch
            {
                return null;
            }
        }
        private static BitmapImage BytesToBitmapImage(byte[] bytes)
        {
            try
            {
                var path = SaveBytesToCache(bytes);
                if (!string.IsNullOrEmpty(path))
                {
                    return new BitmapImage(new Uri(path));
                }
            }
            catch { }
            var bitmap = new BitmapImage();
            using var ms = new MemoryStream(bytes);
            var ras = ms.AsRandomAccessStream();
            bitmap.SetSource(ras);
            return bitmap;
        }
        private static string SaveBytesToCache(byte[] bytes)
        {
            try
            {
                using var sha1 = SHA1.Create();
                var hash = BitConverter.ToString(sha1.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                var fileName = hash + ".png";
                var path = Path.Combine(_cacheDir, fileName);
                if (!File.Exists(path)) File.WriteAllBytes(path, bytes);
                return path;
            }
            catch
            {
                return string.Empty;
            }
        }
        private static BitmapImage? ExtractHead(byte[] skinBytes)
        {
            try
            {
                return BytesToBitmapImage(skinBytes);
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