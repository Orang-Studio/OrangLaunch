using System.Diagnostics;
using System.Text;
using OrangLauncher.Backend;
using OrangLauncher.Models;
namespace OrangLauncher.Managers
{
    public sealed class LaunchRequest
    {
        public MinecraftInstance Instance { get; init;} = null!;
        public UserProfile Account { get; init;} = null!;
        public string? QuickPlayServer { get; init;}
        public bool GlobalDiscreteGpu { get; init;}
    }
    public sealed class LaunchResult
    {
        public int ExitCode { get; init;}
        public bool Started { get; init;}
        public string? CrashReportPath { get; init;}
        public string? CrashSummary { get; init;}
    }
    public class LaunchService
    {
        public event Action<string>? Log;
        public event Action<int, string>? Progress;
        public event Action? GameStarted;
        private Process? _process;
        private CancellationTokenSource? _cts;
        public bool IsGameRunning => _process is { HasExited: false };
        public static string LogsDir
        {
            get
            {
                var dir = Path.Combine(PlatformPaths.GetDataDir(), "logs");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }
        private void L(string message) => Log?.Invoke(message);
        private void P(int percent, string text) => Progress?.Invoke(Math.Clamp(percent, 0, 100), text);
        public void Cancel() => _cts?.Cancel();
        public void Stop()
        {
            _cts?.Cancel();
            try
            {
                if (_process is { HasExited: false }) _process.Kill(true);
            }
            catch { }
        }
        public static List<string> SplitArgs(string? text)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return result;
            var sb = new StringBuilder();
            char quote = '\0';
            bool any = false;
            foreach (var c in text)
            {
                if (quote != '\0')
                {
                    if (c == quote) quote = '\0';
                    else sb.Append(c);
                }
                else if (c == '"' || c == '\'')
                {
                    quote = c;
                    any = true;
                }
                else if (char.IsWhiteSpace(c))
                {
                    if (sb.Length > 0 || any) result.Add(sb.ToString());
                    sb.Clear();
                    any = false;
                }
                else sb.Append(c);
            }
            if (sb.Length > 0 || any) result.Add(sb.ToString());
            return result;
        }
        public static void ApplyVideoOptions(MinecraftInstance instance)
        {
            var options = FileLinks.Resolve(Path.Combine(instance.MinecraftDir, "options.txt"));
            var updates = new Dictionary<string, string>();
            var o = instance.Options;
            if (o.Fullscreen)
            {
                updates["fullscreen"] = "true";
                o.FullscreenManaged = true;
            }
            else if (o.FullscreenManaged)
            {
                updates["fullscreen"] = "false";
                o.FullscreenManaged = false;
            }
            if (o.ResWidth > 0 && o.ResHeight > 0)
            {
                updates["overrideWidth"] = o.ResWidth.ToString();
                updates["overrideHeight"] = o.ResHeight.ToString();
                o.ResManaged = true;
            }
            else if (o.ResManaged)
            {
                updates["overrideWidth"] = "0";
                updates["overrideHeight"] = "0";
                o.ResManaged = false;
            }
            if (updates.Count == 0) return;
            if (!File.Exists(options) && !o.Fullscreen && !(o.ResWidth > 0 && o.ResHeight > 0)) return;
            var lines = File.Exists(options) ? File.ReadAllLines(options).ToList() : new List<string>();
            var seen = new HashSet<string>();
            for (int i = 0; i < lines.Count; i++)
            {
                var idx = lines[i].IndexOf(':');
                if (idx <= 0) continue;
                var key = lines[i][..idx];
                if (updates.TryGetValue(key, out var value))
                {
                    lines[i] = $"{key}:{value}";
                    seen.Add(key);
                }
            }
            foreach (var kv in updates)
                if (!seen.Contains(kv.Key)) lines.Add($"{kv.Key}:{kv.Value}");
            Directory.CreateDirectory(Path.GetDirectoryName(options)!);
            File.WriteAllLines(options, lines);
        }
        private static List<string> LegacyJvmArgs(MinecraftInstance instance)
        {
            var args = new List<string>();
            if (!instance.Options.LegacyFixes) return args;
            if (McVersion.Below(instance.Version, 1, 13))
            {
                args.Add("-Djava.util.Arrays.useLegacyMergeSort=true");
                args.Add("-Dfml.ignoreInvalidMinecraftCertificates=true");
                args.Add("-Dfml.ignorePatchDiscrepancies=true");
            }
            return args;
        }
        private static void SetGpuPreference(string javaPath, bool discrete)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\DirectX\UserGpuPreferences");
                if (discrete) key.SetValue(javaPath, "GpuPreference=2;");
                else if (key.GetValue(javaPath) is string v && v == "GpuPreference=2;") key.DeleteValue(javaPath, false);
            }
            catch { }
        }
        private async Task<MSession> ResolveSessionAsync(UserProfile account)
        {
            if (!account.IsMicrosoft)
            {
                var uuid = string.IsNullOrEmpty(account.Uuid) ? ProfileManager.OfflineUuid(account.Username) : account.Uuid;
                return new MSession(account.Username, "0", uuid) { ClientToken = "0" };
            }
            var valid = await MicrosoftAuthManager.EnsureValidAsync(account);
            return MSession.CreateMicrosoft(valid.Username, valid.Uuid, valid.MinecraftToken ?? valid.AccessToken ?? "");
        }
        private static int RunHook(string command, string cwd, IDictionary<string, string> env, Action<string> log)
        {
            if (string.IsNullOrWhiteSpace(command)) return 0;
            try
            {
                var psi = new ProcessStartInfo("cmd.exe")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = cwd
                };
                psi.ArgumentList.Add("/c");
                psi.ArgumentList.Add(command);
                foreach (var kv in env) psi.Environment[kv.Key] = kv.Value;
                using var p = Process.Start(psi);
                if (p == null) return -1;
                p.OutputDataReceived += (_, e) => { if (e.Data != null) log($"[Hook] {e.Data}"); };
                p.ErrorDataReceived += (_, e) => { if (e.Data != null) log($"[Hook] {e.Data}"); };
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.WaitForExit();
                return p.ExitCode;
            }
            catch (Exception ex)
            {
                log($"[Hook] failed: {ex.Message}");
                return -1;
            }
        }
        public async Task<LaunchResult> RunAsync(LaunchRequest request)
        {
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            var instance = request.Instance;
            var mcPath = new MinecraftPath(PlatformPaths.GetMinecraftDir());
            var launcher = new MinecraftLauncher(mcPath);
            Directory.CreateDirectory(instance.MinecraftDir);
            L($"[Launcher] Launching Minecraft {instance.Version} ({instance.LoaderKey}) as {request.Account.Username}...");
            var session = await ResolveSessionAsync(request.Account);
            ct.ThrowIfCancellationRequested();
            try { SharingManager.PullBeforeLaunch(instance); }
            catch (Exception ex) { L($"[Sharing] {ex.Message}"); }
            P(2, LocalizationManager.T("QT_INSTALLING_PCT", "Installing {name}... {percent}%", ("name", instance.Version), ("percent", 2)));
            var baseMeta = await launcher.GetVersionMetaAsync(instance.Version)
                ?? throw new Exception(LocalizationManager.T("QT_VERSION_NOT_FOUND_MSG", "Minecraft version '{version}' does not exist.", ("version", instance.Version)));
            var requiredJava = baseMeta.JavaVersion?.MajorVersion ?? JavaResolver.RequiredJavaFor(instance.Version);
            string javaPath;
            if (!string.IsNullOrWhiteSpace(instance.JavaPath) && File.Exists(instance.JavaPath))
                javaPath = instance.JavaPath!;
            else
            {
                P(4, LocalizationManager.T("QT_JAVA_DOWNLOADING_FROM", "Downloading Java {major} from Adoptium...", ("major", requiredJava)));
                javaPath = await JavaResolver.EnsureAsync(requiredJava, L);
            }
            L($"[Java] Using: {javaPath}");
            ct.ThrowIfCancellationRequested();
            var versionId = instance.Version;
            if (!instance.IsVanilla)
            {
                string? installed = null;
                if (!string.IsNullOrEmpty(instance.InstalledVersionName) && File.Exists(mcPath.GetVersionJsonPath(instance.InstalledVersionName)))
                    installed = instance.InstalledVersionName;
                installed ??= ModLoaderInstaller.FindInstalledVersion(mcPath.BasePath, instance.LoaderKey, instance.Version, instance.LoaderVersion);
                if (installed != null)
                {
                    L($"[Launcher] Using installed version: {installed}");
                }
                else
                {
                    L($"[Launcher] Installing {instance.LoaderKey} {(string.IsNullOrEmpty(instance.LoaderVersion) ? "latest" : instance.LoaderVersion)} for {instance.Version}...");
                    void OnMsg(string m) => L($"[{instance.LoaderKey}] {m}");
                    void OnPct(int p) => P(p, LocalizationManager.T("QT_INSTALLING_PCT", "Installing {name}... {percent}%", ("name", instance.LoaderKey), ("percent", p)));
                    ModLoaderInstaller.OnProgressChanged += OnMsg;
                    ModLoaderInstaller.OnProgressPercentageChanged += OnPct;
                    try
                    {
                        installed = await ModLoaderInstaller.InstallLoaderAsync(mcPath.BasePath, instance.Version, instance.LoaderKey,
                            string.IsNullOrWhiteSpace(instance.LoaderVersion) ? null : instance.LoaderVersion);
                    }
                    finally
                    {
                        ModLoaderInstaller.OnProgressChanged -= OnMsg;
                        ModLoaderInstaller.OnProgressPercentageChanged -= OnPct;
                    }
                }
                instance.InstalledVersionName = installed;
                InstanceManager.Instance.SaveInstances();
                versionId = installed;
            }
            ct.ThrowIfCancellationRequested();
            L($"[Launcher] Preparing {versionId}...");
            await launcher.InstallAsync(versionId, new SyncProgress<InstallerProgressChangedEventArgs>(e =>
            {
                if (e.TotalTasks > 0)
                {
                    var pct = (int)(e.ProgressedTasks * 100.0 / e.TotalTasks);
                    P(pct, LocalizationManager.T("QT_INSTALLING_PCT", "Installing {name}... {percent}%", ("name", versionId), ("percent", pct)));
                }
            }), null, ct);
            await launcher.PrepareLegacyAssetsAsync(versionId, instance.MinecraftDir);
            P(100, LocalizationManager.GetString("QT_INSTALL_COMPLETE", "Installation complete!"));
            try { ApplyVideoOptions(instance); }
            catch (Exception ex) { L($"[Launcher] options.txt update skipped: {ex.Message}"); }
            var jvm = new List<string>();
            if (instance.Options.PermGenMb > 0) jvm.Add($"-XX:MetaspaceSize={instance.Options.PermGenMb}M");
            jvm.AddRange(SplitArgs(instance.Options.JvmArgs).Where(a => !a.StartsWith("-Xmx") && !a.StartsWith("-Xms")));
            if (!string.Equals(instance.JavaArgs?.Trim(), instance.Options.JvmArgs?.Trim(), StringComparison.Ordinal))
                jvm.AddRange(SplitArgs(instance.JavaArgs).Where(a => !a.StartsWith("-Xmx") && !a.StartsWith("-Xms") && !jvm.Contains(a)));
            if (!jvm.Any(a => a.StartsWith("-XX:+Use") && a.EndsWith("GC")) && !string.IsNullOrWhiteSpace(instance.PerformanceTier))
                jvm.AddRange(SystemPerformanceHelper.GetPerformanceArgumentStrings(instance.PerformanceTier, instance.RamMb).Where(a => !jvm.Contains(a)));
            jvm.AddRange(LegacyJvmArgs(instance));
            if (LegacySkinProxy.IsNeeded(instance.Version) && !jvm.Any(a => a.StartsWith("-Dhttp.proxyHost")))
            {
                LegacySkinProxy.Log = L;
                var proxyArgs = LegacySkinProxy.JvmArgs();
                jvm.AddRange(proxyArgs);
                if (proxyArgs.Count > 0) L("[SkinFix] Routing legacy skin requests through the launcher");
            }
            var maxRam = instance.RamMb;
            var minRam = instance.Options.MinRamMb > 0 ? Math.Min(instance.Options.MinRamMb, maxRam) : maxRam;
            var options = new MLaunchOption
            {
                Session = session,
                MaximumRamMb = maxRam,
                MinimumRamMb = minRam,
                JavaPath = javaPath,
                GameDirectory = instance.MinecraftDir,
                GameLauncherName = "OrangLauncher",
                GameLauncherVersion = UpdateManager.GetCurrentVersionString().TrimStart('v'),
                VersionType = "OrangLauncher",
                ScreenWidth = instance.Options.ResWidth > 0 && instance.Options.ResHeight > 0 ? instance.Options.ResWidth : null,
                ScreenHeight = instance.Options.ResWidth > 0 && instance.Options.ResHeight > 0 ? instance.Options.ResHeight : null,
                Demo = instance.Options.Demo,
                QuickPlayMultiplayer = request.QuickPlayServer,
                DisableServerBlacklist = instance.Options.AntiBan,
                ExtraJvmArguments = jvm.Select(a => new MArgument(a)).ToArray(),
                Log = L
            };
            if (instance.Options.Demo) L("[Launcher] Demo mode: passing --demo to the game");
            if (!string.IsNullOrEmpty(request.QuickPlayServer)) L($"[Launcher] Quick Play -> joining {request.QuickPlayServer}");
            var process = await launcher.BuildProcessAsync(versionId, options);
            var psi = process.StartInfo;
            foreach (var line in (instance.EnvVars ?? "").Split('\n'))
            {
                var t = line.Trim();
                if (t.Length == 0 || t.StartsWith("#")) continue;
                var eq = t.IndexOf('=');
                if (eq > 0) psi.Environment[t[..eq].Trim()] = t[(eq + 1)..].Trim();
            }
            SetGpuPreference(javaPath, instance.Options.DiscreteGpu || request.GlobalDiscreteGpu);
            var hookEnv = new Dictionary<string, string>
            {
                ["INST_NAME"] = instance.Name,
                ["INST_ID"] = instance.InstanceId,
                ["INST_DIR"] = instance.BasePath,
                ["INST_MC_DIR"] = instance.MinecraftDir,
                ["INST_JAVA"] = javaPath,
                ["INST_MC_VER"] = instance.Version
            };
            if (!string.IsNullOrWhiteSpace(instance.Options.PreLaunchCmd))
            {
                L("[Hook] Running pre-launch command...");
                var rc = await Task.Run(() => RunHook(instance.Options.PreLaunchCmd, instance.MinecraftDir, hookEnv, L));
                if (rc != 0)
                {
                    L($"[Hook] Pre-launch command exited with {rc}, aborting launch");
                    return new LaunchResult { ExitCode = rc, Started = false };
                }
            }
            var wrapper = SplitArgs(instance.Options.WrapperCmd);
            if (wrapper.Count > 0)
            {
                var original = new List<string> { psi.FileName };
                original.AddRange(psi.ArgumentList);
                psi.FileName = wrapper[0];
                psi.ArgumentList.Clear();
                foreach (var a in wrapper.Skip(1).Concat(original)) psi.ArgumentList.Add(a);
            }
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;
            L("[Launcher] Starting Minecraft...");
            L("[Launcher] " + MinecraftLauncher.FormatCommandLine(psi).Replace(session.AccessToken.Length > 8 ? session.AccessToken : "\u0000", "********"));
            ct.ThrowIfCancellationRequested();
            try { await PluginManager.Instance.NotifyBeforeGameLaunch(instance.Name, instance.Version); } catch { }
            StreamWriter? gameLog = null;
            try
            {
                var gameLogPath = Path.Combine(LogsDir, $"game_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                gameLog = new StreamWriter(gameLogPath, false, Encoding.UTF8) { AutoFlush = true };
                gameLog.WriteLine($"# {versionId} ({instance.LoaderKey}) in {instance.MinecraftDir}");
                L($"[Launcher] Game output is saved to {gameLogPath}");
            }
            catch { gameLog = null; }
            var logLock = new object();
            var xml = new Log4jXmlReader();
            void Emit(string line)
            {
                L(line);
                try { gameLog?.WriteLine(line); } catch { }
            }
            void OnLine(string? data)
            {
                if (data == null) return;
                lock (logLock)
                {
                    foreach (var line in xml.Feed(data)) Emit(line);
                }
            }
            _process = process;
            process.OutputDataReceived += (_, e) => OnLine(e.Data);
            process.ErrorDataReceived += (_, e) => OnLine(e.Data);
            var started = DateTime.Now;
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            GameStarted?.Invoke();
            try { await process.WaitForExitAsync(ct); }
            catch (OperationCanceledException)
            {
                try { if (!process.HasExited) process.Kill(true); } catch { }
                await process.WaitForExitAsync();
            }
            process.WaitForExit();
            var exitCode = process.ExitCode;
            _process = null;
            lock (logLock)
            {
                try { gameLog?.WriteLine($"# exit code {exitCode}"); gameLog?.Dispose(); } catch { }
            }
            if (!string.IsNullOrWhiteSpace(instance.Options.PostExitCmd))
            {
                hookEnv["INST_EXIT_CODE"] = exitCode.ToString();
                L("[Hook] Running post-exit command...");
                await Task.Run(() => RunHook(instance.Options.PostExitCmd, instance.MinecraftDir, hookEnv, L));
            }
            var elapsed = (long)(DateTime.Now - started).TotalSeconds;
            if (elapsed > 5)
            {
                instance.PlayTimeSeconds += elapsed;
                instance.LastPlayed = DateTime.Now;
                InstanceManager.Instance.SaveInstances();
            }
            L($"[Launcher] Minecraft exited with code {exitCode}");
            try { SharingManager.PushAfterExit(instance); }
            catch (Exception ex) { L($"[Sharing] {ex.Message}"); }
            try { await PluginManager.Instance.NotifyAfterGameExit(instance.Name, exitCode); } catch { }
            string? crashPath = null, crashSummary = null;
            if (exitCode != 0)
            {
                try
                {
                    var dir = Path.Combine(instance.MinecraftDir, "crash-reports");
                    var latest = Directory.Exists(dir)
                        ? new DirectoryInfo(dir).GetFiles("crash-*.txt").OrderByDescending(f => f.LastWriteTime).FirstOrDefault()
                        : null;
                    if (latest != null && DateTime.Now - latest.LastWriteTime < TimeSpan.FromMinutes(10))
                    {
                        crashPath = latest.FullName;
                        crashSummary = string.Join("\n", File.ReadLines(latest.FullName).Take(80).Where(l => l.Trim().Length > 0).Take(30));
                    }
                }
                catch { }
            }
            return new LaunchResult { ExitCode = exitCode, Started = true, CrashReportPath = crashPath, CrashSummary = crashSummary };
        }
    }
}