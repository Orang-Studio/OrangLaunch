using System.Text.Json;

namespace OrangLauncher.Managers
{
    public sealed class DetectedGameSetup
    {
        public string? GameVersion { get; init; }
        public string? Loader { get; init; }
        public string? LoaderVersion { get; init; }
    }
    public static class InstalledVersionDetector
    {
        public static DetectedGameSetup Detect(string minecraftDir)
        {
            try
            {
                var versionsDir = Path.Combine(minecraftDir, "versions");
                if (!Directory.Exists(versionsDir)) return new DetectedGameSetup();

                DetectedGameSetup? best = null;
                DateTime bestTime = DateTime.MinValue;
                foreach (var dir in Directory.GetDirectories(versionsDir))
                {
                    var id = Path.GetFileName(dir);
                    var jsonPath = Path.Combine(dir, id + ".json");
                    if (!File.Exists(jsonPath)) continue;
                    var parsed = ParseVersionJson(jsonPath, id);
                    if (parsed == null) continue;
                    // prefer the most recently touched modded profile; a plain vanilla
                    var t = File.GetLastWriteTimeUtc(jsonPath);
                    bool bestIsModded = best?.Loader != null;
                    bool thisIsModded = parsed.Loader != null;
                    if (best == null || (thisIsModded && !bestIsModded) ||
                        (thisIsModded == bestIsModded && t > bestTime))
                    {
                        best = parsed;
                        bestTime = t;
                    }
                }
                return best ?? new DetectedGameSetup();
            }
            catch
            {
                return new DetectedGameSetup();
            }
        }
        public static string? ResolveGameVersion(string? versionId, params string?[] searchDirs)
        {
            if (string.IsNullOrWhiteSpace(versionId)) return null;
            foreach (var dir in searchDirs)
            {
                if (string.IsNullOrEmpty(dir)) continue;
                try
                {
                    var jsonPath = Path.Combine(dir, "versions", versionId, versionId + ".json");
                    if (!File.Exists(jsonPath)) continue;
                    using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
                    if (doc.RootElement.TryGetProperty("inheritsFrom", out var inh))
                    {
                        var inherits = inh.GetString();
                        if (!string.IsNullOrWhiteSpace(inherits)) return inherits;
                    }
                }
                catch { }
            }
            var (loader, _, gameVersion) = ClassifyVersionId(versionId);
            if (gameVersion != null) return gameVersion;
            return loader == null ? versionId : null;
        }

        private static DetectedGameSetup? ParseVersionJson(string jsonPath, string id)
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
                var root = doc.RootElement;
                string? inherits = root.TryGetProperty("inheritsFrom", out var inh) ? inh.GetString() : null;
                var (loader, loaderVersion, gameFromId) = ClassifyVersionId(id);
                var gameVersion = inherits ?? gameFromId ?? (loader == null ? id : null);
                if (gameVersion == null) return null;
                return new DetectedGameSetup { GameVersion = gameVersion, Loader = loader, LoaderVersion = loaderVersion };
            }
            catch
            {
                return null;
            }
        }

        private static (string? Loader, string? LoaderVersion, string? GameVersion) ClassifyVersionId(string id)
        {
            var lower = id.ToLowerInvariant();
            if (lower.StartsWith("fabric-loader-") || lower.StartsWith("quilt-loader-"))
            {
                var loader = lower.StartsWith("fabric") ? "fabric" : "quilt";
                var rest = id[(loader.Length + "-loader-".Length)..];
                var dash = rest.LastIndexOf('-');
                return dash > 0 ? (loader, rest[..dash], rest[(dash + 1)..]) : (loader, rest, null);
            }
            if (lower.Contains("-forge-"))
            {
                var i = lower.IndexOf("-forge-", StringComparison.Ordinal);
                return ("forge", id[(i + 7)..], id[..i]);
            }
            if (lower.StartsWith("neoforge-"))
                return ("neoforge", id["neoforge-".Length..], null);
            if (lower.Contains("optifine"))
            {
                var dash = id.IndexOf('-');
                return ("optifine", null, dash > 0 ? id[..dash] : null);
            }
            return (null, null, null);
        }
    }
}