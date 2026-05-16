using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
namespace OrangLauncher.Backend
{
    public class InstallerProgressChangedEventArgs
    {
        public string Name { get; set; } = "";
        public int ProgressedTasks { get; set; }
        public int TotalTasks { get; set; }
    }
    public class ByteProgress
    {
        public long DownloadedBytes { get; set; }
        public long TotalBytes { get; set; }
    }
    public class MinecraftLauncher
    {
        private const string ManifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
        private const string ResourcesUrl = "https://resources.download.minecraft.net/";
        private const string LibrariesUrl = "https://libraries.minecraft.net/";
        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        private readonly MinecraftPath _path;
        public MinecraftLauncher(MinecraftPath path)
        {
            _path = path;
            _path.CreateDirectories();
        }
        public MinecraftLauncher() : this(new MinecraftPath()) { }
        public async Task<List<VersionManifestEntry>> GetAllVersionsAsync()
        {
            var json = await Http.GetStringAsync(ManifestUrl);
            var manifest = JsonSerializer.Deserialize<VersionManifest>(json);
            return manifest?.Versions ?? new List<VersionManifestEntry>();
        }
        public async Task InstallAsync(
            string versionId,
            IProgress<InstallerProgressChangedEventArgs>? fileProgress = null,
            IProgress<ByteProgress>? byteProgress = null,
            CancellationToken ct = default)
        {
            fileProgress?.Report(new InstallerProgressChangedEventArgs { Name = $"Resolving {versionId}..." });
            var meta = await GetVersionMetaAsync(versionId);
            if (meta == null)
                throw new Exception($"Version {versionId} not found");
            if (!string.IsNullOrEmpty(meta.InheritsFrom))
            {
                fileProgress?.Report(new InstallerProgressChangedEventArgs { Name = $"Installing base version {meta.InheritsFrom}..." });
                await InstallAsync(meta.InheritsFrom, fileProgress, byteProgress, ct);
            }
            meta = await MergeInheritanceAsync(meta);
            int totalTasks = 1 + (meta.Libraries?.Count ?? 0) + 1;
            int done = 0;
            if (meta.Downloads?.Client != null)
            {
                var jarPath = _path.GetVersionJarPath(versionId);
                Directory.CreateDirectory(Path.GetDirectoryName(jarPath)!);
                fileProgress?.Report(new InstallerProgressChangedEventArgs
                { Name = $"Downloading {versionId}.jar", ProgressedTasks = done, TotalTasks = totalTasks });
                await DownloadIfMissingAsync(jarPath, meta.Downloads.Client.Url, meta.Downloads.Client.Sha1, ct);
                done++;
            }
            if (meta.Libraries != null)
            {
                foreach (var lib in meta.Libraries)
                {
                    ct.ThrowIfCancellationRequested();
                    if (!IsLibraryAllowed(lib))
                    {
                        done++;
                        continue;
                    }
                    fileProgress?.Report(new InstallerProgressChangedEventArgs
                    { Name = $"Library: {lib.Name}", ProgressedTasks = done, TotalTasks = totalTasks });
                    await DownloadLibraryAsync(lib, ct);
                    if (lib.Natives != null)
                    {
                        await DownloadNativesAsync(lib, versionId, ct);
                    }
                    done++;
                }
            }
            if (meta.AssetIndex != null)
            {
                fileProgress?.Report(new InstallerProgressChangedEventArgs
                { Name = "Downloading assets...", ProgressedTasks = done, TotalTasks = totalTasks });
                await DownloadAssetsAsync(meta.AssetIndex, ct);
                done++;
            }
            if (meta.Logging != null && meta.Logging.TryGetValue("client", out var logCfg) && logCfg.File != null)
            {
                var logDir = Path.Combine(_path.Assets, "log_configs");
                Directory.CreateDirectory(logDir);
                var logFile = Path.Combine(logDir, logCfg.File.Id ?? "client.xml");
                if (logCfg.File.Url != null)
                    await DownloadIfMissingAsync(logFile, logCfg.File.Url, logCfg.File.Sha1, ct);
            }
            fileProgress?.Report(new InstallerProgressChangedEventArgs
            { Name = "Done", ProgressedTasks = totalTasks, TotalTasks = totalTasks });
        }
        public async Task<Process> InstallAndBuildProcessAsync(
            string versionId,
            MLaunchOption options,
            IProgress<InstallerProgressChangedEventArgs>? fileProgress = null,
            IProgress<ByteProgress>? byteProgress = null,
            CancellationToken ct = default)
        {
            await InstallAsync(versionId, fileProgress, byteProgress, ct);
            return await BuildProcessAsync(versionId, options);
        }
        public async Task<Process> BuildProcessAsync(string versionId, MLaunchOption options)
        {
            var meta = await GetVersionMetaAsync(versionId);
            if (meta == null)
                throw new Exception($"Version meta not found for {versionId}");
            meta = await MergeInheritanceAsync(meta);
            string effectiveGameDir = options.GameDirectory ?? _path.BasePath;
            Directory.CreateDirectory(effectiveGameDir);
            string javaPath = options.JavaPath ?? FindJava(meta.JavaVersion?.MajorVersion ?? 17);
            var effectiveOptions = options;
            if (Is32BitJava(javaPath) && options.MaximumRamMb > 512)
            {
                effectiveOptions = new MLaunchOption
                {
                    Session = options.Session,
                    MaximumRamMb = 512,
                    MinimumRamMb = Math.Min(256, options.MinimumRamMb),
                    JavaPath = options.JavaPath,
                    GameDirectory = options.GameDirectory,
                    GameLauncherName = options.GameLauncherName,
                    GameLauncherVersion = options.GameLauncherVersion,
                    VersionType = options.VersionType,
                    ScreenWidth = options.ScreenWidth,
                    ScreenHeight = options.ScreenHeight,
                    Fullscreen = options.Fullscreen,
                    ExtraJvmArguments = options.ExtraJvmArguments,
                    ExtraGameArguments = options.ExtraGameArguments
                };
                Debug.WriteLine($"[Launch] 32-bit JVM detected, capping RAM to 512MB");
            }
            string classpath = BuildClasspath(meta, versionId);
            string nativesDir = ExtractNatives(meta, versionId);
            var gameArgs = BuildGameArguments(meta, versionId, effectiveOptions, nativesDir, effectiveGameDir);
            var jvmArgs = BuildJvmArguments(meta, versionId, effectiveOptions, classpath, nativesDir);
            var psi = new ProcessStartInfo
            {
                FileName = javaPath,
                WorkingDirectory = effectiveGameDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = false
            };
            var allArgs = new List<string>();
            allArgs.AddRange(jvmArgs);
            allArgs.Add(meta.MainClass);
            allArgs.AddRange(gameArgs);
            psi.Arguments = string.Join(" ", allArgs.Select(EscapeArg));
            return new Process { StartInfo = psi };
        }
        private static bool Is32BitJava(string javaPath)
        {
            try
            {
                var lower = javaPath.ToLowerInvariant();
                if (lower.Contains("x86") || lower.Contains("i386") || lower.Contains("x86-32") ||
                    lower.Contains("jre_x86") || lower.Contains("jre-x86"))
                    return true;
                if (File.Exists(javaPath))
                {
                    using var fs = new FileStream(javaPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var br = new BinaryReader(fs);
                    if (br.ReadUInt16() != 0x5A4D) return false; 
                    fs.Seek(60, SeekOrigin.Begin);
                    int peOffset = br.ReadInt32();
                    fs.Seek(peOffset, SeekOrigin.Begin);
                    if (br.ReadUInt32() != 0x00004550) return false; 
                    ushort machine = br.ReadUInt16();
                    return machine == 0x014C;
                }
            }
            catch { }
            return false;
        }
        public async Task<VersionMeta?> GetVersionMetaAsync(string versionId)
        {
            var localPath = _path.GetVersionJsonPath(versionId);
            if (File.Exists(localPath))
            {
                var json = await File.ReadAllTextAsync(localPath);
                return JsonSerializer.Deserialize<VersionMeta>(json);
            }
            var versionDir = _path.GetVersionDir(versionId);
            if (Directory.Exists(versionDir))
            {
                var jsonFiles = Directory.GetFiles(versionDir, "*.json");
                if (jsonFiles.Length > 0)
                {
                    var json = await File.ReadAllTextAsync(jsonFiles[0]);
                    return JsonSerializer.Deserialize<VersionMeta>(json);
                }
            }
            var manifestJson = await Http.GetStringAsync(ManifestUrl);
            var manifest = JsonSerializer.Deserialize<VersionManifest>(manifestJson);
            var entry = manifest?.Versions?.FirstOrDefault(v =>
                v.Id.Equals(versionId, StringComparison.OrdinalIgnoreCase));
            if (entry == null) return null;
            var versionJson = await Http.GetStringAsync(entry.Url);
            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
            await File.WriteAllTextAsync(localPath, versionJson);
            return JsonSerializer.Deserialize<VersionMeta>(versionJson);
        }
        private async Task<VersionMeta> MergeInheritanceAsync(VersionMeta meta)
        {
            if (string.IsNullOrEmpty(meta.InheritsFrom))
                return meta;
            var parent = await GetVersionMetaAsync(meta.InheritsFrom);
            if (parent == null)
                throw new Exception($"Cannot resolve parent version: {meta.InheritsFrom}");
            parent = await MergeInheritanceAsync(parent);
            if (string.IsNullOrEmpty(meta.MainClass))
                meta.MainClass = parent.MainClass;
            if (meta.Downloads == null)
                meta.Downloads = parent.Downloads;
            if (meta.AssetIndex == null)
                meta.AssetIndex = parent.AssetIndex;
            if (string.IsNullOrEmpty(meta.Assets))
                meta.Assets = parent.Assets;
            if (meta.JavaVersion == null)
                meta.JavaVersion = parent.JavaVersion;
            if (meta.Arguments == null && meta.MinecraftArguments == null)
            {
                meta.Arguments = parent.Arguments;
                meta.MinecraftArguments = parent.MinecraftArguments;
            }
            else if (meta.Arguments != null && parent.Arguments != null)
            {
                if (parent.Arguments.Jvm != null)
                {
                    var mergedJvm = new List<object>();
                    if (meta.Arguments.Jvm != null) mergedJvm.AddRange(meta.Arguments.Jvm);
                    mergedJvm.AddRange(parent.Arguments.Jvm);
                    meta.Arguments.Jvm = mergedJvm;
                }
                if ((meta.Arguments.Game == null || meta.Arguments.Game.Count == 0) && parent.Arguments.Game != null)
                {
                    meta.Arguments.Game = parent.Arguments.Game;
                }
                else if (meta.Arguments.Game != null && meta.Arguments.Game.Count > 0 && parent.Arguments.Game != null)
                {
                    var mergedGame = new List<object>();
                    mergedGame.AddRange(meta.Arguments.Game);
                    mergedGame.AddRange(parent.Arguments.Game);
                    meta.Arguments.Game = mergedGame;
                }
            }
            if (meta.Logging == null)
                meta.Logging = parent.Logging;
            var merged = new List<VersionLibrary>();
            if (meta.Libraries != null) merged.AddRange(meta.Libraries);
            if (parent.Libraries != null) merged.AddRange(parent.Libraries);
            meta.Libraries = merged;
            meta.InheritsFrom = null;
            return meta;
        }
        private async Task DownloadLibraryAsync(VersionLibrary lib, CancellationToken ct)
        {
            if (lib.Downloads?.Artifact != null)
            {
                var artifact = lib.Downloads.Artifact;
                if (!string.IsNullOrEmpty(artifact.Path) && !string.IsNullOrEmpty(artifact.Url))
                {
                    var destPath = Path.Combine(_path.Libraries, artifact.Path.Replace('/', Path.DirectorySeparatorChar));
                    await DownloadIfMissingAsync(destPath, artifact.Url, artifact.Sha1, ct);
                }
            }
            else if (!string.IsNullOrEmpty(lib.Name))
            {
                var parts = lib.Name.Split(':');
                if (parts.Length >= 3)
                {
                    var group = parts[0].Replace('.', '/');
                    var artifact = parts[1];
                    var version = parts[2];
                    var classifier = parts.Length > 3 ? $"-{parts[3]}" : "";
                    var fileName = $"{artifact}-{version}{classifier}.jar";
                    var path = $"{group}/{artifact}/{version}/{fileName}";
                    var destPath = Path.Combine(_path.Libraries, path.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(destPath))
                    {
                        string? baseUrl = lib.Url?.TrimEnd('/');
                        var urls = new List<string>();
                        if (!string.IsNullOrEmpty(baseUrl))
                            urls.Add($"{baseUrl}/{path}");
                        urls.Add($"{LibrariesUrl}{path}");
                        urls.Add($"https://maven.fabricmc.net/{path}");
                        urls.Add($"https://maven.quiltmc.org/repository/release/{path}");
                        urls.Add($"https://maven.minecraftforge.net/{path}");
                        urls.Add($"https://maven.neoforged.net/releases/{path}");
                        bool downloaded = false;
                        foreach (var url in urls)
                        {
                            try
                            {
                                await DownloadIfMissingAsync(destPath, url, null, ct);
                                downloaded = true;
                                break;
                            }
                            catch {}
                        }
                        if (!downloaded)
                        {
                            Debug.WriteLine($"[WARN] Could not download library: {lib.Name}");
                        }
                    }
                }
            }
        }
        private async Task DownloadNativesAsync(VersionLibrary lib, string versionId, CancellationToken ct)
        {
            if (lib.Natives == null || lib.Downloads?.Classifiers == null) return;
            string osKey = GetOsNativeKey();
            if (!lib.Natives.TryGetValue(osKey, out var classifierKey)) return;
            classifierKey = classifierKey.Replace("${arch}",
                RuntimeInformation.OSArchitecture == Architecture.X64 ? "64" : "32");
            if (!lib.Downloads.Classifiers.TryGetValue(classifierKey, out var artifact)) return;
            if (string.IsNullOrEmpty(artifact.Path) || string.IsNullOrEmpty(artifact.Url)) return;
            var destPath = Path.Combine(_path.Libraries, artifact.Path.Replace('/', Path.DirectorySeparatorChar));
            await DownloadIfMissingAsync(destPath, artifact.Url, artifact.Sha1, ct);
        }
        private async Task DownloadAssetsAsync(AssetIndexInfo indexInfo, CancellationToken ct)
        {
            var indexPath = Path.Combine(_path.AssetsIndexes, $"{indexInfo.Id}.json");
            await DownloadIfMissingAsync(indexPath, indexInfo.Url, indexInfo.Sha1, ct);
            var indexJson = await File.ReadAllTextAsync(indexPath, ct);
            var assetIndex = JsonSerializer.Deserialize<AssetIndex>(indexJson);
            if (assetIndex?.Objects == null) return;
            var semaphore = new SemaphoreSlim(10);
            var tasks = new List<Task>();
            foreach (var (_, obj) in assetIndex.Objects)
            {
                ct.ThrowIfCancellationRequested();
                var hash = obj.Hash;
                var prefix = hash[..2];
                var destPath = Path.Combine(_path.AssetsObjects, prefix, hash);
                var url = $"{ResourcesUrl}{prefix}/{hash}";
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync(ct);
                    try
                    {
                        await DownloadIfMissingAsync(destPath, url, hash, ct);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, ct));
            }
            await Task.WhenAll(tasks);
        }
        private string ExtractNatives(VersionMeta meta, string versionId)
        {
            var nativesDir = _path.GetNativesDir(versionId);
            Directory.CreateDirectory(nativesDir);
            if (meta.Libraries == null) return nativesDir;
            string osKey = GetOsNativeKey();
            foreach (var lib in meta.Libraries)
            {
                if (!IsLibraryAllowed(lib)) continue;
                if (lib.Natives == null || lib.Downloads?.Classifiers == null) continue;
                if (!lib.Natives.TryGetValue(osKey, out var classifierKey)) continue;
                classifierKey = classifierKey.Replace("${arch}",
                    RuntimeInformation.OSArchitecture == Architecture.X64 ? "64" : "32");
                if (!lib.Downloads.Classifiers.TryGetValue(classifierKey, out var artifact)) continue;
                if (string.IsNullOrEmpty(artifact.Path)) continue;
                var jarPath = Path.Combine(_path.Libraries, artifact.Path.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(jarPath)) continue;
                try
                {
                    using var zip = System.IO.Compression.ZipFile.OpenRead(jarPath);
                    foreach (var entry in zip.Entries)
                    {
                        if (entry.FullName.StartsWith("META-INF")) continue;
                        if (string.IsNullOrEmpty(entry.Name)) continue;
                        var dest = Path.Combine(nativesDir, entry.Name);
                        if (!File.Exists(dest))
                        {
                            entry.ExtractToFile(dest, true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WARN] Failed to extract natives from {jarPath}: {ex.Message}");
                }
            }
            return nativesDir;
        }
        private string BuildClasspath(VersionMeta meta, string versionId)
        {
            var separator = Path.PathSeparator.ToString();
            var entries = new List<string>();
            if (meta.Libraries != null)
            {
                foreach (var lib in meta.Libraries)
                {
                    if (!IsLibraryAllowed(lib)) continue;
                    string? libPath = null;
                    if (lib.Downloads?.Artifact?.Path != null)
                    {
                        libPath = Path.Combine(_path.Libraries,
                            lib.Downloads.Artifact.Path.Replace('/', Path.DirectorySeparatorChar));
                    }
                    else if (!string.IsNullOrEmpty(lib.Name))
                    {
                        libPath = ResolveLibraryPath(lib.Name);
                    }
                    if (libPath != null && File.Exists(libPath) && !entries.Contains(libPath))
                    {
                        entries.Add(libPath);
                      }
                  }
              }
            var clientJar = _path.GetVersionJarPath(versionId);
            if (File.Exists(clientJar))
            {
                entries.Add(clientJar);
            }
            var versionJsonPath = _path.GetVersionJsonPath(versionId);
            if (!File.Exists(versionJsonPath))
            {
                var versionDir = _path.GetVersionDir(versionId);
                var jsonFiles = Directory.Exists(versionDir) ? Directory.GetFiles(versionDir, "*.json") : Array.Empty<string>();
                if (jsonFiles.Length > 0) versionJsonPath = jsonFiles[0];
            }
            if (File.Exists(versionJsonPath))
            {
                try
                {
                    var raw = File.ReadAllText(versionJsonPath);
                    var rawMeta = JsonSerializer.Deserialize<VersionMeta>(raw);
                    if (!string.IsNullOrEmpty(rawMeta?.InheritsFrom))
                    {
                        var parentJar = _path.GetVersionJarPath(rawMeta.InheritsFrom);
                        if (File.Exists(parentJar) && !entries.Contains(parentJar))
                            entries.Add(parentJar);
                    }
                }
                catch { }
            }
            return string.Join(separator, entries);
        }
        private string? ResolveLibraryPath(string mavenName)
        {
            var parts = mavenName.Split(':');
            if (parts.Length < 3) return null;
            var group = parts[0].Replace('.', Path.DirectorySeparatorChar);
            var artifact = parts[1];
            var version = parts[2];
            var classifier = parts.Length > 3 ? $"-{parts[3]}" : "";
            var fileName = $"{artifact}-{version}{classifier}.jar";
            return Path.Combine(_path.Libraries, group, artifact, version, fileName);
        }
        private List<string> BuildGameArguments(VersionMeta meta, string versionId, MLaunchOption options, string nativesDir, string effectiveGameDir)
        {
            var args = new List<string>();
            var session = options.Session ?? MSession.CreateOffline("Player");
            var replacements = new Dictionary<string, string>
            {
                {"${auth_player_name}", session.Username},
                {"${version_name}", versionId},
                {"${game_directory}", effectiveGameDir},
                {"${assets_root}", _path.Assets},
                {"${assets_index_name}", meta.AssetIndex?.Id ?? meta.Assets ?? versionId},
                {"${auth_uuid}", session.Uuid.Replace("-", "")},
                {"${auth_access_token}", session.AccessToken},
                {"${clientid}", ""},
                {"${auth_xuid}", ""},
                {"${user_type}", string.IsNullOrEmpty(session.AccessToken) || session.AccessToken == "0" ? "legacy" : "msa"},
                {"${version_type}", options.VersionType ?? meta.Type ?? "release"},
                {"${user_properties}", "{}"},
                {"${resolution_width}", (options.ScreenWidth ?? 854).ToString()},
                {"${resolution_height}", (options.ScreenHeight ?? 480).ToString()},
                {"${launcher_name}", options.GameLauncherName ?? "OrangLauncher"},
                {"${launcher_version}", options.GameLauncherVersion ?? "1.0"},
                {"${quickPlayPath}", ""},
                {"${quickPlaySingleplayer}", ""},
                {"${quickPlayMultiplayer}", ""},
                {"${quickPlayRealms}", ""},
                {"${auth_session}", session.AccessToken},
                {"${game_assets}", Path.Combine(_path.Assets, "virtual", "legacy")},
                {"${auth_legacy_token}", session.AccessToken},
            };
            if (meta.Arguments?.Game != null)
            {
                foreach (var arg in meta.Arguments.Game)
                {
                    if (arg is JsonElement elem)
                    {
                        if (elem.ValueKind == JsonValueKind.String)
                        {
                            args.Add(ReplaceTemplates(elem.GetString()!, replacements));
                        }
                        else if (elem.ValueKind == JsonValueKind.Object)
                        {
                        }
                    }
                }
            }
            else if (!string.IsNullOrEmpty(meta.MinecraftArguments))
            {
                var parts = meta.MinecraftArguments.Split(' ');
                foreach (var part in parts)
                {
                    args.Add(ReplaceTemplates(part, replacements));
                }
            }
            if (options.ExtraGameArguments != null)
            {
                foreach (var arg in options.ExtraGameArguments)
                    args.Add(arg.Value);
            }
            return args;
        }
        private List<string> BuildJvmArguments(VersionMeta meta, string versionId, MLaunchOption options, string classpath, string nativesDir)
        {
            var args = new List<string>();
            args.Add($"-Xms{options.MinimumRamMb}m");
            args.Add($"-Xmx{options.MaximumRamMb}m");
            if (options.ExtraJvmArguments != null)
            {
                foreach (var arg in options.ExtraJvmArguments)
                {
                    if (arg.Value != "--sun-misc-unsafe-memory-access=allow" && !arg.Value.Contains("System.Collections.Generic.List"))
                    {
                        args.Add(arg.Value);
                    }
                }
            }
            var replacements = new Dictionary<string, string>
            {
                {"${natives_directory}", nativesDir},
                {"${launcher_name}", options.GameLauncherName ?? "OrangLauncher"},
                {"${launcher_version}", options.GameLauncherVersion ?? "1.0"},
                {"${classpath}", classpath},
                {"${classpath_separator}", Path.PathSeparator.ToString()},
                {"${library_directory}", _path.Libraries},
                {"${version_name}", versionId},
            };
            if (meta.Arguments?.Jvm != null)
            {
                foreach (var arg in meta.Arguments.Jvm)
                {
                    if (arg is JsonElement elem)
                    {
                        if (elem.ValueKind == JsonValueKind.String)
                        {
                            var argStr = ReplaceTemplates(elem.GetString()!, replacements);
                            if (argStr != "--sun-misc-unsafe-memory-access=allow" && !argStr.Contains("System.Collections.Generic.List"))
                                args.Add(argStr);
                        }
                        else if (elem.ValueKind == JsonValueKind.Object)
                        {
                            if (EvaluateRuleArg(elem, out var values))
                            {
                                foreach (var val in values)
                                {
                                    var argStr = ReplaceTemplates(val, replacements);
                                    if (argStr != "--sun-misc-unsafe-memory-access=allow" && !argStr.Contains("System.Collections.Generic.List"))
                                        args.Add(argStr);
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                args.Add($"-Djava.library.path={nativesDir}");
                args.Add("-cp");
                args.Add(classpath);
            }
            if (meta.Logging != null && meta.Logging.TryGetValue("client", out var logCfg))
            {
                if (logCfg.File != null && logCfg.Argument != null)
                {
                    var logFile = Path.Combine(_path.Assets, "log_configs", logCfg.File.Id ?? "client.xml");
                    if (File.Exists(logFile))
                    {
                        args.Add(logCfg.Argument.Replace("${path}", logFile));
                    }
                }
            }
            return args;
        }
        private bool EvaluateRuleArg(JsonElement elem, out List<string> values)
        {
            values = new List<string>();
            try
            {
                if (!elem.TryGetProperty("rules", out var rules)) return false;
                bool allowed = false;
                foreach (var rule in rules.EnumerateArray())
                {
                    var action = rule.GetProperty("action").GetString();
                    bool matches = true;
                    if (rule.TryGetProperty("os", out var os))
                    {
                        if (os.TryGetProperty("name", out var osName))
                        {
                            var currentOs = GetCurrentOs();
                            matches = osName.GetString() == currentOs;
                        }
                        if (os.TryGetProperty("arch", out var arch))
                        {
                            var currentArch = RuntimeInformation.OSArchitecture == Architecture.X64 ? "x86" : "x86_32";
                            matches = matches && arch.GetString() == currentArch;
                        }
                    }
                    if (action == "allow")
                        allowed = matches;
                    else if (action == "disallow")
                        allowed = !matches;
                }
                if (!allowed) return false;
                if (elem.TryGetProperty("value", out var value))
                {
                    if (value.ValueKind == JsonValueKind.String)
                        values.Add(value.GetString()!);
                    else if (value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var v in value.EnumerateArray())
                            if (v.ValueKind == JsonValueKind.String)
                                values.Add(v.GetString()!);
                    }
                }
                return values.Count > 0;
            }
            catch
            {
                return false;
            }
        }
        public static string FindJava(int majorVersion = 17)
        {
            var orangJavaDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OrangLauncher", "java");
            if (Directory.Exists(orangJavaDir))
            {
                var requestedVersionDir = Path.Combine(orangJavaDir, $"java-{majorVersion}");
                if (Directory.Exists(requestedVersionDir))
                {
                    try
                    {
                        var javaw = Directory.GetFiles(requestedVersionDir, "javaw.exe", SearchOption.AllDirectories).FirstOrDefault();
                        if (javaw != null) return javaw;
                    }
                    catch { }
                }
                try
                {
                    foreach (var subdir in Directory.GetDirectories(orangJavaDir).OrderByDescending(d => d))
                    {
                        var javaw = Directory.GetFiles(subdir, "javaw.exe", SearchOption.AllDirectories).FirstOrDefault();
                        if (javaw != null) return javaw;
                    }
                }
                catch { }
            }
            var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(javaHome))
            {
                var javawPath = Path.Combine(javaHome, "bin", "javaw.exe");
                var javaPath = Path.Combine(javaHome, "bin", "java.exe");
                if (File.Exists(javawPath)) return javawPath;
                if (File.Exists(javaPath)) return javaPath;
            }
            var mcRuntime = Path.Combine(MinecraftPath.GetDefaultPath(), "runtime");
            if (Directory.Exists(mcRuntime))
            {
                var componentName = majorVersion >= 21 ? "java-runtime-delta"
                    : majorVersion >= 17 ? "java-runtime-gamma"
                    : majorVersion >= 16 ? "java-runtime-beta"
                    : "java-runtime-alpha";
                var searchPaths = new[]
                {
                    Path.Combine(mcRuntime, componentName, "windows-x64", componentName, "bin", "javaw.exe"),
                    Path.Combine(mcRuntime, componentName, "windows-x86", componentName, "bin", "javaw.exe"),
                    Path.Combine(mcRuntime, "java-runtime-gamma", "windows-x64", "java-runtime-gamma", "bin", "javaw.exe"),
                    Path.Combine(mcRuntime, "java-runtime-beta", "windows-x64", "java-runtime-beta", "bin", "javaw.exe"),
                };
                foreach (var p in searchPaths)
                    if (File.Exists(p)) return p;
                try
                {
                    foreach (var component in Directory.GetDirectories(mcRuntime))
                    {
                        var javaExe = Directory.GetFiles(component, "javaw.exe", SearchOption.AllDirectories).FirstOrDefault();
                        if (javaExe != null) return javaExe;
                    }
                }
                catch { }
            }
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var commonDirs = new[]
            {
                Path.Combine(programFiles, "Java"),
                Path.Combine(programFilesX86, "Java"),
                Path.Combine(programFiles, "Eclipse Adoptium"),
                Path.Combine(programFiles, "AdoptOpenJDK"),
                Path.Combine(programFiles, "Zulu"),
                Path.Combine(programFiles, "Microsoft"),
                Path.Combine(localAppData, "Programs", "Eclipse Adoptium"),
            };
            foreach (var dir in commonDirs)
            {
                if (!Directory.Exists(dir)) continue;
                try
                {
                    foreach (var javaDir in Directory.GetDirectories(dir).OrderByDescending(d => d))
                    {
                        var javaw = Path.Combine(javaDir, "bin", "javaw.exe");
                        if (File.Exists(javaw)) return javaw;
                    }
                }
                catch { }
            }
            var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? Array.Empty<string>();
            foreach (var dir in pathDirs)
            {
                var javaw = Path.Combine(dir, "javaw.exe");
                if (File.Exists(javaw)) return javaw;
                var java = Path.Combine(dir, "java.exe");
                if (File.Exists(java)) return java;
            }
            return "javaw.exe";
        }
        private static bool IsLibraryAllowed(VersionLibrary lib)
        {
            if (lib.Rules == null || lib.Rules.Count == 0)
                return true;
            bool allowed = false;
            var currentOs = GetCurrentOs();
            foreach (var rule in lib.Rules)
            {
                bool matches = true;
                if (rule.Os != null)
                {
                    if (rule.Os.Name != null)
                        matches = rule.Os.Name == currentOs;
                }
                if (rule.Action == "allow")
                {
                    if (rule.Os == null)
                        allowed = true;
                    else if (matches)
                        allowed = true;
                }
                else if (rule.Action == "disallow")
                {
                    if (matches)
                        allowed = false;
                }
            }
            return allowed;
        }
        private static string GetCurrentOs()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "windows";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "linux";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "osx";
            return "windows";
        }
        private static string GetOsNativeKey()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "windows";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "linux";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "osx";
            return "windows";
        }
        private static string ReplaceTemplates(string template, Dictionary<string, string> replacements)
        {
            foreach (var (key, value) in replacements)
            {
                template = template.Replace(key, value);
            }
            return template;
        }
        private static string EscapeArg(string arg)
        {
            if (string.IsNullOrEmpty(arg)) return "\"\"";
            if (arg.Contains(' ') || arg.Contains('"'))
            {
                return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            }
            return arg;
        }
        private static async Task DownloadIfMissingAsync(string destPath, string url, string? expectedSha1, CancellationToken ct)
        {
            if (File.Exists(destPath))
            {
                if (string.IsNullOrEmpty(expectedSha1))
                    return;
                var localSha1 = ComputeSha1(destPath);
                if (localSha1.Equals(expectedSha1, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(fileStream, ct);
        }
        private static string ComputeSha1(string filePath)
        {
            using var sha1 = SHA1.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha1.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}