using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using OrangLauncher.Models;
namespace OrangLauncher.Managers
{
    public static class InstanceExporter
    {
        public static readonly HashSet<string> DefaultSkip = new(StringComparer.OrdinalIgnoreCase)
        {
            ".minecraft/versions", ".minecraft/libraries", ".minecraft/assets", ".minecraft/logs",
            ".minecraft/crash-reports", ".minecraft/runtime", ".minecraft/saves", ".minecraft/screenshots",
            ".minecraft/.fabric", ".minecraft/.mixin.out", ".minecraft/.cache", ".minecraft/usercache.json",
            ".minecraft/usernamecache.json", ".minecraft/servers.dat_old", ".minecraft/options.txt"
        };
        private static readonly Dictionary<string, string> LoaderKeys = new()
        {
            ["fabric"] = "fabric-loader",
            ["quilt"] = "quilt-loader",
            ["forge"] = "forge",
            ["neoforge"] = "neoforge"
        };
        public static bool IsSkipped(string rel) => DefaultSkip.Contains(rel) || DefaultSkip.Any(d => rel.StartsWith(d + "/", StringComparison.OrdinalIgnoreCase));
        public static async Task<string> ExportAsync(MinecraftInstance instance, string outPath, string format, IReadOnlyCollection<string> selected,
            Action<string>? log = null, Action<int, int, string>? progress = null)
        {
            var baseDir = instance.BasePath;
            const string mcRoot = ".minecraft/";
            var files = selected
                .Select(rel => (Rel: rel.Replace('\\', '/'), Path: Path.Combine(baseDir, rel.Replace('/', Path.DirectorySeparatorChar))))
                .Where(f => File.Exists(f.Path))
                .ToList();
            var instanceJson = JsonSerializer.Serialize(instance, new JsonSerializerOptions { WriteIndented = true });
            if (File.Exists(outPath)) File.Delete(outPath);
            if (format == "zip")
            {
                using var zf = ZipFile.Open(outPath, ZipArchiveMode.Create);
                var e = zf.CreateEntry("instance.json");
                await using (var w = new StreamWriter(e.Open())) await w.WriteAsync(instanceJson);
                for (int i = 0; i < files.Count; i++)
                {
                    zf.CreateEntryFromFile(files[i].Path, files[i].Rel, CompressionLevel.Optimal);
                    progress?.Invoke(i + 1, files.Count, files[i].Rel);
                }
                return outPath;
            }
            var candidates = files.Where(f => f.Rel.StartsWith(mcRoot) &&
                (f.Rel.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) || f.Rel.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) &&
                new[] { "mods/", "resourcepacks/", "shaderpacks/", "datapacks/" }.Any(d => f.Rel[mcRoot.Length..].StartsWith(d))).ToList();
            var bySha1 = new Dictionary<string, (string Rel, string Path)>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in candidates)
            {
                try { bySha1[ModrinthClient.Sha1(c.Path)] = c; } catch { }
            }
            log?.Invoke($"[Export] Looking up {bySha1.Count} files on Modrinth");
            var found = await ModrinthClient.LookupHashesAsync(bySha1.Keys);
            var remoteFiles = new JsonArray();
            var remoteRels = new HashSet<string>();
            foreach (var (sha1, version) in found)
            {
                if (!bySha1.TryGetValue(sha1, out var local)) continue;
                var vf = version.Files.FirstOrDefault(f => string.Equals(f.Hashes.Sha1, sha1, StringComparison.OrdinalIgnoreCase));
                if (vf == null) continue;
                remoteRels.Add(local.Rel);
                var rel = local.Rel[mcRoot.Length..];
                remoteFiles.Add(new JsonObject
                {
                    ["path"] = rel,
                    ["hashes"] = new JsonObject { ["sha1"] = sha1, ["sha512"] = vf.Hashes.Sha512 ?? ModrinthClient.Sha512(local.Path) },
                    ["env"] = new JsonObject { ["client"] = "required", ["server"] = rel.StartsWith("mods/") ? "required" : "unsupported" },
                    ["downloads"] = new JsonArray(vf.Url),
                    ["fileSize"] = new FileInfo(local.Path).Length
                });
            }
            var deps = new JsonObject { ["minecraft"] = instance.Version };
            if (LoaderKeys.TryGetValue(instance.LoaderKey, out var loaderKey))
            {
                if (!string.IsNullOrEmpty(instance.LoaderVersion)) deps[loaderKey] = instance.LoaderVersion;
                else if (!string.IsNullOrEmpty(instance.InstalledVersionName))
                {
                    var m = Regex.Match(instance.InstalledVersionName.Replace(instance.Version, ""), @"(\d+\.\d+\.\d+(?:[-.][\w.]+)?)");
                    if (m.Success) deps[loaderKey] = m.Groups[1].Value;
                }
            }
            var index = new JsonObject
            {
                ["formatVersion"] = 1,
                ["game"] = "minecraft",
                ["versionId"] = "1.0.0",
                ["name"] = instance.Name,
                ["summary"] = $"Exported from OrangLauncher {UpdateManager.GetCurrentVersionString().TrimStart('v')}",
                ["files"] = remoteFiles,
                ["dependencies"] = deps
            };
            var jsonOptions = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            using (var zf = ZipFile.Open(outPath, ZipArchiveMode.Create))
            {
                if (format == "orangpack")
                {
                    var orang = new JsonObject
                    {
                        ["launcher"] = UpdateManager.GetCurrentVersionString().TrimStart('v'),
                        ["instance"] = new JsonObject
                        {
                            ["name"] = instance.Name,
                            ["version"] = instance.Version,
                            ["mod_loader"] = instance.LoaderKey,
                            ["loader_version"] = instance.LoaderVersion,
                            ["ram"] = instance.Ram,
                            ["env_vars"] = instance.EnvVars,
                            ["options"] = JsonSerializer.SerializeToNode(instance.Options)
                        }
                    };
                    index["orangpack"] = orang;
                    var oe = zf.CreateEntry(ModpackImporter.OrangpackIndex);
                    await using (var w = new StreamWriter(oe.Open())) await w.WriteAsync(index.ToJsonString(jsonOptions));
                }
                var me = zf.CreateEntry("modrinth.index.json");
                await using (var w = new StreamWriter(me.Open())) await w.WriteAsync(index.ToJsonString(jsonOptions));
                if (!string.IsNullOrEmpty(instance.Icon) && File.Exists(instance.Icon))
                    zf.CreateEntryFromFile(instance.Icon, "icon" + Path.GetExtension(instance.Icon).ToLowerInvariant());
                for (int i = 0; i < files.Count; i++)
                {
                    var (rel, path) = files[i];
                    if (remoteRels.Contains(rel)) continue;
                    if (rel.StartsWith(mcRoot)) zf.CreateEntryFromFile(path, "overrides/" + rel[mcRoot.Length..], CompressionLevel.Optimal);
                    else if (format == "orangpack") zf.CreateEntryFromFile(path, "instance/" + rel, CompressionLevel.Optimal);
                    progress?.Invoke(i + 1, files.Count, rel);
                }
            }
            log?.Invoke($"[Export] {remoteFiles.Count} files referenced remotely, rest packed as overrides");
            return outPath;
        }
    }
}