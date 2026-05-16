using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
namespace OrangLauncher.Managers
{
    public class ModpackImporter
    {
        public event Action<string, double>? ProgressChanged;
        private readonly HttpClient _httpClient = new();
        public ModpackImporter()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "OrangLauncher/6.0.0 (github.com/Orang-Studio/OrangLaunch)");
        }
        public async Task<(bool Success, string Message, string? InstanceId)> ImportMrPackAsync(string mrpackPath)
        {
            try
            {
                ProgressChanged?.Invoke("Reading modpack...", 0);
                using var archive = ZipFile.OpenRead(mrpackPath);
                var indexEntry = archive.GetEntry("modrinth.index.json");
                if (indexEntry == null)
                    return (false, "Invalid mrpack: missing modrinth.index.json", null);
                using var indexStream = indexEntry.Open();
                using var reader = new StreamReader(indexStream);
                var indexJson = await reader.ReadToEndAsync();
                using var doc = JsonDocument.Parse(indexJson);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    return (false, "Invalid modrinth.index.json format", null);
                ProgressChanged?.Invoke("Creating instance...", 10);
                string modLoader = "vanilla";
                string? modLoaderVersion = null;
                string mcVersion = "1.21.11";
                string packName = Path.GetFileNameWithoutExtension(mrpackPath);
                if (root.TryGetProperty("name", out var nameElem) && nameElem.ValueKind == JsonValueKind.String)
                    packName = nameElem.GetString() ?? packName;
                if (root.TryGetProperty("dependencies", out var deps) && deps.ValueKind == JsonValueKind.Object)
                {
                    if (deps.TryGetProperty("minecraft", out var mcv) && mcv.ValueKind == JsonValueKind.String)
                        mcVersion = mcv.GetString() ?? mcVersion;
                    if (deps.TryGetProperty("fabric-loader", out var fver) && fver.ValueKind == JsonValueKind.String)
                    {
                        modLoader = "fabric"; modLoaderVersion = fver.GetString();
                    }
                    else if (deps.TryGetProperty("quilt-loader", out var qver) && qver.ValueKind == JsonValueKind.String)
                    {
                        modLoader = "quilt"; modLoaderVersion = qver.GetString();
                    }
                    else if (deps.TryGetProperty("forge", out var forg) && forg.ValueKind == JsonValueKind.String)
                    {
                        modLoader = "forge"; modLoaderVersion = forg.GetString();
                    }
                    else if (deps.TryGetProperty("neoforge", out var nf) && nf.ValueKind == JsonValueKind.String)
                    {
                        modLoader = "neoforge"; modLoaderVersion = nf.GetString();
                    }
                }
                if (root.TryGetProperty("files", out var filesElem) && filesElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var fileElem in filesElem.EnumerateArray())
                    {
                        if (fileElem.ValueKind != JsonValueKind.Object) continue;
                        if (fileElem.TryGetProperty("path", out var pathElem) && pathElem.ValueKind == JsonValueKind.String)
                        {
                            var p = pathElem.GetString() ?? "";
                            var lp = p.ToLowerInvariant();
                            if (lp.Contains("fabric")) { modLoader = "fabric"; break; }
                            if (lp.Contains("quilt")) { modLoader = "quilt"; break; }
                            if (lp.Contains("forge")) { modLoader = "forge"; break; }
                            if (lp.Contains("neoforge")) { modLoader = "neoforge"; break; }
                        }
                    }
                }
                var instance = InstanceManager.Instance.CreateInstance(
                    packName,
                    mcVersion,
                    modLoader,
                    "4096"
                );
                if (instance == null)
                    return (false, "Failed to create instance", null);
                string installedVersionName = "";
                try
                {
                    if (!string.IsNullOrEmpty(modLoader) && !modLoader.Equals("vanilla", StringComparison.OrdinalIgnoreCase))
                    {
                        var loaderVer = modLoaderVersion;
                        var globalMcDir = PlatformPaths.GetMinecraftDir();
                        installedVersionName = await ModLoaderInstaller.InstallLoaderAsync(globalMcDir, mcVersion, modLoader, loaderVer);
                        try { instance.InstalledVersionName = installedVersionName; } catch { }
                        ProgressChanged?.Invoke($"Installed {modLoader} {installedVersionName}", 15);
                    }
                }
                catch (Exception loaderEx)
                {
                    ProgressChanged?.Invoke($"Warning: failed to install {modLoader}: {loaderEx.Message}", 15);
                }
                if (string.IsNullOrEmpty(instance.InstalledVersionName) && !string.IsNullOrEmpty(modLoaderVersion))
                {
                    try { instance.InstalledVersionName = modLoaderVersion; } catch { }
                }
                ProgressChanged?.Invoke("Extracting overrides...", 20);
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.StartsWith("overrides/") && !string.IsNullOrEmpty(entry.Name))
                    {
                        var relativePath = entry.FullName.Substring("overrides/".Length);
                        var destPath = Path.Combine(instance.MinecraftDir, relativePath);
                        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                        entry.ExtractToFile(destPath, true);
                    }
                    else if (entry.FullName.StartsWith("client-overrides/") && !string.IsNullOrEmpty(entry.Name))
                    {
                        var relativePath = entry.FullName.Substring("client-overrides/".Length);
                        var destPath = Path.Combine(instance.MinecraftDir, relativePath);
                        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                        entry.ExtractToFile(destPath, true);
                    }
                }
                ProgressChanged?.Invoke("Downloading mods...", 30);
                if (root.TryGetProperty("files", out var filesElement) && filesElement.ValueKind == JsonValueKind.Array)
                {
                    var fileList = new List<JsonElement>();
                    foreach (var fe in filesElement.EnumerateArray()) fileList.Add(fe);
                    int total = fileList.Count;
                    int current = 0;
                    foreach (var fileElem in fileList)
                    {
                        current++;
                        string pathStr = "";
                        if (fileElem.ValueKind == JsonValueKind.Object && fileElem.TryGetProperty("path", out var pe) && pe.ValueKind == JsonValueKind.String)
                            pathStr = pe.GetString() ?? "";
                        double progress = 30 + (current / (double)total) * 60;
                        ProgressChanged?.Invoke($"Processing {pathStr}...", progress);
                        if (fileElem.ValueKind == JsonValueKind.Object && fileElem.TryGetProperty("env", out var env) && env.ValueKind == JsonValueKind.Object)
                        {
                            if (env.TryGetProperty("client", out var clientEnv) && clientEnv.ValueKind == JsonValueKind.String)
                            {
                                if (string.Equals(clientEnv.GetString(), "unsupported", StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }
                            }
                        }
                        var urls = new List<string>();
                        if (fileElem.ValueKind == JsonValueKind.Object && fileElem.TryGetProperty("downloads", out var downloadsElem) && downloadsElem.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var d in downloadsElem.EnumerateArray())
                            {
                                if (d.ValueKind == JsonValueKind.String) urls.Add(d.GetString() ?? "");
                                else if (d.ValueKind == JsonValueKind.Object && d.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String) urls.Add(u.GetString() ?? "");
                            }
                        }
                        if (urls.Count == 0 && fileElem.ValueKind == JsonValueKind.Object && fileElem.TryGetProperty("url", out var topUrl) && topUrl.ValueKind == JsonValueKind.String)
                        {
                            urls.Add(topUrl.GetString() ?? "");
                        }
                        if (urls.Count == 0)
                        {
                            continue;
                        }
                        var destPath = Path.Combine(instance.MinecraftDir, pathStr ?? "");
                        if (pathStr.StartsWith("overrides/", StringComparison.OrdinalIgnoreCase))
                        {
                            var rel = pathStr.Substring("overrides/".Length);
                            destPath = Path.Combine(instance.MinecraftDir, rel);
                        }
                        try { Directory.CreateDirectory(Path.GetDirectoryName(destPath)!); } catch { }
                        bool downloaded = false;
                        foreach (var downloadUrl in urls)
                        {
                            try
                            {
                                var resp = await _httpClient.GetAsync(downloadUrl);
                                if (resp.IsSuccessStatusCode)
                                {
                                    var content = await resp.Content.ReadAsByteArrayAsync();
                                    await File.WriteAllBytesAsync(destPath, content);
                                    downloaded = true;
                                    break;
                                }
                            }
                            catch { }
                        }
                        if (!downloaded)
                            ProgressChanged?.Invoke($"Warning: failed to download {pathStr}", progress);
                    }
                }
                ProgressChanged?.Invoke("Complete!", 100);
                InstanceManager.Instance.SaveInstances();
                var displayName = packName;
                if (root.TryGetProperty("name", out var nm) && nm.ValueKind == JsonValueKind.String) displayName = nm.GetString() ?? displayName;
                return (true, $"Successfully imported modpack '{displayName}'", instance.InstanceId);
            }
            catch (Exception ex)
            {
                return (false, $"Import failed: {ex.Message}", null);
            }
        }
        private class ModrinthIndex
        {
            public int FormatVersion { get; set; }
            public string? Name { get; set; }
            public string? VersionId { get; set; }
            public System.Collections.Generic.Dictionary<string, string>? Dependencies { get; set; }
            public ModrinthFile[]? Files { get; set; }
        }
        private class ModrinthFile
        {
            public string Path { get; set; } = "";
            public ModrinthHashes? Hashes { get; set; }
            public string[]? Downloads { get; set; }
            public System.Collections.Generic.Dictionary<string, string>? Env { get; set; }
            public long FileSize { get; set; }
        }
        private class ModrinthHashes
        {
            public string? Sha512 { get; set; }
            public string? Sha1 { get; set; }
        }
    }
}