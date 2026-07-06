using System.Text.Json;

namespace OrangLauncher.Managers
{
    /// <summary>Game version + loader detected from an existing .minecraft folder.</summary>
    public sealed class DetectedGameSetup
    {
        public string? GameVersion { get; init; }
        /// <summary>fabric / forge / quilt / neoforge, or null for vanilla.</summary>
        public string? Loader { get; init; }
        public string? LoaderVersion { get; init; }
    }

    /// <summary>
    /// Detects what is actually installed in a .minecraft directory by reading
    /// versions/*/*.json. Used by the Content store so that installs against the
    /// default .minecraft target match the loader's game version instead of
    /// silently taking the newest file (e.g. 26.3 when the loader runs 26.2).
    /// </summary>
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
                    // Prefer the most recently touched modded profile; a plain vanilla
                    // version only wins when no modded profile exists at all.
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

        /// <summary>
        /// Recognizes loader version ids: fabric-loader-0.16.9-26.2,
        /// quilt-loader-0.27.0-26.2, 26.2-forge-52.0.1, neoforge-21.4.33.
        /// </summary>
        private static (string? Loader, string? LoaderVersion, string? GameVersion) ClassifyVersionId(string id)
        {
            var lower = id.ToLowerInvariant();
            if (lower.StartsWith("fabric-loader-") || lower.StartsWith("quilt-loader-"))
            {
                var loader = lower.StartsWith("fabric") ? "fabric" : "quilt";
                var rest = id[(loader.Length + "-loader-".Length)..];
                var dash = rest.IndexOf('-');
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
