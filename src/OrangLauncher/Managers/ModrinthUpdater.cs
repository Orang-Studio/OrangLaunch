namespace OrangLauncher.Managers
{
    public class UpdateCandidate
    {
        public string LocalPath { get; set; } = "";
        public string FileName => Path.GetFileName(LocalPath);
        public string ProjectTitle { get; set; } = "";
        public string CurrentVersion { get; set; } = "";
        public string NewVersion { get; set; } = "";
        public string Url { get; set; } = "";
        public string NewFileName { get; set; } = "";
        public string? Sha1 { get; set; }
        public bool IsDowngrade { get; set; }
    }
    public static class ModrinthUpdater
    {
        public static async Task<List<UpdateCandidate>> ScanAsync(string directory, string loader, string gameVersion, string[] exts,
            Action<int, int, string>? progress, CancellationToken ct)
        {
            var files = Directory.Exists(directory)
                ? Directory.GetFiles(directory).Where(f => exts.Any(e => f.EndsWith(e, StringComparison.OrdinalIgnoreCase))).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList()
                : new List<string>();
            var byHash = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < files.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                try { byHash[ModrinthClient.Sha1(files[i])] = files[i]; } catch { }
                if ((i + 1) % 10 == 0 || i + 1 == files.Count)
                    progress?.Invoke(i + 1, files.Count, LocalizationManager.T("QT_HASHING_N", "Hashing {done}/{total}", ("done", i + 1), ("total", files.Count)));
            }
            var known = await ModrinthClient.LookupHashesAsync(byHash.Keys);
            ct.ThrowIfCancellationRequested();
            progress?.Invoke(files.Count, files.Count, LocalizationManager.GetString("QT_CHECKING_MODRINTH", "Checking for updates on Modrinth..."));
            var loaders = string.IsNullOrEmpty(loader) || loader is "vanilla" or "none" ? new List<string>() : ModrinthClient.AcceptedLoaders(loader).ToList();
            var latest = known.Count > 0 ? await ModrinthClient.LatestForHashesAsync(known.Keys, loaders, new[] { gameVersion }) : new();
            var projects = await ModrinthClient.GetProjectsAsync(latest.Values.Select(v => v.ProjectId).Where(p => !string.IsNullOrEmpty(p)));
            var result = new List<UpdateCandidate>();
            foreach (var (sha1, path) in byHash)
            {
                if (!known.TryGetValue(sha1, out var current) || !latest.TryGetValue(sha1, out var newest)) continue;
                if (newest.Id == current.Id || (!string.IsNullOrEmpty(newest.VersionNumber) && newest.VersionNumber == current.VersionNumber)) continue;
                var file = newest.PrimaryFile;
                if (file == null) continue;
                projects.TryGetValue(newest.ProjectId, out var project);
                result.Add(new UpdateCandidate
                {
                    LocalPath = path,
                    ProjectTitle = project?.Title ?? current.Name ?? Path.GetFileName(path),
                    CurrentVersion = current.VersionNumber,
                    NewVersion = newest.VersionNumber,
                    Url = file.Url,
                    NewFileName = file.Filename,
                    Sha1 = file.Hashes.Sha1,
                    IsDowngrade = newest.DatePublished < current.DatePublished
                });
            }
            return result.OrderBy(c => c.IsDowngrade).ThenBy(c => c.ProjectTitle, StringComparer.OrdinalIgnoreCase).ToList();
        }
        public static async Task<(bool Ok, string Message)> ApplyAsync(UpdateCandidate c)
        {
            var dir = Path.GetDirectoryName(c.LocalPath)!;
            var tmp = Path.Combine(Path.GetTempPath(), "orang_upd_" + Guid.NewGuid().ToString("N") + "_" + c.NewFileName);
            try
            {
                await ModrinthClient.DownloadFileAsync(c.Url, tmp);
                if (!string.IsNullOrEmpty(c.Sha1) && !ModrinthClient.Sha1(tmp).Equals(c.Sha1, StringComparison.OrdinalIgnoreCase))
                    return (false, LocalizationManager.GetString("QT_UPD_HASH", "Downloaded file hash mismatch"));
                var target = Path.Combine(dir, c.NewFileName);
                if (c.LocalPath.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)) target += ".disabled";
                var bak = c.LocalPath + ".bak";
                File.Move(c.LocalPath, bak, true);
                try
                {
                    File.Move(tmp, target, true);
                }
                catch (Exception ex)
                {
                    File.Move(bak, c.LocalPath, true);
                    return (false, LocalizationManager.T("QT_UPD_REPLACE_FAILED", "Failed to replace file: {error}", ("error", ex.Message)));
                }
                try { File.Delete(bak); } catch { }
                return (true, c.NewVersion);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
}   }   }   }