using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace OrangLauncher.Backend
{
    public class FabricInstaller
    {
        private const string MetaApi = "https://meta.fabricmc.net/v2";
        private readonly HttpClient _http;
        public FabricInstaller(HttpClient? http = null)
        {
            _http = http ?? new HttpClient();
        }
        public async Task<List<string>> GetLoaderVersionsAsync(string mcVersion)
        {
            var url = $"{MetaApi}/versions/loader/{mcVersion}";
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var versions = new List<string>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var loaderVersion = item.GetProperty("loader").GetProperty("version").GetString();
                if (!string.IsNullOrEmpty(loaderVersion))
                    versions.Add(loaderVersion);
            }
            return versions;
        }
        public async Task<string> Install(string mcVersion, MinecraftPath path, string? loaderVersion = null)
        {
            if (string.IsNullOrEmpty(loaderVersion))
            {
                var versions = await GetLoaderVersionsAsync(mcVersion);
                loaderVersion = versions.FirstOrDefault()
                    ?? throw new Exception($"No Fabric loader versions found for MC {mcVersion}");
            }
            var versionJsonUrl = $"{MetaApi}/versions/loader/{mcVersion}/{loaderVersion}/profile/json";
            var versionJson = await _http.GetStringAsync(versionJsonUrl);
            using var doc = JsonDocument.Parse(versionJson);
            var versionId = doc.RootElement.GetProperty("id").GetString()
                ?? $"fabric-loader-{loaderVersion}-{mcVersion}";
            var versionDir = path.GetVersionDir(versionId);
            Directory.CreateDirectory(versionDir);
            var jsonPath = path.GetVersionJsonPath(versionId);
            await File.WriteAllTextAsync(jsonPath, versionJson);
            return versionId;
        }
    }
    public class QuiltInstaller
    {
        private const string MetaApi = "https://meta.quiltmc.org/v3";
        private readonly HttpClient _http;
        public QuiltInstaller(HttpClient? http = null)
        {
            _http = http ?? new HttpClient();
        }
        public async Task<List<string>> GetLoaderVersionsAsync(string mcVersion)
        {
            var url = $"{MetaApi}/versions/loader/{mcVersion}";
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var versions = new List<string>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var loaderVersion = item.GetProperty("loader").GetProperty("version").GetString();
                if (!string.IsNullOrEmpty(loaderVersion))
                    versions.Add(loaderVersion);
            }
            return versions;
        }
        public async Task<string> Install(string mcVersion, MinecraftPath path, string? loaderVersion = null)
        {
            if (string.IsNullOrEmpty(loaderVersion))
            {
                var versions = await GetLoaderVersionsAsync(mcVersion);
                loaderVersion = versions.FirstOrDefault()
                    ?? throw new Exception($"No Quilt loader versions found for MC {mcVersion}");
            }
            var versionJsonUrl = $"{MetaApi}/versions/loader/{mcVersion}/{loaderVersion}/profile/json";
            var versionJson = await _http.GetStringAsync(versionJsonUrl);
            using var doc = JsonDocument.Parse(versionJson);
            var versionId = doc.RootElement.GetProperty("id").GetString()
                ?? $"quilt-loader-{loaderVersion}-{mcVersion}";
            var versionDir = path.GetVersionDir(versionId);
            Directory.CreateDirectory(versionDir);
            var jsonPath = path.GetVersionJsonPath(versionId);
            await File.WriteAllTextAsync(jsonPath, versionJson);
            System.Diagnostics.Debug.WriteLine($"[Quilt] Installed version JSON: {jsonPath}");
            System.Diagnostics.Debug.WriteLine($"[Quilt] Version ID: {versionId}");
            return versionId;
        }
    }
    public class ForgeInstaller
    {
        private const string ForgePromosUrl = "https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json";
        private const string ForgeMavenBase = "https://maven.minecraftforge.net/net/minecraftforge/forge";
        private readonly MinecraftLauncher _launcher;
        private readonly HttpClient _http;
        public Action<string>? OnProgress { get; set; }
        public ForgeInstaller(MinecraftLauncher launcher, HttpClient? http = null)
        {
            _launcher = launcher;
            _http = http ?? new HttpClient();
        }
        public async Task<string?> GetRecommendedVersionAsync(string mcVersion)
        {
            var json = await _http.GetStringAsync(ForgePromosUrl);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("promos", out var promos))
            {
                if (promos.TryGetProperty($"{mcVersion}-recommended", out var rec))
                    return rec.GetString();
                if (promos.TryGetProperty($"{mcVersion}-latest", out var latest))
                    return latest.GetString();
            }
            return null;
        }
        public async Task<string> Install(string mcVersion, string? forgeVersion = null)
        {
            if (string.IsNullOrEmpty(forgeVersion))
            {
                forgeVersion = await GetRecommendedVersionAsync(mcVersion)
                    ?? throw new Exception($"No Forge versions found for MC {mcVersion}");
            }
            var fullVersion = $"{mcVersion}-{forgeVersion}";
            var installerUrl = $"{ForgeMavenBase}/{fullVersion}/forge-{fullVersion}-installer.jar";
            var forgeVersionId = $"{mcVersion}-forge-{forgeVersion}";
            var minecraftDir = Managers.PlatformPaths.GetMinecraftDir();
            var versionsDir = Path.Combine(minecraftDir, "versions");
            var librariesDir = Path.Combine(minecraftDir, "libraries");
            Directory.CreateDirectory(versionsDir);
            Directory.CreateDirectory(librariesDir);
            var tempDir = Path.Combine(Path.GetTempPath(), $"oranglauncher_forge_{fullVersion}");
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
            Directory.CreateDirectory(tempDir);
            var installerPath = Path.Combine(tempDir, $"forge-{fullVersion}-installer.jar");
            System.Diagnostics.Debug.WriteLine($"[Forge] Downloading installer from {installerUrl}");
            if (!File.Exists(installerPath))
            {
                var data = await _http.GetByteArrayAsync(installerUrl);
                await File.WriteAllBytesAsync(installerPath, data);
            }
            try
            {
                using var zip = ZipFile.OpenRead(installerPath);
                string? installProfileJson = null;
                var installProfileEntry = zip.GetEntry("install_profile.json");
                if (installProfileEntry != null)
                {
                    using var stream = installProfileEntry.Open();
                    using var reader = new StreamReader(stream);
                    installProfileJson = await reader.ReadToEndAsync();
                }
                string actualMcVersion = mcVersion;
                if (installProfileJson != null)
                {
                    using var profileDoc = JsonDocument.Parse(installProfileJson);
                    var root = profileDoc.RootElement;
                    if (root.TryGetProperty("minecraft", out var mcProp))
                    {
                        actualMcVersion = mcProp.GetString() ?? mcVersion;
                    }
                    else if (root.TryGetProperty("install", out var installProp) &&
                             installProp.TryGetProperty("minecraft", out var mcProp2))
                    {
                        actualMcVersion = mcProp2.GetString() ?? mcVersion;
                    }
                }
                string? clientJsonText = null;
                var versionJsonEntry = zip.GetEntry("version.json");
                if (versionJsonEntry != null)
                {
                    using var stream = versionJsonEntry.Open();
                    using var reader = new StreamReader(stream);
                    clientJsonText = await reader.ReadToEndAsync();
                }
                else if (installProfileJson != null)
                {
                    using var profileDoc = JsonDocument.Parse(installProfileJson);
                    if (profileDoc.RootElement.TryGetProperty("versionInfo", out var versionInfo))
                    {
                        clientJsonText = versionInfo.GetRawText();
                    }
                }
                if (clientJsonText == null)
                {
                    throw new Exception("Forge installer does not contain version.json or versionInfo");
                }
                var clientJsonNode = JsonNode.Parse(clientJsonText);
                if (clientJsonNode != null)
                {
                    clientJsonNode["id"] = forgeVersionId;
                }
                var versionDir = Path.Combine(versionsDir, forgeVersionId);
                Directory.CreateDirectory(versionDir);
                var versionJsonPath = Path.Combine(versionDir, $"{forgeVersionId}.json");
                await File.WriteAllTextAsync(versionJsonPath, clientJsonNode?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? clientJsonText);
                System.Diagnostics.Debug.WriteLine($"[Forge] Wrote version JSON: {versionJsonPath}");
                var forgeLibPath = Path.Combine(librariesDir, "net", "minecraftforge", "forge", fullVersion);
                Directory.CreateDirectory(forgeLibPath);
                TryExtractFromZip(zip, $"maven/net/minecraftforge/forge/{fullVersion}/forge-{fullVersion}-universal.jar",
                    Path.Combine(forgeLibPath, $"forge-{fullVersion}-universal.jar"));
                TryExtractFromZip(zip, $"forge-{fullVersion}-universal.jar",
                    Path.Combine(forgeLibPath, $"forge-{fullVersion}.jar"));
                TryExtractFromZip(zip, $"maven/net/minecraftforge/forge/{fullVersion}/forge-{fullVersion}.jar",
                    Path.Combine(forgeLibPath, $"forge-{fullVersion}.jar"));
                TryExtractFromZip(zip, $"maven/net/minecraftforge/forge/{fullVersion}/forge-{fullVersion}-client.jar",
                    Path.Combine(forgeLibPath, $"forge-{fullVersion}-client.jar"));
                foreach (var entry in zip.Entries)
                {
                    if (entry.FullName.StartsWith("maven/") && !string.IsNullOrEmpty(entry.Name))
                    {
                        var relativePath = entry.FullName["maven/".Length..];
                        var destPath = Path.Combine(librariesDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
                        var destDir = Path.GetDirectoryName(destPath);
                        if (destDir != null)
                        {
                            Directory.CreateDirectory(destDir);
                            if (!File.Exists(destPath))
                            {
                                entry.ExtractToFile(destPath, false);
                            }
                        }
                    }
                }
                foreach (var entry in zip.Entries)
                {
                    if (entry.FullName.StartsWith("data/") && !string.IsNullOrEmpty(entry.Name))
                    {
                        var relativePath = entry.FullName["data/".Length..];
                        var destPath = Path.Combine(tempDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
                        var destDir = Path.GetDirectoryName(destPath);
                        if (destDir != null) Directory.CreateDirectory(destDir);
                        if (!File.Exists(destPath))
                        {
                            entry.ExtractToFile(destPath, false);
                        }
                        System.Diagnostics.Debug.WriteLine($"[Forge] Extracted data: {entry.FullName} -> {destPath}");
                    }
                }
                var lzmaPath = Path.Combine(tempDir, "client.lzma");
                if (installProfileJson != null)
                {
                    using var profileDoc = JsonDocument.Parse(installProfileJson);
                    var root = profileDoc.RootElement;
                    if (root.TryGetProperty("libraries", out var libs))
                    {
                        await InstallLibrariesFromJsonAsync(libs, minecraftDir);
                    }
                }
                if (clientJsonText != null)
                {
                    using var clientDoc = JsonDocument.Parse(clientJsonText);
                    if (clientDoc.RootElement.TryGetProperty("libraries", out var clientLibs))
                    {
                        await InstallLibrariesFromJsonAsync(clientLibs, minecraftDir);
                    }
                }
                if (installProfileJson != null)
                {
                    using var profileDoc = JsonDocument.Parse(installProfileJson);
                    if (profileDoc.RootElement.TryGetProperty("processors", out var processors))
                    {
                        await RunForgeProcessorsAsync(profileDoc.RootElement, minecraftDir, tempDir, lzmaPath, installerPath, actualMcVersion, fullVersion, OnProgress);
                    }
                }
                var forgeJarPath = Path.Combine(versionDir, $"{forgeVersionId}.jar");
                if (!File.Exists(forgeJarPath))
                {
                    var baseJarPath = Path.Combine(versionsDir, actualMcVersion, $"{actualMcVersion}.jar");
                    if (File.Exists(baseJarPath))
                    {
                        File.Copy(baseJarPath, forgeJarPath, false);
                        System.Diagnostics.Debug.WriteLine($"[Forge] Copied base client.jar to {forgeJarPath}");
                    }
                }
                System.Diagnostics.Debug.WriteLine($"[Forge] Installation complete: {forgeVersionId}");
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
            return forgeVersionId;
        }
        private static void TryExtractFromZip(ZipArchive zip, string entryName, string destPath)
        {
            var entry = zip.GetEntry(entryName);
            if (entry != null)
            {
                var dir = Path.GetDirectoryName(destPath);
                if (dir != null) Directory.CreateDirectory(dir);
                if (!File.Exists(destPath))
                {
                    entry.ExtractToFile(destPath, false);
                }
                System.Diagnostics.Debug.WriteLine($"[Forge] Extracted: {entryName} -> {destPath}");
            }
        }
        private async Task InstallLibrariesFromJsonAsync(JsonElement librariesArray, string minecraftDir)
        {
            var librariesDir = Path.Combine(minecraftDir, "libraries");
            foreach (var lib in librariesArray.EnumerateArray())
            {
                string? name = null;
                string? url = null;
                if (lib.TryGetProperty("name", out var nameProp))
                    name = nameProp.GetString();
                if (lib.TryGetProperty("downloads", out var downloads) &&
                    downloads.TryGetProperty("artifact", out var artifact))
                {
                    string? artifactPath = null;
                    string? artifactUrl = null;
                    if (artifact.TryGetProperty("path", out var pathProp))
                        artifactPath = pathProp.GetString();
                    if (artifact.TryGetProperty("url", out var urlProp))
                        artifactUrl = urlProp.GetString();
                    if (!string.IsNullOrEmpty(artifactPath))
                    {
                        var destPath = Path.Combine(librariesDir, artifactPath.Replace('/', Path.DirectorySeparatorChar));
                        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                        if (!string.IsNullOrEmpty(artifactUrl) && !File.Exists(destPath))
                        {
                            try
                            {
                                var data = await _http.GetByteArrayAsync(artifactUrl);
                                await File.WriteAllBytesAsync(destPath, data);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[Forge] Failed to download library {artifactUrl}: {ex.Message}");
                            }
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(name))
                {
                    if (lib.TryGetProperty("url", out var libUrlProp))
                        url = libUrlProp.GetString();
                    var parts = name.Split(':');
                    if (parts.Length >= 3)
                    {
                        var group = parts[0].Replace('.', '/');
                        var artifactName = parts[1];
                        var version = parts[2];
                        var classifier = parts.Length > 3 ? $"-{parts[3]}" : "";
                        var fileEnd = "jar";
                        if (version.Contains('@'))
                        {
                            var split = version.Split('@');
                            version = split[0];
                            fileEnd = split[1];
                        }
                        var fileName = $"{artifactName}-{version}{classifier}.{fileEnd}";
                        var path = $"{group}/{artifactName}/{version}/{fileName}";
                        var destPath = Path.Combine(librariesDir, path.Replace('/', Path.DirectorySeparatorChar));
                        if (!File.Exists(destPath))
                        {
                            var baseUrl = url?.TrimEnd('/') ?? "https://libraries.minecraft.net";
                            var urls = new[]
                            {
                                $"{baseUrl}/{path}",
                                $"https://libraries.minecraft.net/{path}",
                                $"https://maven.minecraftforge.net/{path}",
                                $"https://maven.neoforged.net/releases/{path}"
                            };
                            foreach (var downloadUrl in urls)
                            {
                                try
                                {
                                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                                    var data = await _http.GetByteArrayAsync(downloadUrl);
                                    await File.WriteAllBytesAsync(destPath, data);
                                    break;
                                }
                                catch
                                {
                                }
                            }
                        }
                    }
                }
            }
        }
        private async Task RunForgeProcessorsAsync(JsonElement installProfile, string minecraftDir, string tempDir, string lzmaPath, string installerPath, string mcVersion, string forgeFullVersion, Action<string>? onProgress)
        {
            if (!installProfile.TryGetProperty("processors", out var processors))
                return;
            var librariesDir = Path.Combine(minecraftDir, "libraries");
            var separator = Path.PathSeparator.ToString();
            var argumentVars = new Dictionary<string, string>
            {
                ["{MINECRAFT_JAR}"] = Path.Combine(minecraftDir, "versions", mcVersion, $"{mcVersion}.jar"),
                ["{INSTALLER}"] = installerPath,
                ["{BINPATCH}"] = lzmaPath,
                ["{SIDE}"] = "client"
            };
            var rootPath = Path.Combine(tempDir, "root");
            Directory.CreateDirectory(rootPath);
            argumentVars["{ROOT}"] = rootPath;
            if (installProfile.TryGetProperty("data", out var dataSection))
            {
                foreach (var dataProp in dataSection.EnumerateObject())
                {
                    if (dataProp.Value.TryGetProperty("client", out var clientVal))
                    {
                        var clientStr = clientVal.GetString() ?? "";
                        string resolvedValue;
                        if (clientStr.StartsWith('[') && clientStr.EndsWith(']'))
                        {
                            resolvedValue = ResolveLibraryPath(clientStr[1..^1], librariesDir);
                        }
                        else if (clientStr.StartsWith("/data/"))
                        {
                            var fileName = clientStr["/data/".Length..];
                            resolvedValue = Path.Combine(tempDir, fileName);
                        }
                        else
                        {
                            resolvedValue = clientStr;
                        }
                        var key = $"{{{dataProp.Name}}}";
                        if (!argumentVars.ContainsKey(key))
                            argumentVars[key] = resolvedValue;
                    }
                }
            }
            int javaVersion = 8;
            var mcParts = mcVersion.Split('.');
            if (mcParts.Length >= 2 && int.TryParse(mcParts[1], out int minor))
            {
                if (minor >= 21)
                    javaVersion = 21;
                else if (minor >= 17)
                    javaVersion = 17;
            }
            var javaPath = MinecraftLauncher.FindJava(javaVersion);
            System.Diagnostics.Debug.WriteLine($"[Forge] Using Java {javaVersion} at: {javaPath}");
            foreach (var (_, value) in argumentVars)
            {
                if (value.Length > 4 && (value.EndsWith(".jar") || value.EndsWith(".zip")))
                {
                    var dir = Path.GetDirectoryName(value);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        try { Directory.CreateDirectory(dir); } catch { }
                    }
                }
            }
            var mcJarPath = argumentVars["{MINECRAFT_JAR}"];
            if (!File.Exists(mcJarPath))
            {
                throw new Exception($"Forge processors require the vanilla client JAR at {mcJarPath} but it was not found. " +
                    "Please ensure the base Minecraft version is installed first.");
            }
            int processorFailures = 0;
            foreach (var processor in processors.EnumerateArray())
            {
                if (processor.TryGetProperty("sides", out var sides))
                {
                    bool hasClient = false;
                    foreach (var side in sides.EnumerateArray())
                    {
                        if (side.GetString() == "client") hasClient = true;
                    }
                    if (!hasClient) continue;
                }
                var jarName = processor.GetProperty("jar").GetString()!;
                System.Diagnostics.Debug.WriteLine($"[Forge] Running processor {jarName}");
                string label = jarName.Contains("binarypatcher") ? "Forge: Patching Minecraft client (this may take a few minutes)..."
                             : jarName.Contains("ForgeAutoRenamingTool") ? "Forge: Remapping Minecraft..."
                             : jarName.Contains("installertools") ? "Forge: Running installer tools..."
                             : $"Forge: Running {Path.GetFileName(jarName)}...";
                onProgress?.Invoke(label);
                var classpathParts = new List<string>();
                if (processor.TryGetProperty("classpath", out var classpath))
                {
                    foreach (var cp in classpath.EnumerateArray())
                    {
                        var cpStr = cp.GetString();
                        if (cpStr != null)
                            classpathParts.Add(ResolveLibraryPath(cpStr, librariesDir));
                    }
                }
                classpathParts.Add(ResolveLibraryPath(jarName, librariesDir));
                var classpathStr = string.Join(separator, classpathParts);
                var processorJarPath = ResolveLibraryPath(jarName, librariesDir);
                string mainClass;
                try
                {
                    mainClass = GetJarMainClass(processorJarPath);
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine($"[Forge] Could not read main class from {processorJarPath}, skipping");
                    continue;
                }
                var command = new List<string> { javaPath };
                if (javaVersion >= 9)
                {
                    command.Add("--add-opens"); command.Add("java.base/java.util=ALL-UNNAMED");
                    command.Add("--add-opens"); command.Add("java.base/java.lang=ALL-UNNAMED");
                    command.Add("--add-opens"); command.Add("java.base/java.lang.reflect=ALL-UNNAMED");
                    command.Add("--add-opens"); command.Add("java.base/java.io=ALL-UNNAMED");
                    command.Add("--add-opens"); command.Add("java.base/sun.security.provider=ALL-UNNAMED");
                }
                command.Add("-Xmx1g");
                command.AddRange(new[] { "-cp", classpathStr, mainClass });
                if (processor.TryGetProperty("args", out var args))
                {
                    foreach (var arg in args.EnumerateArray())
                    {
                        var argStr = arg.GetString() ?? "";
                        var resolved = argumentVars.GetValueOrDefault(argStr, argStr);
                        if (resolved.StartsWith('[') && resolved.EndsWith(']'))
                        {
                            command.Add(ResolveLibraryPath(resolved[1..^1], librariesDir));
                        }
                        else
                        {
                            command.Add(resolved);
                        }
                    }
                }
                for (int i = 0; i < command.Count; i++)
                {
                    foreach (var (key, value) in argumentVars)
                    {
                        command[i] = command[i].Replace(key, value);
                    }
                }
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = command[0],
                    Arguments = string.Join(" ", command.Skip(1).Select(a =>
                        a.Contains(' ') ? $"\"{a}\"" : a)),
                    WorkingDirectory = rootPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                try
                {
                    var process = System.Diagnostics.Process.Start(psi);
                    if (process != null)
                    {
                        var stdoutTask = process.StandardOutput.ReadToEndAsync();
                        var stderrTask = process.StandardError.ReadToEndAsync();
                        int timeoutMs = jarName.Contains("binarypatcher") ? 600_000 : 120_000;
                        using var cts = new System.Threading.CancellationTokenSource(timeoutMs);
                        try
                        {
                            await process.WaitForExitAsync(cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            try { process.Kill(); } catch { }
                            processorFailures++;
                            System.Diagnostics.Debug.WriteLine($"[Forge] Processor {jarName} timed out after {timeoutMs / 1000}s, killed");
                            continue;
                        }
                        var stdout = await stdoutTask;
                        var stderr = await stderrTask;
                        System.Diagnostics.Debug.WriteLine($"[Forge] Processor {jarName} exit code: {process.ExitCode}");
                        if (!string.IsNullOrWhiteSpace(stdout))
                            System.Diagnostics.Debug.WriteLine($"[Forge] Processor stdout: {stdout[..Math.Min(500, stdout.Length)]}");
                        if (!string.IsNullOrWhiteSpace(stderr))
                            System.Diagnostics.Debug.WriteLine($"[Forge] Processor stderr: {stderr[..Math.Min(500, stderr.Length)]}");
                        if (process.ExitCode != 0)
                        {
                            processorFailures++;
                            System.Diagnostics.Debug.WriteLine($"[Forge] WARNING: Processor {jarName} failed with exit code {process.ExitCode}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    processorFailures++;
                    System.Diagnostics.Debug.WriteLine($"[Forge] Processor {jarName} failed: {ex.Message}");
                }
            }
            if (processorFailures > 0)
            {
                throw new Exception($"Forge installation failed: {processorFailures} processor(s) failed. " +
                    $"Check debug output for details. Java used: {javaPath} (version {javaVersion})");
            }
        }
        private static string ResolveLibraryPath(string mavenName, string librariesDir)
        {
            var parts = mavenName.Split(':');
            if (parts.Length < 3) return mavenName;
            var group = parts[0].Replace('.', Path.DirectorySeparatorChar);
            var artifact = parts[1];
            var version = parts[2];
            var classifier = parts.Length > 3 ? $"-{parts[3]}" : "";
            var fileEnd = "jar";
            if (version.Contains('@'))
            {
                var split = version.Split('@');
                version = split[0];
                fileEnd = split[1];
            }
            var fileName = $"{artifact}-{version}{classifier}.{fileEnd}";
            return Path.Combine(librariesDir, group, artifact, version, fileName);
        }
        private static string GetJarMainClass(string jarPath)
        {
            using var zip = ZipFile.OpenRead(jarPath);
            var manifestEntry = zip.GetEntry("META-INF/MANIFEST.MF");
            if (manifestEntry == null)
                throw new Exception($"No MANIFEST.MF found in {jarPath}");
            using var stream = manifestEntry.Open();
            using var reader = new StreamReader(stream);
            var manifest = reader.ReadToEnd();
            foreach (var line in manifest.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("Main-Class:", StringComparison.OrdinalIgnoreCase))
                {
                    return trimmed["Main-Class:".Length..].Trim();
                }
            }
            throw new Exception($"No Main-Class found in {jarPath}");
        }
    }
    public class NeoForgeInstaller
    {
        private const string NeoForgeMavenApi = "https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge";
        private const string NeoForgeMavenBase = "https://maven.neoforged.net/releases/net/neoforged/neoforge";
        private readonly MinecraftLauncher _launcher;
        private readonly HttpClient _http;
        public NeoForgeInstaller(MinecraftLauncher launcher, HttpClient? http = null)
        {
            _launcher = launcher;
            _http = http ?? new HttpClient();
        }
        private static void EnsureLauncherProfilesExist(string minecraftDir)
        {
            var profilesPath = Path.Combine(minecraftDir, "launcher_profiles.json");
            if (!File.Exists(profilesPath))
            {
                var emptyProfiles = new JsonObject
                {
                    ["profiles"] = new JsonObject(),
                    ["selectedProfile"] = (JsonNode?)null,
                    ["authenticationDatabase"] = new JsonObject()
                };
                Directory.CreateDirectory(minecraftDir);
                File.WriteAllText(profilesPath, emptyProfiles.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                System.Diagnostics.Debug.WriteLine($"[NeoForge] Created launcher_profiles.json at {profilesPath}");
            }
        }
        public async Task<string> Install(string neoforgeVersion)
        {
            var minecraftDir = Managers.PlatformPaths.GetMinecraftDir();
            EnsureLauncherProfilesExist(minecraftDir);
            var installerUrl = $"{NeoForgeMavenBase}/{neoforgeVersion}/neoforge-{neoforgeVersion}-installer.jar";
            var tempDir = Path.Combine(Path.GetTempPath(), "oranglauncher_neoforge");
            Directory.CreateDirectory(tempDir);
            var installerPath = Path.Combine(tempDir, $"neoforge-{neoforgeVersion}-installer.jar");
            System.Diagnostics.Debug.WriteLine($"[NeoForge] Downloading installer from {installerUrl}");
            if (!File.Exists(installerPath))
            {
                var data = await _http.GetByteArrayAsync(installerUrl);
                await File.WriteAllBytesAsync(installerPath, data);
            }
            var versionsDir = Path.Combine(minecraftDir, "versions");
            var existingNeoforgeDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(versionsDir))
            {
                foreach (var d in Directory.GetDirectories(versionsDir))
                {
                    var name = Path.GetFileName(d);
                    if (name != null && name.Contains("neoforge", StringComparison.OrdinalIgnoreCase))
                        existingNeoforgeDirs.Add(name);
                }
            }
            var javaPath = MinecraftLauncher.FindJava(17);
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = javaPath,
                Arguments = $"-jar \"{installerPath}\" --install-client \"{minecraftDir}\"",
                WorkingDirectory = tempDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            System.Diagnostics.Debug.WriteLine($"[NeoForge] Running: {psi.FileName} {psi.Arguments}");
            var process = System.Diagnostics.Process.Start(psi);
            if (process != null)
            {
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();
                var completed = await Task.WhenAny(
                    process.WaitForExitAsync(),
                    Task.Delay(TimeSpan.FromMinutes(10))
                );
                if (!process.HasExited)
                {
                    process.Kill();
                    throw new Exception("NeoForge installer timed out after 10 minutes");
                }
                var stdout = await stdoutTask;
                var stderr = await stderrTask;
                System.Diagnostics.Debug.WriteLine($"[NeoForge] Exit code: {process.ExitCode}");
                if (!string.IsNullOrWhiteSpace(stdout))
                    System.Diagnostics.Debug.WriteLine($"[NeoForge] Installer stdout: {stdout}");
                if (!string.IsNullOrWhiteSpace(stderr))
                    System.Diagnostics.Debug.WriteLine($"[NeoForge] Installer stderr: {stderr}");
                if (process.ExitCode != 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[NeoForge] Installer process failed, attempting manual extraction");
                    return await ManualInstallNeoForgeAsync(installerPath, neoforgeVersion, minecraftDir);
                }
            }
            try { File.Delete(installerPath); } catch { }
            if (Directory.Exists(versionsDir))
            {
                var expectedName = $"neoforge-{neoforgeVersion}";
                var expectedDir = Path.Combine(versionsDir, expectedName);
                if (Directory.Exists(expectedDir) && File.Exists(Path.Combine(expectedDir, $"{expectedName}.json")))
                    return expectedName;
                var newNeoforgeDirs = Directory.GetDirectories(versionsDir)
                    .Select(Path.GetFileName)
                    .Where(d => d != null &&
                        d!.Contains("neoforge", StringComparison.OrdinalIgnoreCase) &&
                        !existingNeoforgeDirs.Contains(d!) &&
                        File.Exists(Path.Combine(versionsDir, d!, $"{d}.json")))
                    .ToList();
                if (newNeoforgeDirs.Count > 0)
                    return newNeoforgeDirs[0]!;
                var anyValid = Directory.GetDirectories(versionsDir)
                    .Where(d => Path.GetFileName(d)?.Contains("neoforge", StringComparison.OrdinalIgnoreCase) == true)
                    .Where(d => File.Exists(Path.Combine(d, $"{Path.GetFileName(d)}.json")))
                    .OrderByDescending(d => Directory.GetLastWriteTime(d))
                    .Select(Path.GetFileName)
                    .FirstOrDefault();
                if (anyValid != null) return anyValid;
            }
            return $"neoforge-{neoforgeVersion}";
        }
        private async Task<string> ManualInstallNeoForgeAsync(string installerPath, string neoforgeVersion, string minecraftDir)
        {
            var versionsDir = Path.Combine(minecraftDir, "versions");
            var librariesDir = Path.Combine(minecraftDir, "libraries");
            var versionId = $"neoforge-{neoforgeVersion}";
            using var zip = ZipFile.OpenRead(installerPath);
            string? clientJsonText = null;
            var versionJsonEntry = zip.GetEntry("version.json");
            if (versionJsonEntry != null)
            {
                using var stream = versionJsonEntry.Open();
                using var reader = new StreamReader(stream);
                clientJsonText = await reader.ReadToEndAsync();
            }
            if (clientJsonText == null)
            {
                throw new Exception("NeoForge installer does not contain version.json");
            }
            var clientJsonNode = JsonNode.Parse(clientJsonText);
            if (clientJsonNode != null)
            {
                clientJsonNode["id"] = versionId;
            }
            var versionDir = Path.Combine(versionsDir, versionId);
            Directory.CreateDirectory(versionDir);
            var versionJsonPath = Path.Combine(versionDir, $"{versionId}.json");
            await File.WriteAllTextAsync(versionJsonPath, clientJsonNode?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? clientJsonText);
            System.Diagnostics.Debug.WriteLine($"[NeoForge] Wrote version JSON: {versionJsonPath}");
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.StartsWith("maven/") && !string.IsNullOrEmpty(entry.Name))
                {
                    var relativePath = entry.FullName["maven/".Length..];
                    var destPath = Path.Combine(librariesDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
                    var destDir = Path.GetDirectoryName(destPath);
                    if (destDir != null)
                    {
                        Directory.CreateDirectory(destDir);
                        if (!File.Exists(destPath))
                        {
                            entry.ExtractToFile(destPath, false);
                        }
                    }
                }
            }
            using var clientDoc = JsonDocument.Parse(clientJsonText);
            if (clientDoc.RootElement.TryGetProperty("libraries", out var libs))
            {
                await InstallLibrariesFromJsonAsync(libs, minecraftDir);
            }
            var installProfileEntry = zip.GetEntry("install_profile.json");
            if (installProfileEntry != null)
            {
                using var stream = installProfileEntry.Open();
                using var reader = new StreamReader(stream);
                var installProfileJson = await reader.ReadToEndAsync();
                using var profileDoc = JsonDocument.Parse(installProfileJson);
                if (profileDoc.RootElement.TryGetProperty("libraries", out var profileLibs))
                {
                    await InstallLibrariesFromJsonAsync(profileLibs, minecraftDir);
                }
            }
            return versionId;
        }
        private async Task InstallLibrariesFromJsonAsync(JsonElement librariesArray, string minecraftDir)
        {
            var librariesDir = Path.Combine(minecraftDir, "libraries");
            foreach (var lib in librariesArray.EnumerateArray())
            {
                string? name = null;
                string? url = null;
                if (lib.TryGetProperty("name", out var nameProp))
                    name = nameProp.GetString();
                if (lib.TryGetProperty("downloads", out var downloads) &&
                    downloads.TryGetProperty("artifact", out var artifact))
                {
                    string? artifactPath = null;
                    string? artifactUrl = null;
                    if (artifact.TryGetProperty("path", out var pathProp))
                        artifactPath = pathProp.GetString();
                    if (artifact.TryGetProperty("url", out var urlProp))
                        artifactUrl = urlProp.GetString();
                    if (!string.IsNullOrEmpty(artifactPath) && !string.IsNullOrEmpty(artifactUrl))
                    {
                        var destPath = Path.Combine(librariesDir, artifactPath.Replace('/', Path.DirectorySeparatorChar));
                        if (!File.Exists(destPath))
                        {
                            try
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                                var data = await _http.GetByteArrayAsync(artifactUrl);
                                await File.WriteAllBytesAsync(destPath, data);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[NeoForge] Failed to download library {artifactUrl}: {ex.Message}");
                            }
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(name))
                {
                    if (lib.TryGetProperty("url", out var libUrlProp))
                        url = libUrlProp.GetString();
                    var parts = name.Split(':');
                    if (parts.Length >= 3)
                    {
                        var group = parts[0].Replace('.', '/');
                        var artifactName = parts[1];
                        var version = parts[2];
                        var classifier = parts.Length > 3 ? $"-{parts[3]}" : "";
                        var fileEnd = "jar";
                        if (version.Contains('@'))
                        {
                            var split = version.Split('@');
                            version = split[0];
                            fileEnd = split[1];
                        }
                        var fileName = $"{artifactName}-{version}{classifier}.{fileEnd}";
                        var path = $"{group}/{artifactName}/{version}/{fileName}";
                        var destPath = Path.Combine(librariesDir, path.Replace('/', Path.DirectorySeparatorChar));
                        if (!File.Exists(destPath))
                        {
                            var baseUrl = url?.TrimEnd('/') ?? "https://libraries.minecraft.net";
                            var urls = new[]
                            {
                                $"{baseUrl}/{path}",
                                $"https://libraries.minecraft.net/{path}",
                                $"https://maven.neoforged.net/releases/{path}",
                                $"https://maven.minecraftforge.net/{path}"
                            };
                            foreach (var downloadUrl in urls)
                            {
                                try
                                {
                                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                                    var data = await _http.GetByteArrayAsync(downloadUrl);
                                    await File.WriteAllBytesAsync(destPath, data);
                                    break;
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
        }
    }
    public class NeoForgeVersionLoader
    {
        private readonly HttpClient _http;
        public NeoForgeVersionLoader(HttpClient? http = null)
        {
            _http = http ?? new HttpClient();
        }
        public async Task<List<NeoForgeVersionInfo>> GetNeoForgeVersions(string mcVersion)
        {
            var versions = new List<NeoForgeVersionInfo>();
            try
            {
                var mcParts = mcVersion.Split('.');
                if (mcParts.Length < 2) return versions;
                int minor = int.Parse(mcParts[1]);
                int patch = mcParts.Length > 2 ? int.Parse(mcParts[2]) : 0;
                var prefix = $"{minor}.{patch}";
                var url = "https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge";
                var json = await _http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("versions", out var versionArray))
                {
                    foreach (var v in versionArray.EnumerateArray())
                    {
                        var vStr = v.GetString();
                        if (vStr != null && vStr.StartsWith(prefix))
                        {
                            versions.Add(new NeoForgeVersionInfo
                            {
                                VersionName = vStr,
                                MinecraftVersion = mcVersion
                            });
                        }
                    }
                    versions.Reverse();
                }
            }
            catch { }
            return versions;
        }
    }
    public class NeoForgeVersionInfo
    {
        public string VersionName { get; set; } = "";
        public string MinecraftVersion { get; set; } = "";
    }
}