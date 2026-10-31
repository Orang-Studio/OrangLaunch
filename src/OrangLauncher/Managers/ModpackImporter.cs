using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using OrangLauncher.Models;
namespace OrangLauncher.Managers
{
    public class ModpackMeta
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("version_id")] public string? VersionId { get; set; }
        [JsonPropertyName("summary")] public string? Summary { get; set; }
        [JsonPropertyName("imported")] public string? Imported { get; set; }
        [JsonPropertyName("source_file")] public string? SourceFile { get; set; }
        [JsonPropertyName("file_count")] public int FileCount { get; set; }
        [JsonPropertyName("managed_files")] public List<string> ManagedFiles { get; set; } = new();
        [JsonPropertyName("modrinth_project_id")] public string? ModrinthProjectId { get; set; }
        [JsonPropertyName("modrinth_version_id")] public string? ModrinthVersionId { get; set; }
        [JsonPropertyName("modrinth_version_number")] public string? ModrinthVersionNumber { get; set; }
        [JsonPropertyName("modrinth_slug")] public string? ModrinthSlug { get; set; }
        [JsonPropertyName("modrinth_title")] public string? ModrinthTitle { get; set; }
        public static string PathFor(MinecraftInstance instance) => Path.Combine(instance.BasePath, "modpack.json");
        public static ModpackMeta? Load(MinecraftInstance instance)
        {
            try
            {
                var p = PathFor(instance);
                return File.Exists(p) ? JsonSerializer.Deserialize<ModpackMeta>(File.ReadAllText(p)) : null;
            }
            catch { return null; }
        }
        public void Save(MinecraftInstance instance)
        {
            File.WriteAllText(PathFor(instance), JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        }
    }
    public class ModpackImporter
    {
        public const string OrangpackExt = ".orangpack";
        public const string OrangpackIndex = "orangpack.json";
        public event Action<string, double>? ProgressChanged;
        public event Action<string>? Log;
        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromMinutes(5) };
        private readonly List<string> _failed = new();
        public ModpackImporter()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                $"OrangLauncher/{UpdateManager.GetCurrentVersionString().TrimStart('v')} (github.com/Orang-Studio/OrangLaunch)");
        }
        private static string T(string key, string def, params (string, object?)[] args) => LocalizationManager.T(key, def, args);
        private void Report(string text, double pct) => ProgressChanged?.Invoke(text, pct);
        private static JsonNode? ReadIndex(ZipArchive zip)
        {
            var entry = zip.GetEntry(OrangpackIndex) ?? zip.GetEntry("modrinth.index.json");
            if (entry == null) return null;
            using var s = entry.Open();
            return JsonNode.Parse(s);
        }
        public static (string Loader, string Version) DetectLoader(JsonNode index)
        {
            var deps = index["dependencies"] as JsonObject;
            if (deps != null)
            {
                foreach (var (key, loader) in new[] { ("fabric-loader", "fabric"), ("quilt-loader", "quilt"), ("neoforge", "neoforge"), ("forge", "forge") })
                    if (deps[key] is JsonValue v && v.TryGetValue<string>(out var s)) return (loader, s);
            }
            if (index["files"] is JsonArray files)
            {
                foreach (var f in files)
                {
                    var p = (f?["path"]?.GetValue<string>() ?? "").ToLowerInvariant();
                    if (p.Contains("fabric")) return ("fabric", "");
                    if (p.Contains("quilt")) return ("quilt", "");
                    if (p.Contains("neoforge")) return ("neoforge", "");
                    if (p.Contains("forge")) return ("forge", "");
                }
            }
            return ("vanilla", "");
        }
        public async Task<(bool Success, string Message, string? InstanceId)> ImportMrPackAsync(string mrpackPath)
        {
            _failed.Clear();
            try
            {
                var file = new FileInfo(mrpackPath);
                if (!file.Exists) return (false, T("QT_ERR_FILE_NOT_FOUND", "File not found: {path}", ("path", mrpackPath)), null);
                if (file.Length == 0) return (false, T("QT_ERR_MRPACK_EMPTY", "The mrpack file is empty (0 bytes)"), null);
                ZipArchive zip;
                try { zip = ZipFile.OpenRead(mrpackPath); }
                catch { return (false, T("QT_ERR_MRPACK_INVALID", "The selected file is not a valid .mrpack (zip) file.\n\nFile: {name}\nSize: {size} bytes\n\nMake sure the file downloaded completely.", ("name", file.Name), ("size", file.Length)), null); }
                using (zip)
                {
                    Report(T("QT_IMPORTING_MODPACK", "Importing modpack..."), 0);
                    var index = ReadIndex(zip);
                    if (index == null) return (false, "Invalid modpack: modrinth.index.json not found", null);
                    var gameVersion = index["dependencies"]?["minecraft"]?.GetValue<string>() ?? "";
                    if (string.IsNullOrEmpty(gameVersion)) return (false, "Invalid modpack: no Minecraft version found in modrinth.index.json", null);
                    var packName = index["name"]?.GetValue<string>() ?? Path.GetFileNameWithoutExtension(mrpackPath);
                    var (loader, loaderVersion) = DetectLoader(index);
                    var instance = InstanceManager.Instance.CreateInstance(packName, gameVersion, loader, "4096", loaderVersion)
                        ?? throw new Exception(T("QT_ERR_CREATE_INSTANCE", "Failed to create instance"));
                    InstanceManager.Instance.SetSelectedInstance(instance.InstanceId);
                    await ApplyAsync(zip, index, instance, mrpackPath);
                    try { SharingManager.Apply(instance); } catch { }
                    InstanceManager.Instance.SaveInstances();
                    var note = "";
                    if (_failed.Count > 0)
                    {
                        note = "\n\n" + T("QT_FILES_NOT_DOWNLOADED", "{n} file(s) could not be downloaded", ("n", _failed.Count)) + ":\n" + string.Join("\n", _failed.Take(8));
                        if (_failed.Count > 8) note += "\n" + T("QT_AND_N_MORE", "... and {n} more", ("n", _failed.Count - 8));
                    }
                    return (true, T("QT_IMPORT_SUCCESS", "Successfully imported {name} (Minecraft {version}, {loader})", ("name", packName), ("version", gameVersion), ("loader", loader)) + note, instance.InstanceId);
                }
            }
            catch (Exception ex)
            {
                return (false, T("QT_ERR_IMPORT_MODPACK", "Error importing modpack: {error}", ("error", ex.Message)), null);
            }
        }
        public async Task<(bool Success, string Message)> ApplyToInstanceAsync(string packPath, MinecraftInstance instance, bool removeManaged)
        {
            _failed.Clear();
            try
            {
                using var zip = ZipFile.OpenRead(packPath);
                var index = ReadIndex(zip);
                if (index == null) return (false, "Invalid modpack: modrinth.index.json not found");
                if (removeManaged)
                {
                    var meta = ModpackMeta.Load(instance);
                    foreach (var rel in meta?.ManagedFiles ?? new())
                    {
                        if (!rel.StartsWith("mods/")) continue;
                        var target = ResolveInside(instance.MinecraftDir, rel);
                        try { if (target != null && File.Exists(target)) File.Delete(target); } catch { }
                    }
                }
                var gameVersion = index["dependencies"]?["minecraft"]?.GetValue<string>() ?? instance.Version;
                var (loader, loaderVersion) = DetectLoader(index);
                bool changed = instance.Version != gameVersion || instance.LoaderKey != loader || (instance.LoaderVersion ?? "") != loaderVersion;
                instance.Version = gameVersion;
                instance.ModLoader = loader;
                instance.LoaderVersion = loaderVersion;
                if (changed) instance.InstalledVersionName = null;
                await ApplyAsync(zip, index, instance, packPath);
                InstanceManager.Instance.SaveInstances();
                var note = _failed.Count > 0 ? "\n\n" + T("QT_FILES_NOT_DOWNLOADED", "{n} file(s) could not be downloaded", ("n", _failed.Count)) + "." : "";
                return (true, T("QT_PACK_APPLIED", "Applied {name} {version} to '{instance}'.",
                    ("name", index["name"]?.GetValue<string>() ?? Path.GetFileName(packPath)),
                    ("version", index["versionId"]?.GetValue<string>() ?? ""),
                    ("instance", instance.Name)) + note);
            }
            catch (Exception ex)
            {
                return (false, T("QT_ERR_APPLY_MODPACK", "Error applying modpack: {error}", ("error", ex.Message)));
            }
        }
        private async Task ApplyAsync(ZipArchive zip, JsonNode index, MinecraftInstance instance, string sourcePath)
        {
            Directory.CreateDirectory(instance.MinecraftDir);
            var files = (index["files"] as JsonArray)?.Where(f => f != null).Select(f => f!).ToList() ?? new();
            int done = 0;
            using var gate = new SemaphoreSlim(6);
            var tasks = files.Select(async f =>
            {
                await gate.WaitAsync();
                try
                {
                    var path = (f["path"]?.GetValue<string>() ?? "").Replace('\\', '/').TrimStart('/');
                    if (string.IsNullOrEmpty(path)) return;
                    if (f["env"]?["client"]?.GetValue<string>() is string client && client.Equals("unsupported", StringComparison.OrdinalIgnoreCase)) return;
                    var dest = ResolveInside(instance.MinecraftDir, path);
                    if (dest == null) return;
                    var sha1 = f["hashes"]?["sha1"]?.GetValue<string>();
                    if (File.Exists(dest) && sha1 != null && ModrinthClient.Sha1(dest).Equals(sha1, StringComparison.OrdinalIgnoreCase)) return;
                    var urls = (f["downloads"] as JsonArray)?.Select(u => u?.GetValue<string>()).Where(u => !string.IsNullOrEmpty(u)).ToList() ?? new();
                    bool ok = false;
                    foreach (var url in urls)
                    {
                        try
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                            var bytes = await _httpClient.GetByteArrayAsync(url!);
                            await File.WriteAllBytesAsync(dest, bytes);
                            ok = true;
                            break;
                        }
                        catch { }
                    }
                    if (!ok) lock (_failed) _failed.Add(path);
                }
                finally
                {
                    var n = Interlocked.Increment(ref done);
                    Report(T("QT_DOWNLOADING_N", "Downloading files {done}/{total}", ("done", n), ("total", files.Count)), 5 + n * 80.0 / Math.Max(1, files.Count));
                    gate.Release();
                }
            });
            await Task.WhenAll(tasks);
            Report(T("QT_EXTRACTING_OVERRIDES", "Extracting overrides..."), 88);
            foreach (var entry in zip.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;
                foreach (var prefix in new[] { "overrides/", "client-overrides/" })
                {
                    if (!entry.FullName.StartsWith(prefix)) continue;
                    var dest = ResolveInside(instance.MinecraftDir, entry.FullName[prefix.Length..]);
                    if (dest == null) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    entry.ExtractToFile(dest, true);
                }
            }
            await SaveMetadataAsync(zip, index, instance, sourcePath, files);
            ApplyOrangpackSettings(zip, index, instance);
            Report(T("QT_READY", "Ready"), 100);
        }
        private async Task SaveMetadataAsync(ZipArchive zip, JsonNode index, MinecraftInstance instance, string sourcePath, List<JsonNode> files)
        {
            var meta = new ModpackMeta
            {
                Name = index["name"]?.GetValue<string>(),
                VersionId = index["versionId"]?.GetValue<string>(),
                Summary = index["summary"]?.GetValue<string>(),
                Imported = DateTime.Now.ToString("s"),
                SourceFile = sourcePath,
                FileCount = files.Count,
                ManagedFiles = files.Select(f => f["path"]?.GetValue<string>() ?? "").Where(p => p.Length > 0).ToList()
            };
            try
            {
                var sha1 = ModrinthClient.Sha1(sourcePath);
                var found = await ModrinthClient.LookupHashesAsync(new[] { sha1 });
                if (found.TryGetValue(sha1, out var version))
                {
                    meta.ModrinthProjectId = version.ProjectId;
                    meta.ModrinthVersionId = version.Id;
                    meta.ModrinthVersionNumber = version.VersionNumber;
                    var project = await ModrinthClient.GetProjectAsync(version.ProjectId);
                    if (project != null)
                    {
                        meta.ModrinthSlug = project.Slug;
                        meta.ModrinthTitle = project.Title;
                        if (!string.IsNullOrEmpty(project.IconUrl) && string.IsNullOrEmpty(instance.Icon))
                        {
                            var bytes = await ModrinthClient.GetIconAsync(project.IconUrl);
                            if (bytes != null)
                            {
                                var iconPath = Path.Combine(instance.BasePath, "icon.png");
                                await File.WriteAllBytesAsync(iconPath, UiKit.ToPng(bytes) ?? bytes);
                                instance.Icon = iconPath;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { Log?.Invoke($"[MRPACK] hash lookup failed: {ex.Message}"); }
            if (string.IsNullOrEmpty(instance.Icon))
            {
                foreach (var candidate in new[] { "icon.png", "icon.jpg", "icon.jpeg", "pack.png" })
                {
                    var e = zip.GetEntry(candidate);
                    if (e == null) continue;
                    var iconPath = Path.Combine(instance.BasePath, "icon.png");
                    e.ExtractToFile(iconPath, true);
                    instance.Icon = iconPath;
                    break;
                }
            }
            try { meta.Save(instance); } catch { }
        }
        private static void ApplyOrangpackSettings(ZipArchive zip, JsonNode index, MinecraftInstance instance)
        {
            if (index["orangpack"]?["instance"] is not JsonObject inst) return;
            try
            {
                if (inst["ram"]?.GetValue<string>() is string ram && ram.Length > 0)
                    instance.Ram = ram.EndsWith("G", StringComparison.OrdinalIgnoreCase) && int.TryParse(ram[..^1], out var gb) ? (gb * 1024).ToString() : ram;
                if (inst["env_vars"]?.GetValue<string>() is string env && env.Length > 0) instance.EnvVars = env;
                if (inst["options"] is JsonObject opts)
                {
                    var parsed = opts.Deserialize<InstanceOptions>();
                    if (parsed != null)
                    {
                        parsed.PreLaunchCmd = instance.Options.PreLaunchCmd;
                        parsed.WrapperCmd = instance.Options.WrapperCmd;
                        parsed.PostExitCmd = instance.Options.PostExitCmd;
                        instance.Options = parsed;
                    }
                }
                else if (inst["opts"] is JsonObject pyOpts)
                {
                    if (pyOpts["jvm_args"]?.GetValue<string>() is string jvm) instance.Options.JvmArgs = jvm;
                    if (pyOpts["min_ram_mb"] is JsonValue mr && mr.TryGetValue<int>(out var mrv)) instance.Options.MinRamMb = mrv;
                    if (pyOpts["res_width"] is JsonValue rw && rw.TryGetValue<int>(out var rwv)) instance.Options.ResWidth = rwv;
                    if (pyOpts["res_height"] is JsonValue rh && rh.TryGetValue<int>(out var rhv)) instance.Options.ResHeight = rhv;
                    if (pyOpts["fullscreen"] is JsonValue fs && fs.TryGetValue<bool>(out var fsv)) instance.Options.Fullscreen = fsv;
                    if (pyOpts["anti_ban"] is JsonValue ab && ab.TryGetValue<bool>(out var abv)) instance.Options.AntiBan = abv;
                    if (pyOpts["demo"] is JsonValue dm && dm.TryGetValue<bool>(out var dmv)) instance.Options.Demo = dmv;
                    if (pyOpts["legacy_fixes"] is JsonValue lf && lf.TryGetValue<bool>(out var lfv)) instance.Options.LegacyFixes = lfv;
                }
                foreach (var entry in zip.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name) || !entry.FullName.StartsWith("instance/") || entry.Name == "instance.json") continue;
                    var dest = ResolveInside(instance.BasePath, entry.FullName["instance/".Length..]);
                    if (dest == null) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    entry.ExtractToFile(dest, true);
                }
                var icon = Path.Combine(instance.BasePath, "icon.png");
                if (File.Exists(icon)) instance.Icon = icon;
            }
            catch { }
        }
        public async Task<(bool Success, string Message, string? InstanceId)> ImportCurseForgeAsync(string zipPath)
        {
            try
            {
                using var zip = ZipFile.OpenRead(zipPath);
                var manifestEntry = zip.GetEntry("manifest.json");
                if (manifestEntry == null) return (false, "manifest.json not found - this does not appear to be a CurseForge modpack.", null);
                JsonNode? manifest;
                using (var s = manifestEntry.Open()) manifest = JsonNode.Parse(s);
                if (manifest?["manifestType"]?.GetValue<string>() != "minecraftModpack")
                    return (false, $"Unknown manifest type: {manifest?["manifestType"]}", null);
                var mcVersion = manifest["minecraft"]?["version"]?.GetValue<string>() ?? "";
                string loaderId = "";
                if (manifest["minecraft"]?["modLoaders"] is JsonArray loaders)
                    foreach (var ml in loaders)
                        if (ml?["primary"]?.GetValue<bool>() == true) { loaderId = ml["id"]?.GetValue<string>() ?? ""; break; }
                string loader = "vanilla", loaderVersion = "";
                foreach (var name in new[] { "neoforge", "forge", "fabric", "quilt" })
                {
                    if (loaderId.StartsWith(name + "-")) { loader = name; loaderVersion = loaderId[(name.Length + 1)..]; break; }
                }
                var packName = manifest["name"]?.GetValue<string>() ?? Path.GetFileNameWithoutExtension(zipPath);
                var instance = InstanceManager.Instance.CreateInstance(packName, mcVersion, loader, "4096", loaderVersion)
                    ?? throw new Exception(T("QT_ERR_CREATE_INSTANCE", "Failed to create instance"));
                var overrides = (manifest["overrides"]?.GetValue<string>() ?? "overrides").TrimEnd('/') + "/";
                int copied = 0;
                foreach (var entry in zip.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name) || !entry.FullName.StartsWith(overrides)) continue;
                    var dest = ResolveInside(instance.MinecraftDir, entry.FullName[overrides.Length..]);
                    if (dest == null) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    entry.ExtractToFile(dest, true);
                    copied++;
                }
                var files = (manifest["files"] as JsonArray)?.Where(f => f != null).Select(f => f!).ToList() ?? new();
                var required = files.Where(f => f["required"]?.GetValue<bool>() != false).ToList();
                var optional = files.Count - required.Count;
                var apiKey = Environment.GetEnvironmentVariable("CURSEFORGE_API_KEY") ?? "";
                int downloaded = 0;
                var failed = new List<string>();
                foreach (var f in required)
                {
                    var pid = f["projectID"]?.ToString();
                    var fid = f["fileID"]?.ToString();
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        try
                        {
                            using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.curseforge.com/v1/mods/{pid}/files/{fid}/download-url");
                            req.Headers.Add("x-api-key", apiKey);
                            var resp = await _httpClient.SendAsync(req);
                            if (resp.IsSuccessStatusCode)
                            {
                                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                                var url = doc.RootElement.GetProperty("data").GetString();
                                if (!string.IsNullOrEmpty(url))
                                {
                                    var name = Uri.UnescapeDataString(url.Split('/').Last().Split('?')[0]);
                                    Directory.CreateDirectory(instance.ModsDir);
                                    await File.WriteAllBytesAsync(Path.Combine(instance.ModsDir, name), await _httpClient.GetByteArrayAsync(url));
                                    downloaded++;
                                    continue;
                                }
                            }
                        }
                        catch (Exception ex) { Log?.Invoke($"[CurseForge] Failed to download {pid}/{fid}: {ex.Message}"); }
                    }
                    failed.Add($"{pid}/{fid}");
                }
                InstanceManager.Instance.SaveInstances();
                var parts = new List<string>
                {
                    T("QT_CF_CREATED", "Created instance: {name}", ("name", instance.Name)),
                    $"Minecraft {mcVersion} + {loader}" + (loaderVersion.Length > 0 ? $" {loaderVersion}" : ""),
                    T("QT_CF_OVERRIDES", "Override files copied: {n}", ("n", copied)),
                    T("QT_CF_MODS", "Mods downloaded: {done}/{total}", ("done", downloaded), ("total", required.Count))
                };
                if (failed.Count > 0)
                    parts.Add("\n" + T("QT_CF_MANUAL", "{n} mod(s) need manual download (no API key or download failed).\nSet CURSEFORGE_API_KEY env var, or download mods manually from CurseForge\nand place them in: {path}", ("n", failed.Count), ("path", instance.ModsDir)));
                if (optional > 0) parts.Add(T("QT_CF_OPTIONAL", "Optional mods not included: {n}", ("n", optional)));
                return (true, string.Join("\n", parts), instance.InstanceId);
            }
            catch (Exception ex)
            {
                return (false, T("QT_IMPORT_FAILED", "Import failed") + $": {ex.Message}", null);
            }
        }
        private static string? ResolveInside(string baseDir, string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath) || Path.IsPathRooted(relativePath)) return null;
            var baseFull = Path.GetFullPath(baseDir + Path.DirectorySeparatorChar);
            var destFull = Path.GetFullPath(Path.Combine(baseFull, relativePath));
            return destFull.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase) ? destFull : null;
        }
    }
}
