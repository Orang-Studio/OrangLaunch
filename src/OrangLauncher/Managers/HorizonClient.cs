using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OrangLauncher.Managers;

public sealed record HorizonReleaseInfo(
    string Tag,
    string GithubTag,
    string Name,
    string AssetName,
    string DownloadUrl,
    string ChecksumsUrl,
    bool IsNightly,
    string PublishedAt);

public sealed class HorizonFileRef
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
    public string Signature { get; set; } = "";
}

public sealed class HorizonDependencyRef
{
    public string Id { get; set; } = "";
    public string Version { get; set; } = "";
    public string Sha256 { get; set; } = "";
}

public sealed class HorizonProjectVersion
{
    public string Version { get; set; } = "";
    public string Published { get; set; } = "";
    public string Changelog { get; set; } = "";
    public string Api { get; set; } = "";
    public string Side { get; set; } = "";
    public string Game { get; set; } = "26.3";
    public List<string> Capabilities { get; set; } = [];
    public List<HorizonDependencyRef> Requires { get; set; } = [];
    public HorizonFileRef File { get; set; } = new();
}

public sealed class HorizonProject
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "mod";
    public string Name { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Authors { get; set; } = [];
    public List<string> Owners { get; set; } = [];
    public string License { get; set; } = "";
    public string Source { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Key { get; set; } = "";
    public long Downloads { get; set; }
    public long Stars { get; set; }
    public string Updated { get; set; } = "";
    public List<string> Categories { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public List<string> Screenshots { get; set; } = [];
    public List<HorizonProjectVersion> Versions { get; set; } = [];
}

public sealed class HorizonRevokedEntry
{
    public string Id { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Date { get; set; } = "";
}

public sealed class HorizonIndex
{
    public int Schema { get; set; } = 1;
    public List<HorizonProject> Projects { get; set; } = [];
    public List<HorizonRevokedEntry> Revoked { get; set; } = [];
}

public sealed record HorizonRating(double Average, int Count);

public sealed record HorizonHmodMeta(string Id, string Name, string Version, string Description, string Api, string Side);

public static class HorizonClient
{
    public const string DefaultGameVersion = "26.3";
    private const string ReleasesApiUrl = "https://api.github.com/repos/horizon-rust/horizon/releases?per_page=100";
    private const string DefaultIndexUrl = "https://horizon-rust.github.io/horizon-mods/index.json";
    private const string RatingsUrl = "https://playhorizon.dev/api/mods/ratings";
    private const long MaxPackageBytes = 64L * 1024 * 1024;

    private static readonly HttpClient Http = CreateHttp();
    private static List<HorizonReleaseInfo>? _cachedReleases;
    private static DateTime _releasesFetchedAt;
    private static HorizonIndex? _cachedIndex;
    private static DateTime _indexFetchedAt;
    private static Dictionary<string, HorizonRating>? _cachedRatings;

    private static HttpClient CreateHttp()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"OrangLauncher/{UpdateManager.GetCurrentVersionString().TrimStart('v')} (HorizonClient)");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return client;
    }

    public static string HorizonRootDir => Path.Combine(PlatformPaths.GetDataDir(), "horizon");
    public static string VersionsDir => Path.Combine(HorizonRootDir, "versions");
    public static string ServerJarsDir => Path.Combine(HorizonRootDir, "server-jars");
    public static string GetVersionDir(string tag) => Path.Combine(VersionsDir, SanitizeTag(tag));

    private static string SanitizeTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return "latest";
        var invalid = Path.GetInvalidFileNameChars();
        return new string(tag.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }

    public static string GetClientExePath(string tag) => Path.Combine(GetVersionDir(tag), "horizon-client.exe");
    public static string GetPrepareExePath(string tag) => Path.Combine(GetVersionDir(tag), "horizon-prepare.exe");

    public static bool IsInstalled(string tag)
    {
        var dir = GetVersionDir(tag);
        return File.Exists(Path.Combine(dir, ".installed")) && File.Exists(GetClientExePath(tag));
    }

    public static List<string> GetInstalledVersions()
    {
        var list = new List<string>();
        if (!Directory.Exists(VersionsDir)) return list;
        foreach (var dir in Directory.GetDirectories(VersionsDir))
        {
            var tag = Path.GetFileName(dir);
            if (!string.IsNullOrEmpty(tag) && IsInstalled(tag))
                list.Add(tag);
        }
        list.Sort((a, b) => string.Compare(b, a, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    public static string GetGameVersion(string tag)
    {
        try
        {
            var lockPath = Path.Combine(GetVersionDir(tag), "research-lock.json");
            if (File.Exists(lockPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(lockPath));
                if (doc.RootElement.TryGetProperty("minecraft", out var mc) &&
                    mc.TryGetProperty("version", out var ver) &&
                    ver.ValueKind == JsonValueKind.String)
                {
                    var s = ver.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) return s;
                }
            }
        }
        catch { }
        return DefaultGameVersion;
    }

    public static int GetRequiredJavaMajor(string tag)
    {
        try
        {
            var lockPath = Path.Combine(GetVersionDir(tag), "research-lock.json");
            if (File.Exists(lockPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(lockPath));
                if (doc.RootElement.TryGetProperty("minecraft", out var mc) &&
                    mc.TryGetProperty("java_major", out var jm) &&
                    jm.TryGetInt32(out var v) && v > 0)
                    return v;
            }
        }
        catch { }
        return 21;
    }

    public static string GetResourcesDir(string tag)
    {
        var mcVer = GetGameVersion(tag);
        return Path.Combine(GetVersionDir(tag), ".local", "horizon-client", mcVer);
    }

    public static bool IsPrepared(string tag)
    {
        return File.Exists(Path.Combine(GetResourcesDir(tag), "horizon-resources.json"));
    }

    public static async Task<List<HorizonReleaseInfo>> GetReleasesAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        if (!forceRefresh && _cachedReleases is { Count: > 0 } && (DateTime.UtcNow - _releasesFetchedAt).TotalMinutes < 10)
            return _cachedReleases;

        Directory.CreateDirectory(HorizonRootDir);
        var cacheFile = Path.Combine(HorizonRootDir, "releases_cache.json");
        string? json = null;

        try
        {
            using var resp = await Http.GetAsync(ReleasesApiUrl, ct);
            if (resp.IsSuccessStatusCode)
            {
                json = await resp.Content.ReadAsStringAsync(ct);
                await File.WriteAllTextAsync(cacheFile, json, ct);
            }
        }
        catch { }

        if (string.IsNullOrWhiteSpace(json) && File.Exists(cacheFile))
        {
            try { json = await File.ReadAllTextAsync(cacheFile, ct); } catch { }
        }

        var result = new List<HorizonReleaseInfo>();
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var rel in doc.RootElement.EnumerateArray())
                    {
                        if (rel.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True)
                            continue;
                        var ghTag = rel.TryGetProperty("tag_name", out var tn) ? tn.GetString() ?? "" : "";
                        if (!IsGameReleaseTag(ghTag)) continue;

                        var title = rel.TryGetProperty("name", out var nm) ? nm.GetString() ?? ghTag : ghTag;
                        var published = rel.TryGetProperty("published_at", out var pa) ? pa.GetString() ?? "" : "";
                        var isNightly = ghTag == "nightly" || ghTag.StartsWith("nightly-", StringComparison.OrdinalIgnoreCase);

                        var assetInfo = PickPlatformAsset(rel);
                        if (assetInfo == null) continue;

                        var versionTag = isNightly ? BuildNightlyTag(ghTag, title, published) : ghTag;
                        var displayName = isNightly && !string.IsNullOrWhiteSpace(title) ? title : versionTag;
                        var checksumsUrl = $"https://github.com/horizon-rust/horizon/releases/download/{ghTag}/SHA256SUMS";

                        result.Add(new HorizonReleaseInfo(
                            versionTag,
                            ghTag,
                            displayName,
                            assetInfo.Value.Name,
                            assetInfo.Value.Url,
                            checksumsUrl,
                            isNightly,
                            published));
                    }
                }
            }
            catch { }
        }

        result = result
            .OrderByDescending(r => r.GithubTag.Equals("nightly", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(r => r.IsNightly)
            .ThenByDescending(r => r.PublishedAt, StringComparer.Ordinal)
            .ToList();

        foreach (var installed in GetInstalledVersions())
        {
            if (result.All(r => !string.Equals(r.Tag, installed, StringComparison.OrdinalIgnoreCase)))
            {
                result.Add(new HorizonReleaseInfo(
                    installed,
                    installed,
                    installed,
                    "horizon-windows-x64.zip",
                    "",
                    "",
                    installed.StartsWith("nightly", StringComparison.OrdinalIgnoreCase),
                    ""));
            }
        }

        if (result.Count == 0)
        {
            var archAsset = RuntimeInformation.OSArchitecture == Architecture.Arm64
                ? "horizon-windows-arm64.zip"
                : "horizon-windows-x64.zip";
            result.Add(new HorizonReleaseInfo(
                "nightly",
                "nightly",
                "Horizon Nightly (Latest)",
                archAsset,
                $"https://github.com/horizon-rust/horizon/releases/download/nightly/{archAsset}",
                "https://github.com/horizon-rust/horizon/releases/download/nightly/SHA256SUMS",
                true,
                ""));
        }

        _cachedReleases = result;
        _releasesFetchedAt = DateTime.UtcNow;
        return result;
    }

    public static async Task<IReadOnlyList<string>> GetReleaseTagsAsync(CancellationToken ct = default)
    {
        var releases = await GetReleasesAsync(false, ct);
        return releases.Select(r => r.Tag).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsGameReleaseTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return false;
        if (tag.StartsWith("launcher@", StringComparison.OrdinalIgnoreCase) ||
            tag.StartsWith("launcher-v", StringComparison.OrdinalIgnoreCase))
            return false;
        if (tag == "nightly" || tag.StartsWith("nightly-", StringComparison.OrdinalIgnoreCase))
            return true;
        return tag.Length > 1 && tag[0] == 'v' && char.IsDigit(tag[1]);
    }

    private static string BuildNightlyTag(string ghTag, string title, string publishedAt)
    {
        if (ghTag.StartsWith("nightly-", StringComparison.OrdinalIgnoreCase) && ghTag.Length > 8)
            return ghTag;

        var datePart = publishedAt.Length >= 10 ? publishedAt[..10] : DateTime.UtcNow.ToString("yyyy-MM-dd");
        var commitPart = "";

        if (!string.IsNullOrWhiteSpace(title))
        {
            var m = Regex.Match(title, @"(\d{4}-\d{2}-\d{2})");
            if (m.Success) datePart = m.Groups[1].Value;
            var parts = title.Split(['·', '-', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var last = parts.LastOrDefault() ?? "";
            if (last.Length is >= 7 and <= 12 && last.All(Uri.IsHexDigit))
                commitPart = last;
        }

        return string.IsNullOrEmpty(commitPart) ? $"nightly-{datePart}" : $"nightly-{datePart}-{commitPart}";
    }

    private static (string Name, string Url)? PickPlatformAsset(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return null;

        string[] candidates = RuntimeInformation.OSArchitecture == Architecture.Arm64
            ? ["horizon-windows-arm64.zip", "horizon-windows-x64.zip"]
            : ["horizon-windows-x64.zip"];

        foreach (var wanted in candidates)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
                if (string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(url))
                    return (name, url);
            }
        }
        return null;
    }

    public static async Task<string> ResolveTagAsync(string? requestedTag, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(requestedTag) &&
            !string.Equals(requestedTag, "Latest", StringComparison.OrdinalIgnoreCase))
        {
            if (IsInstalled(requestedTag)) return requestedTag;
            var all = await GetReleasesAsync(false, ct);
            var match = all.FirstOrDefault(r =>
                string.Equals(r.Tag, requestedTag, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.GithubTag, requestedTag, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match.Tag;
        }

        var releases = await GetReleasesAsync(false, ct);
        if (releases.Count > 0) return releases[0].Tag;

        var installed = GetInstalledVersions();
        return installed.Count > 0 ? installed[0] : "nightly";
    }

    public static async Task<HashSet<string>> GetSupportedClientFlagsAsync(string clientExe, CancellationToken ct = default)
    {
        var flags = new HashSet<string>(StringComparer.Ordinal);
        if (!File.Exists(clientExe)) return flags;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = clientExe,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("--help");
            using var proc = Process.Start(psi);
            if (proc == null) return flags;
            var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = proc.StandardError.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);
            var text = (await stdoutTask) + "\n" + (await stderrTask);
            foreach (Match m in Regex.Matches(text, @"--[A-Za-z0-9-]+"))
                flags.Add(m.Value);
        }
        catch { }
        return flags;
    }

    public static Dictionary<string, string> ParseHorizonEnvVars(string? text)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text)) return map;
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            foreach (var word in LaunchService.SplitArgs(line))
            {
                var eq = word.IndexOf('=');
                if (eq <= 0) continue;
                var k = word[..eq].Trim();
                var v = word[(eq + 1)..].Trim();
                if (k.Length > 0 && (char.IsLetter(k[0]) || k[0] == '_') && k.All(c => char.IsLetterOrDigit(c) || c == '_'))
                    map[k] = v;
            }
        }
        return map;
    }

    public static async Task EnsureInstalledAsync(
        string tag,
        Action<double, string>? progress,
        Action<string>? log,
        CancellationToken ct = default)
    {
        if (IsInstalled(tag))
        {
            log?.Invoke($"[Horizon] Build {tag} is already installed.");
            return;
        }

        var releases = await GetReleasesAsync(false, ct);
        var rel = releases.FirstOrDefault(r =>
                      string.Equals(r.Tag, tag, StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(r.GithubTag, tag, StringComparison.OrdinalIgnoreCase))
                  ?? releases.FirstOrDefault();

        if (rel == null || string.IsNullOrWhiteSpace(rel.DownloadUrl))
            throw new InvalidOperationException($"Could not find Horizon release download for '{tag}'.");

        Directory.CreateDirectory(VersionsDir);
        var tmpZip = Path.Combine(VersionsDir, $".{SanitizeTag(tag)}.zip.part");
        var destDir = GetVersionDir(tag);

        log?.Invoke($"[Horizon] Downloading {rel.Tag} ({rel.AssetName})...");
        progress?.Invoke(5, $"Downloading Horizon {rel.Tag}...");

        var expectedSha256 = await TryFetchExpectedSha256Async(rel.ChecksumsUrl, rel.AssetName, ct);

        using (var resp = await Http.GetAsync(rel.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength ?? -1L;
            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst = new FileStream(tmpZip, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            using var sha = SHA256.Create();

            var buf = new byte[81920];
            long readTotal = 0;
            int read;
            while ((read = await src.ReadAsync(buf, ct)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, read), ct);
                sha.TransformBlock(buf, 0, read, null, 0);
                readTotal += read;
                if (total > 0)
                {
                    var pct = 5 + (readTotal / (double)total) * 40.0;
                    progress?.Invoke(pct, $"Downloading Horizon {rel.Tag} ({readTotal / 1048576.0:F1} / {total / 1048576.0:F1} MB)...");
                }
            }
            sha.TransformFinalBlock([], 0, 0);
            var actualSha256 = Convert.ToHexString(sha.Hash!).ToLowerInvariant();

            if (!string.IsNullOrEmpty(expectedSha256) &&
                !string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                dst.Close();
                try { File.Delete(tmpZip); } catch { }
                throw new InvalidDataException($"Horizon archive SHA-256 mismatch (expected {expectedSha256}, got {actualSha256}).");
            }
        }

        progress?.Invoke(46, $"Unpacking Horizon {rel.Tag}...");
        log?.Invoke($"[Horizon] Extracting {rel.AssetName} to {destDir}...");

        var stageDir = Path.Combine(VersionsDir, $".stage-{SanitizeTag(tag)}");
        if (Directory.Exists(stageDir)) Directory.Delete(stageDir, true);
        Directory.CreateDirectory(stageDir);

        try
        {
            ExtractArchiveFlattenTop(tmpZip, stageDir);
            await File.WriteAllTextAsync(Path.Combine(stageDir, ".installed"), DateTime.UtcNow.ToString("O"), ct);
            if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
            Directory.Move(stageDir, destDir);
        }
        finally
        {
            try { if (File.Exists(tmpZip)) File.Delete(tmpZip); } catch { }
            try { if (Directory.Exists(stageDir)) Directory.Delete(stageDir, true); } catch { }
        }

        log?.Invoke($"[Horizon] Installed {rel.Tag} successfully.");
    }

    private static async Task<string?> TryFetchExpectedSha256Async(string checksumsUrl, string assetName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(checksumsUrl)) return null;
        try
        {
            using var resp = await Http.GetAsync(checksumsUrl, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var text = await resp.Content.ReadAsStringAsync(ct);
            foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var file = parts[^1].TrimStart('*');
                    if (string.Equals(file, assetName, StringComparison.OrdinalIgnoreCase) && parts[0].Length == 64)
                        return parts[0].ToLowerInvariant();
                }
            }
        }
        catch { }
        return null;
    }

    private static void ExtractArchiveFlattenTop(string zipPath, string destDir)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        string? commonPrefix = null;
        bool singleTopRoot = true;

        foreach (var entry in archive.Entries)
        {
            var norm = entry.FullName.Replace('\\', '/').TrimStart('/');
            if (string.IsNullOrEmpty(norm)) continue;
            var slash = norm.IndexOf('/');
            if (slash <= 0)
            {
                singleTopRoot = false;
                break;
            }
            var top = norm[..(slash + 1)];
            if (commonPrefix == null) commonPrefix = top;
            else if (!string.Equals(commonPrefix, top, StringComparison.Ordinal))
            {
                singleTopRoot = false;
                break;
            }
        }

        var fullDestRoot = Path.GetFullPath(destDir);
        foreach (var entry in archive.Entries)
        {
            var rel = entry.FullName.Replace('\\', '/').TrimStart('/');
            if (singleTopRoot && commonPrefix != null && rel.StartsWith(commonPrefix, StringComparison.Ordinal))
                rel = rel[commonPrefix.Length..];

            if (string.IsNullOrEmpty(rel) || rel.EndsWith('/')) continue;

            var targetPath = Path.GetFullPath(Path.Combine(destDir, rel.Replace('/', Path.DirectorySeparatorChar)));
            if (!targetPath.StartsWith(fullDestRoot, StringComparison.OrdinalIgnoreCase))
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            entry.ExtractToFile(targetPath, true);
        }
    }

    public static async Task PrepareAsync(
        string tag,
        string instanceMinecraftDir,
        Action<double, string>? progress,
        Action<string>? log,
        CancellationToken ct = default)
    {
        if (IsPrepared(tag))
        {
            log?.Invoke($"[Horizon] Resources for {tag} are already prepared.");
            return;
        }

        if (TryReusePreparedResources(tag, log))
            return;

        var versionDir = GetVersionDir(tag);
        var prepareExe = GetPrepareExePath(tag);
        var mcVersion = GetGameVersion(tag);
        var resourcesDir = GetResourcesDir(tag);
        Directory.CreateDirectory(resourcesDir);

        var sharedMcDir = PlatformPaths.GetMinecraftDir();
        Directory.CreateDirectory(sharedMcDir);

        if (File.Exists(prepareExe))
        {
            progress?.Invoke(55, $"Preparing Minecraft {mcVersion} assets for Horizon...");
            log?.Invoke($"[Horizon] Preparing Minecraft {mcVersion} resources...");

            var serverJarPath = await EnsureVanillaAssetsAndServerJarAsync(mcVersion, sharedMcDir, progress, log, ct);
            var javaMajor = GetRequiredJavaMajor(tag);
            var javaExe = await ResolveJavaForPrepareAsync(javaMajor, progress, log);

            if (!string.IsNullOrEmpty(serverJarPath) && File.Exists(serverJarPath))
            {
                progress?.Invoke(72, "Running horizon-prepare...");
                var prepCode = await RunPrepareToolAsync(
                    prepareExe,
                    versionDir,
                    ["--root", versionDir, "--prepare", "--server-jar", serverJarPath, "--minecraft-dir", sharedMcDir],
                    javaExe,
                    log,
                    ct);

                if (prepCode == 0)
                {
                    progress?.Invoke(86, "Preparing Horizon sounds...");
                    await RunPrepareToolAsync(
                        prepareExe,
                        versionDir,
                        ["sounds", "--root", versionDir, "--minecraft-dir", sharedMcDir],
                        javaExe,
                        log,
                        ct);
                }
            }
            else
            {
                await RunPrepareToolAsync(
                    prepareExe,
                    versionDir,
                    ["sounds", "--root", versionDir, "--minecraft-dir", sharedMcDir],
                    javaExe,
                    log,
                    ct);
            }
        }

        if (!IsPrepared(tag))
        {
            CopyFallbackVanillaAssets(sharedMcDir, resourcesDir, log);
            var manifestPath = Path.Combine(resourcesDir, "horizon-resources.json");
            if (!File.Exists(manifestPath))
            {
                var fallbackJson = JsonSerializer.Serialize(new
                {
                    version = mcVersion,
                    prepared_at = DateTime.UtcNow.ToString("O"),
                    generator = "OrangLauncher"
                }, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(manifestPath, fallbackJson, ct);
            }
        }
    }

    private static bool TryReusePreparedResources(string targetTag, Action<string>? log)
    {
        try
        {
            var targetDir = GetVersionDir(targetTag);
            var targetMc = GetGameVersion(targetTag);
            var targetLock = ReadLockPins(Path.Combine(targetDir, "research-lock.json"));

            foreach (var otherTag in GetInstalledVersions())
            {
                if (string.Equals(otherTag, targetTag, StringComparison.OrdinalIgnoreCase)) continue;
                if (!IsPrepared(otherTag)) continue;

                var otherDir = GetVersionDir(otherTag);
                var otherLock = ReadLockPins(Path.Combine(otherDir, "research-lock.json"));
                if (targetLock != null && otherLock != null && targetLock != otherLock) continue;

                var srcRes = GetResourcesDir(otherTag);
                var dstRes = GetResourcesDir(targetTag);
                if (!Directory.Exists(srcRes)) continue;

                log?.Invoke($"[Horizon] Reusing prepared resources from {otherTag}...");
                CopyDirectoryExcept(srcRes, dstRes, "horizon-data.bin");

                var srcReports = Path.Combine(otherDir, ".local", "datagen", "out", "reports");
                var dstReports = Path.Combine(targetDir, ".local", "datagen", "out", "reports");
                if (Directory.Exists(srcReports))
                {
                    Directory.CreateDirectory(dstReports);
                    foreach (var rep in new[] { "registries.json", "commands.json" })
                    {
                        var sf = Path.Combine(srcReports, rep);
                        if (File.Exists(sf)) File.Copy(sf, Path.Combine(dstReports, rep), true);
                    }
                }

                if (IsPrepared(targetTag)) return true;
            }
        }
        catch { }
        return false;
    }

    private static string? ReadLockPins(string lockFile)
    {
        if (!File.Exists(lockFile)) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(lockFile));
            if (doc.RootElement.TryGetProperty("minecraft", out var mc))
            {
                var ver = mc.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "";
                var c = mc.TryGetProperty("client_sha1", out var cs) ? cs.GetString() ?? "" : "";
                var s = mc.TryGetProperty("server_sha1", out var ss) ? ss.GetString() ?? "" : "";
                return $"{ver}:{c}:{s}";
            }
        }
        catch { }
        return null;
    }

    private static void CopyDirectoryExcept(string src, string dst, string excludedFileName)
    {
        Directory.CreateDirectory(dst);
        foreach (var file in Directory.GetFiles(src))
        {
            var name = Path.GetFileName(file);
            if (string.Equals(name, excludedFileName, StringComparison.OrdinalIgnoreCase)) continue;
            File.Copy(file, Path.Combine(dst, name), true);
        }
        foreach (var dir in Directory.GetDirectories(src))
        {
            var name = Path.GetFileName(dir);
            CopyDirectoryExcept(dir, Path.Combine(dst, name), excludedFileName);
        }
    }

    private static async Task<string?> EnsureVanillaAssetsAndServerJarAsync(
        string mcVersion,
        string sharedMcDir,
        Action<double, string>? progress,
        Action<string>? log,
        CancellationToken ct)
    {
        Directory.CreateDirectory(ServerJarsDir);
        var serverJar = Path.Combine(ServerJarsDir, $"{mcVersion}.jar");

        try
        {
            using var manifestResp = await Http.GetAsync("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json", ct);
            if (!manifestResp.IsSuccessStatusCode) return File.Exists(serverJar) ? serverJar : null;

            using var manifestDoc = JsonDocument.Parse(await manifestResp.Content.ReadAsStringAsync(ct));
            var versions = manifestDoc.RootElement.GetProperty("versions");
            string? versionMetaUrl = null;
            string resolvedMcVersion = mcVersion;

            foreach (var v in versions.EnumerateArray())
            {
                var id = v.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                if (string.Equals(id, mcVersion, StringComparison.OrdinalIgnoreCase))
                {
                    versionMetaUrl = v.GetProperty("url").GetString();
                    break;
                }
            }

            if (versionMetaUrl == null &&
                manifestDoc.RootElement.TryGetProperty("latest", out var latest) &&
                latest.TryGetProperty("release", out var latestRel))
            {
                resolvedMcVersion = latestRel.GetString() ?? mcVersion;
                foreach (var v in versions.EnumerateArray())
                {
                    var id = v.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                    if (string.Equals(id, resolvedMcVersion, StringComparison.OrdinalIgnoreCase))
                    {
                        versionMetaUrl = v.GetProperty("url").GetString();
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(versionMetaUrl))
                return File.Exists(serverJar) ? serverJar : null;

            using var metaResp = await Http.GetAsync(versionMetaUrl, ct);
            if (!metaResp.IsSuccessStatusCode) return File.Exists(serverJar) ? serverJar : null;
            var metaJson = await metaResp.Content.ReadAsStringAsync(ct);

            var versionDir = Path.Combine(sharedMcDir, "versions", mcVersion);
            Directory.CreateDirectory(versionDir);
            var versionJsonPath = Path.Combine(versionDir, $"{mcVersion}.json");
            if (!File.Exists(versionJsonPath))
                await File.WriteAllTextAsync(versionJsonPath, metaJson, ct);

            using var metaDoc = JsonDocument.Parse(metaJson);
            if (metaDoc.RootElement.TryGetProperty("downloads", out var downloads))
            {
                var clientJarPath = Path.Combine(versionDir, $"{mcVersion}.jar");
                if (!File.Exists(clientJarPath) &&
                    downloads.TryGetProperty("client", out var clientObj) &&
                    clientObj.TryGetProperty("url", out var clientUrlProp))
                {
                    var clientUrl = clientUrlProp.GetString();
                    if (!string.IsNullOrEmpty(clientUrl))
                    {
                        progress?.Invoke(60, $"Downloading Minecraft {mcVersion} client jar...");
                        await DownloadFileDirectAsync(clientUrl, clientJarPath, ct);
                    }
                }

                if (!File.Exists(serverJar) &&
                    downloads.TryGetProperty("server", out var serverObj) &&
                    serverObj.TryGetProperty("url", out var serverUrlProp))
                {
                    var serverUrl = serverUrlProp.GetString();
                    if (!string.IsNullOrEmpty(serverUrl))
                    {
                        progress?.Invoke(65, $"Downloading Minecraft {mcVersion} server jar...");
                        await DownloadFileDirectAsync(serverUrl, serverJar, ct);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            log?.Invoke($"[Horizon] Asset check note: {ex.Message}");
        }

        return File.Exists(serverJar) ? serverJar : null;
    }

    private static async Task DownloadFileDirectAsync(string url, string targetPath, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var tmp = targetPath + ".part";
        using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        await using (var src = await resp.Content.ReadAsStreamAsync(ct))
        await using (var dst = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
        {
            await src.CopyToAsync(dst, ct);
        }
        File.Move(tmp, targetPath, true);
    }

    private static async Task<string?> ResolveJavaForPrepareAsync(int major, Action<double, string>? progress, Action<string>? log)
    {
        try
        {
            return await JavaResolver.EnsureAsync(major, s => log?.Invoke($"[Java] {s}"));
        }
        catch
        {
            try
            {
                return await JavaResolver.EnsureAsync(21, s => log?.Invoke($"[Java] {s}"));
            }
            catch
            {
                return null;
            }
        }
    }

    private static async Task<int> RunPrepareToolAsync(
        string exePath,
        string workingDir,
        IReadOnlyList<string> args,
        string? javaExe,
        Action<string>? log,
        CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            if (!string.IsNullOrEmpty(javaExe) && File.Exists(javaExe))
            {
                var binDir = Path.GetDirectoryName(javaExe)!;
                var homeDir = Directory.GetParent(binDir)?.FullName ?? binDir;
                psi.Environment["JAVA_HOME"] = homeDir;
                var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                psi.Environment["PATH"] = $"{binDir}{Path.PathSeparator}{currentPath}";
            }

            using var proc = new Process { StartInfo = psi };
            proc.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) log?.Invoke($"[horizon-prepare] {e.Data}"); };
            proc.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) log?.Invoke($"[horizon-prepare] {e.Data}"); };

            if (!proc.Start()) return -1;
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await proc.WaitForExitAsync(ct);
            return proc.ExitCode;
        }
        catch (Exception ex)
        {
            log?.Invoke($"[Horizon] Prepare tool notice: {ex.Message}");
            return -1;
        }
    }

    private static void CopyFallbackVanillaAssets(string sharedMcDir, string resourcesDir, Action<string>? log)
    {
        try
        {
            var versionsRoot = Path.Combine(sharedMcDir, "versions");
            if (!Directory.Exists(versionsRoot)) return;
            var jarCandidate = Directory.GetFiles(versionsRoot, "*.jar", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (jarCandidate == null) return;

            using var zip = ZipFile.OpenRead(jarCandidate);
            foreach (var entry in zip.Entries)
            {
                if (!entry.FullName.StartsWith("assets/", StringComparison.Ordinal) || entry.FullName.EndsWith('/'))
                    continue;
                var dst = Path.Combine(resourcesDir, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                if (!File.Exists(dst)) entry.ExtractToFile(dst, false);
            }
        }
        catch { }
    }

    public static string WriteSessionFile(string instanceMinecraftDir, string username, string uuid, string accessToken, bool isMicrosoft)
    {
        var sessionsDir = Path.Combine(instanceMinecraftDir, "sessions");
        Directory.CreateDirectory(sessionsDir);
        var randHex = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
        var sessionPath = Path.Combine(sessionsDir, $"session-{randHex}.json");

        var cleanUuid = (uuid ?? "").Replace("-", "").Trim();
        if (string.IsNullOrEmpty(cleanUuid))
            cleanUuid = Guid.NewGuid().ToString("N");
        var payload = new
        {
            name = string.IsNullOrWhiteSpace(username) ? "Player" : username,
            uuid = cleanUuid,
            access_token = isMicrosoft && !string.IsNullOrWhiteSpace(accessToken) ? accessToken : "0",
            user_type = isMicrosoft ? "msa" : "legacy",
            xuid = (string?)null
        };

        File.WriteAllText(sessionPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        return sessionPath;
    }

    public static bool HandleStartCrashFallback(string instanceMinecraftDir, int exitCode, IReadOnlyList<string> logLines, Action<string>? log)
    {
        var unsignedExit = unchecked((uint)exitCode);
        var isNativeCrash = (unsignedExit & 0xC0000000u) == 0xC0000000u || exitCode is 134 or 139;
        if (!isNativeCrash) return false;

        bool sawRendererInit = false;
        bool sawFirstFrame = false;
        foreach (var line in logLines)
        {
            if (line.Contains("Horizon renderer:", StringComparison.OrdinalIgnoreCase) &&
                (line.Contains("Vulkan", StringComparison.OrdinalIgnoreCase) || line.Contains("Gl,", StringComparison.OrdinalIgnoreCase)))
                sawRendererInit = true;
            if (line.Contains("Horizon: first frame", StringComparison.OrdinalIgnoreCase))
                sawFirstFrame = true;
        }
        if (!sawRendererInit || sawFirstFrame) return false;
        try
        {
            var gfxPath = Path.Combine(instanceMinecraftDir, "horizon-graphics.txt");
            var lines = File.Exists(gfxPath) ? File.ReadAllLines(gfxPath).ToList() : [];
            var idx = lines.FindIndex(l => l.TrimStart().StartsWith("graphicsApi:", StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) lines[idx] = "graphicsApi:dx12";
            else lines.Add("graphicsApi:dx12");
            File.WriteAllLines(gfxPath, lines);
            log?.Invoke("[Horizon] Renderer crashed before first frame; switched graphicsApi to dx12 in horizon-graphics.txt.");
            return true;
        }
        catch
        {return false;}
    }

    public static Dictionary<string, bool> ReadModStates(string instanceMinecraftDir)
    {
        var map = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var file = Path.Combine(instanceMinecraftDir, "horizon-mods.txt");
        if (!File.Exists(file)) return map;
        try
        {
            foreach (var raw in File.ReadAllLines(file))
            {
                var line = raw.Trim();
                if (!line.StartsWith("mod.", StringComparison.OrdinalIgnoreCase)) continue;
                var colon = line.IndexOf(':');
                if (colon <= 4) continue;
                var id = line[4..colon].Trim();
                var val = line[(colon + 1)..].Trim();
                if (!string.IsNullOrEmpty(id))
                    map[id] = string.Equals(val, "true", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch { }
        return map;
    }

    public static bool IsModEnabled(string instanceMinecraftDir, string modId, bool defaultWhenMissing = true)
    {
        if (string.IsNullOrWhiteSpace(modId)) return defaultWhenMissing;
        var states = ReadModStates(instanceMinecraftDir);
        return states.TryGetValue(modId, out var enabled) ? enabled : defaultWhenMissing;
    }

    public static void SetModEnabled(string instanceMinecraftDir, string modId, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(modId)) return;
        Directory.CreateDirectory(instanceMinecraftDir);
        var file = Path.Combine(instanceMinecraftDir, "horizon-mods.txt");
        var lines = File.Exists(file) ? File.ReadAllLines(file).ToList() : [];
        var prefix = $"mod.{modId}:";
        var replacement = $"mod.{modId}:{(enabled ? "true" : "false")}";
        var idx = lines.FindIndex(l => l.Trim().StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0) lines[idx] = replacement;
        else lines.Add(replacement);
        File.WriteAllLines(file, lines);
    }

    public static HorizonHmodMeta? ReadHmodMetadata(string path)
    {
        try
        {
            string? toml = null;
            if (Directory.Exists(path))
            {
                var manifest = Path.Combine(path, "horizon-mod.toml");
                if (File.Exists(manifest)) toml = File.ReadAllText(manifest);
            }
            else if (File.Exists(path))
            {
                using var zip = ZipFile.OpenRead(path);
                var entry = zip.GetEntry("horizon-mod.toml")
                            ?? zip.Entries.FirstOrDefault(e => e.FullName.EndsWith("/horizon-mod.toml", StringComparison.OrdinalIgnoreCase));
                if (entry != null)
                {
                    using var sr = new StreamReader(entry.Open(), Encoding.UTF8);
                    toml = sr.ReadToEnd();
                }
            }

            if (string.IsNullOrWhiteSpace(toml)) return null;
            var fields = ParseSimpleTomlKeyValues(toml);
            var fallbackId = Path.GetFileNameWithoutExtension(path);
            if (fallbackId.EndsWith(".hmod", StringComparison.OrdinalIgnoreCase))
                fallbackId = Path.GetFileNameWithoutExtension(fallbackId);
            var id = fields.GetValueOrDefault("id") ?? fallbackId;
            var name = fields.GetValueOrDefault("name") ?? id;
            var version = fields.GetValueOrDefault("version") ?? "0.1.0";
            var desc = fields.GetValueOrDefault("description") ?? "";
            var api = fields.GetValueOrDefault("api") ?? "0.1";
            var side = fields.GetValueOrDefault("side") ?? "both";
            return new HorizonHmodMeta(id, name, version, desc, api, side);
        }
        catch
        {
            return null;
        }
    }

    public static Dictionary<string, string> ParseSimpleTomlKeyValues(string toml)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in toml.Split('\n'))
        {
            var line = raw.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#') || line.StartsWith('[')) continue;
            var eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var key = line[..eq].Trim();
            var val = line[(eq + 1)..].Trim();
            if (val.Length >= 2 && ((val.StartsWith('"') && val.EndsWith('"')) || (val.StartsWith('\'') && val.EndsWith('\''))))
                val = val[1..^1];
            if (!dict.ContainsKey(key))
                dict[key] = val;
        }
        return dict;
    }

    public static async Task TurnOffRevokedAsync(string instanceMinecraftDir, Action<string>? log = null, CancellationToken ct = default)
    {
        try
        {
            var modsDir = Path.Combine(instanceMinecraftDir, "horizon-mods");
            if (!Directory.Exists(modsDir)) return;
            var index = await GetIndexAsync(false, ct);
            if (index.Revoked.Count == 0) return;
            var revokedHashes = new HashSet<string>(index.Revoked.Select(r => r.Sha256), StringComparer.OrdinalIgnoreCase);
            foreach (var hmod in Directory.GetFiles(modsDir, "*.hmod"))
            {
                var hash = ComputeFileSha256(hmod);
                var meta = ReadHmodMetadata(hmod);
                var modId = meta?.Id ?? Path.GetFileNameWithoutExtension(hmod);
                if (revokedHashes.Contains(hash) ||
                    index.Revoked.Any(r => string.Equals(r.Id, modId, StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(r.Sha256)))
                {
                    SetModEnabled(instanceMinecraftDir, modId, false);
                    log?.Invoke($"[Horizon] Disabled revoked mod '{modId}'.");
                }
            }
        }
        catch { }
    }

    public static string ComputeFileSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static async Task<HorizonIndex> GetIndexAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        if (!forceRefresh && _cachedIndex != null && (DateTime.UtcNow - _indexFetchedAt).TotalMinutes < 5)
            return _cachedIndex;
        Directory.CreateDirectory(HorizonRootDir);
        var cachePath = Path.Combine(HorizonRootDir, "horizon-index.json");
        var indexUrl = Environment.GetEnvironmentVariable("HORIZON_MOD_INDEX");
        if (string.IsNullOrWhiteSpace(indexUrl)) indexUrl = DefaultIndexUrl;
        string? json = null;
        try
        {
            using var resp = await Http.GetAsync(indexUrl, ct);
            if (resp.IsSuccessStatusCode)
            {
                json = await resp.Content.ReadAsStringAsync(ct);
                await File.WriteAllTextAsync(cachePath, json, ct);
            }
        }
        catch { }

        if (string.IsNullOrWhiteSpace(json) && File.Exists(cachePath))
        {try { json = await File.ReadAllTextAsync(cachePath, ct); } catch { }}
        var index = ParseIndexJson(json);
        _cachedIndex = index;
        _indexFetchedAt = DateTime.UtcNow;
        return index;
    }

    private static HorizonIndex ParseIndexJson(string? json)
    {
        var result = new HorizonIndex();
        if (string.IsNullOrWhiteSpace(json)) return result;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("schema", out var sc) && sc.TryGetInt32(out var sv))
                result.Schema = sv;

            if (root.TryGetProperty("projects", out var projs) && projs.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in projs.EnumerateArray())
                {
                    var proj = new HorizonProject
                    {
                        Id = GetStr(p, "id"),
                        Kind = GetStr(p, "kind", "mod"),
                        Name = GetStr(p, "name"),
                        Summary = GetStr(p, "summary"),
                        Description = GetStr(p, "description"),
                        License = GetStr(p, "license"),
                        Source = GetStr(p, "source"),
                        Icon = GetStr(p, "icon"),
                        Key = GetStr(p, "key"),
                        Downloads = GetLong(p, "downloads"),
                        Stars = GetLong(p, "stars"),
                        Updated = GetStr(p, "updated"),
                        Authors = GetStrList(p, "authors"),
                        Owners = GetStrList(p, "owners"),
                        Categories = GetStrList(p, "categories"),
                        Tags = GetStrList(p, "tags"),
                        Screenshots = GetStrList(p, "screenshots")
                    };

                    if (string.IsNullOrWhiteSpace(proj.Name)) proj.Name = proj.Id;
                    if (string.IsNullOrWhiteSpace(proj.Description)) proj.Description = proj.Summary;
                    if (p.TryGetProperty("versions", out var vers) && vers.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var v in vers.EnumerateArray())
                        {
                            var pv = new HorizonProjectVersion
                            {
                                Version = GetStr(v, "version", "0.1.0"), Published = GetStr(v, "published"), Changelog = GetStr(v, "changelog"), Api = GetStr(v, "api"), Side = GetStr(v, "side", "both"), Game = GetStr(v, "game", DefaultGameVersion), Capabilities = GetStrList(v, "capabilities")
                            };

                            if (v.TryGetProperty("requires", out var reqs) && reqs.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var r in reqs.EnumerateArray())
                                {
                                    pv.Requires.Add(new HorizonDependencyRef
                                    {
                                        Id = GetStr(r, "id"),
                                        Version = GetStr(r, "version"),
                                        Sha256 = GetStr(r, "sha256")
                                    });
                                }
                            }

                            if (v.TryGetProperty("file", out var f) && f.ValueKind == JsonValueKind.Object)
                            {
                                pv.File = new HorizonFileRef
                                {
                                    Name = GetStr(f, "name"),
                                    Url = GetStr(f, "url"),
                                    Size = GetLong(f, "size"),
                                    Sha256 = GetStr(f, "sha256"),
                                    Signature = GetStr(f, "signature")
                                };
                            }
                            proj.Versions.Add(pv);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(proj.Id))
                        result.Projects.Add(proj);
                }
            }

            if (root.TryGetProperty("revoked", out var revs) && revs.ValueKind == JsonValueKind.Array)
            {
                foreach (var r in revs.EnumerateArray())
                {
                    result.Revoked.Add(new HorizonRevokedEntry
                    {
                        Id = GetStr(r, "id"),
                        Sha256 = GetStr(r, "sha256"),
                        Reason = GetStr(r, "reason"),
                        Date = GetStr(r, "date")
                    });
                }
            }
        }
        catch { }

        return result;
    }

    public static async Task<Dictionary<string, HorizonRating>> GetRatingsAsync(CancellationToken ct = default)
    {
        if (_cachedRatings != null) return _cachedRatings;
        var map = new Dictionary<string, HorizonRating>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var resp = await Http.GetAsync(RatingsUrl, ct);
            if (resp.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                if (doc.RootElement.TryGetProperty("projects", out var projects) && projects.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in projects.EnumerateObject())
                    {
                        var avg = prop.Value.TryGetProperty("average", out var a) && a.TryGetDouble(out var av) ? av : 0;
                        var cnt = prop.Value.TryGetProperty("count", out var c) && c.TryGetInt32(out var cv) ? cv : 0;
                        map[prop.Name] = new HorizonRating(avg, cnt);
                    }
                }
            }
        }
        catch { }
        _cachedRatings = map;
        return map;
    }

    public static async Task<(List<HorizonProject> Hits, int TotalHits)> SearchAsync(
        string query,
        string projectType,
        string sort,
        int offset = 0,
        int limit = 20,
        CancellationToken ct = default)
    {
        var index = await GetIndexAsync(false, ct);
        var words = (query ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var filtered = index.Projects.Where(p => MatchesKind(p.Kind, projectType) && MatchesQuery(p, words));

        filtered = sort switch
        {
            "newest" => filtered.OrderByDescending(p => p.Versions.FirstOrDefault()?.Published ?? p.Updated),
            "updated" => filtered.OrderByDescending(p => p.Updated),
            "follows" => filtered.OrderByDescending(p => p.Stars).ThenByDescending(p => p.Downloads),
            _ => filtered.OrderByDescending(p => p.Downloads).ThenByDescending(p => p.Stars)
        };

        var all = filtered.ToList();
        var page = all.Skip(Math.Max(0, offset)).Take(Math.Max(1, limit)).ToList();
        return (page, all.Count);
    }

    private static bool MatchesKind(string projectKind, string requestedType)
    {
        var k = (projectKind ?? "mod").ToLowerInvariant();
        return (requestedType ?? "mod").ToLowerInvariant() switch
        {
            "mod" => k is "mod" or "native-mod",
            "resourcepack" => k is "resourcepack" or "resource-pack",
            "datapack" => k is "datapack" or "data-pack",
            "shader" => k is "shader" or "shaderpack" or "shader-pack",
            "modpack" => k is "modpack" or "mod-pack",
            _ => true
        };
    }

    private static bool MatchesQuery(HorizonProject p, string[] words)
    {
        if (words.Length == 0) return true;
        foreach (var w in words)
        {
            bool match =
                p.Id.Contains(w, StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains(w, StringComparison.OrdinalIgnoreCase) ||
                p.Summary.Contains(w, StringComparison.OrdinalIgnoreCase) ||
                p.Authors.Any(a => a.Contains(w, StringComparison.OrdinalIgnoreCase)) ||
                p.Tags.Any(t => t.Contains(w, StringComparison.OrdinalIgnoreCase)) ||
                p.Categories.Any(c => c.Contains(w, StringComparison.OrdinalIgnoreCase));
            if (!match) return false;
        }
        return true;
    }

    public static async Task<HorizonProject?> GetProjectAsync(string id, CancellationToken ct = default)
    {
        var index = await GetIndexAsync(false, ct);
        return index.Projects.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public static async Task<string> InstallProjectAsync(
        string projectId,
        string destDir,
        string? instanceMinecraftDir = null,
        string? versionNumber = null,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var index = await GetIndexAsync(false, ct);
        var project = index.Projects.FirstOrDefault(p => string.Equals(p.Id, projectId, StringComparison.OrdinalIgnoreCase))
                      ?? throw new InvalidOperationException($"Horizon project '{projectId}' was not found in the registry.");

        var version = (!string.IsNullOrWhiteSpace(versionNumber)
                          ? project.Versions.FirstOrDefault(v => string.Equals(v.Version, versionNumber, StringComparison.OrdinalIgnoreCase))
                          : null)
                      ?? project.Versions.FirstOrDefault()
                      ?? throw new InvalidOperationException($"Horizon project '{project.Name}' has no published versions.");

        return await DownloadAndInstallVersionAsync(index, project, version, destDir, instanceMinecraftDir, progress, new HashSet<string>(StringComparer.OrdinalIgnoreCase), ct);
    }

    private static async Task<string> DownloadAndInstallVersionAsync(
        HorizonIndex index,
        HorizonProject project,
        HorizonProjectVersion version,
        string destDir,
        string? instanceMinecraftDir,
        IProgress<double>? progress,
        HashSet<string> visited,
        CancellationToken ct)
    {
        if (!visited.Add(project.Id)) return "";
        if (index.Revoked.Any(r =>
                string.Equals(r.Id, project.Id, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrEmpty(r.Sha256) || string.Equals(r.Sha256, version.File.Sha256, StringComparison.OrdinalIgnoreCase))))
        {
            throw new InvalidOperationException($"Horizon package '{project.Name} {version.Version}' has been revoked for security reasons.");
        }

        var url = version.File.Url;
        if (!IsAllowedPackageUrl(url))
            throw new InvalidOperationException($"Blocked untrusted package download URL: {url}");

        if (version.File.Size > MaxPackageBytes)
            throw new InvalidOperationException($"Package '{project.Name}' exceeds the 64 MiB size limit.");
        Directory.CreateDirectory(destDir);

        var isMod = MatchesKind(project.Kind, "mod");
        var fileName = isMod
            ? $"{SanitizeTag(project.Id)}.hmod"
            : (!string.IsNullOrWhiteSpace(version.File.Name) ? Path.GetFileName(version.File.Name) : $"{SanitizeTag(project.Id)}.zip");
        var targetPath = Path.Combine(destDir, fileName);
        var tmpPath = targetPath + ".part";

        using (var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength ?? version.File.Size;
            if (total > MaxPackageBytes)
                throw new InvalidOperationException($"Package '{project.Name}' exceeds the 64 MiB size limit.");

            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            using var sha = SHA256.Create();
            var buf = new byte[81920];
            long readTotal = 0;
            int read;
            while ((read = await src.ReadAsync(buf, ct)) > 0)
            {
                readTotal += read;
                if (readTotal > MaxPackageBytes)
                    throw new InvalidOperationException($"Package '{project.Name}' exceeds the 64 MiB size limit.");

                await dst.WriteAsync(buf.AsMemory(0, read), ct);
                sha.TransformBlock(buf, 0, read, null, 0);
                if (total > 0)
                    progress?.Report(Math.Clamp(readTotal * 100.0 / total, 0, 100));
            }
            sha.TransformFinalBlock([], 0, 0);
            var actualSha = Convert.ToHexString(sha.Hash!).ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(version.File.Sha256) &&
                !string.Equals(actualSha, version.File.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                dst.Close();
                try { File.Delete(tmpPath); } catch { }
                throw new InvalidDataException($"SHA-256 verification failed for '{project.Name}' (expected {version.File.Sha256}, got {actualSha}).");
            }
        }

        File.Move(tmpPath, targetPath, true);

        if (isMod && !string.IsNullOrWhiteSpace(instanceMinecraftDir))
            SetModEnabled(instanceMinecraftDir, project.Id, true);

        foreach (var dep in version.Requires)
        {
            if (string.IsNullOrWhiteSpace(dep.Id) || visited.Contains(dep.Id)) continue;
            var depProj = index.Projects.FirstOrDefault(p => string.Equals(p.Id, dep.Id, StringComparison.OrdinalIgnoreCase));
            if (depProj == null) continue;
            var depVer = (!string.IsNullOrWhiteSpace(dep.Version)
                             ? depProj.Versions.FirstOrDefault(v => string.Equals(v.Version, dep.Version, StringComparison.OrdinalIgnoreCase))
                             : null)
                         ?? depProj.Versions.FirstOrDefault();
            if (depVer != null)
                await DownloadAndInstallVersionAsync(index, depProj, depVer, destDir, instanceMinecraftDir, null, visited, ct);
        }

        progress?.Report(100);
        return targetPath;
    }

    public static bool IsAllowedPackageUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return false;
        if (string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) &&
            uri.AbsolutePath.Contains("/releases/download/", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(uri.Host, "horizon-rust.github.io", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uri.Host, "playhorizon.dev", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private static string GetStr(JsonElement el, string prop, string fallback = "")
    {
        return el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? fallback
            : fallback;
    }

    private static long GetLong(JsonElement el, string prop)
    {
        return el.TryGetProperty(prop, out var v) && v.TryGetInt64(out var n) ? n : 0;
    }

    private static List<string> GetStrList(JsonElement el, string prop)
    {
        var list = new List<string>();
        if (el.TryGetProperty(prop, out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var s = item.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) list.Add(s);
                }
            }
        }
        return list;
    }
}