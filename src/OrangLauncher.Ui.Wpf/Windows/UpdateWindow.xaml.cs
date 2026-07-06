using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
namespace OrangLauncher
{
    public class UpdateItem : INotifyPropertyChanged
    {
        private bool _isSelected = true;
        public string? FilePath { get; set; }
        public string? Name { get; set; }
        public string? CurrentVersion { get; set; }
        public string? LatestVersion { get; set; }
        public string? ProjectId { get; set; }
        public string? DownloadUrl { get; set; }
        public string? NewFileName { get; set; }
        public string? FileHash { get; set; }
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
    public partial class UpdateWindow : Window
    {
        private readonly string _installPath;
        private readonly string _projectType;
        private readonly string _gameVersion;
        private readonly string _modLoader;
        private readonly ObservableCollection<UpdateItem> _updates = new();
        public UpdateWindow(string installPath, string projectType, string gameVersion, string modLoader = "")
        {
            InitializeComponent();
            _installPath = installPath;
            _projectType = projectType;
            _gameVersion = gameVersion;
            _modLoader = modLoader;
            Title = $"Update {GetProjectTypeDisplayName()}s";
            UpdatesListBox.ItemsSource = _updates;
            Loaded += UpdateWindow_Loaded;
        }
        private string GetProjectTypeDisplayName()
        {
            return _projectType switch
            {
                "mod" => "Mod",
                "resourcepack" => "Resource Pack",
                "shader" => "Shader",
                _ => "Project"
            };
        }
        private async void UpdateWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await CheckForUpdatesAsync();
        }
        private async void RecheckButton_Click(object sender, RoutedEventArgs e)
        {
            await CheckForUpdatesAsync();
        }
        private async Task CheckForUpdatesAsync()
        {
            UpdateProgressBar.Visibility = Visibility.Visible;
            UpdateProgressBar.IsIndeterminate = true;
            _updates.Clear();
            try
            {
                if (!Directory.Exists(_installPath))
                {
                    TitleTextBlock.Text = "No files found";
                    return;
                }
                var files = Directory.GetFiles(_installPath, "*.jar")
                    .Concat(Directory.GetFiles(_installPath, "*.zip"))
                    .ToList();
                if (files.Count == 0)
                {
                    TitleTextBlock.Text = "No files found";
                    return;
                }
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "OrangLauncher/1.0 (contact@example.com)");
                var hashes = new List<string>();
                var fileHashMap = new Dictionary<string, string>();
                foreach (var file in files)
                {
                    using var stream = File.OpenRead(file);
                    using var sha512 = SHA512.Create();
                    var hashBytes = await sha512.ComputeHashAsync(stream);
                    var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                    hashes.Add(hash);
                    fileHashMap[hash] = file;
                }
                var hashesJson = JsonSerializer.Serialize(new { hashes = hashes, algorithm = "sha512" });
                var content = new StringContent(hashesJson, System.Text.Encoding.UTF8, "application/json");
                var response = await client.PostAsync("https://api.modrinth.com/v2/version_files", content);
                Dictionary<string, JsonElement> versionDataDict = new();
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var versionData = JsonDocument.Parse(responseJson);
                    foreach (var prop in versionData.RootElement.EnumerateObject())
                    {
                        versionDataDict[prop.Name] = prop.Value;
                    }
                }
                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    var hash = hashes[i];
                    var fileName = Path.GetFileName(file);
                    if (versionDataDict.TryGetValue(hash, out var version))
                    {
                        var projectId = version.GetProperty("project_id").GetString();
                        var currentVersion = version.GetProperty("version_number").GetString();
                        var latestInfo = await GetLatestVersionAsync(client, projectId);
                        if (latestInfo != null && !string.Equals(latestInfo.VersionNumber, currentVersion, StringComparison.OrdinalIgnoreCase))
                        {
                            _updates.Add(new UpdateItem
                            {
                                FilePath = file,
                                Name = ExtractModName(fileName),
                                CurrentVersion = currentVersion,
                                LatestVersion = latestInfo.VersionNumber,
                                ProjectId = projectId,
                                DownloadUrl = latestInfo.DownloadUrl,
                                NewFileName = latestInfo.FileName,
                                FileHash = hash
                            });
                        }
                    }
                    else
                    {
                        var projectInfo = await TryFindProjectByFilenameAsync(client, fileName);
                        if (projectInfo != null)
                        {
                            var latestInfo = await GetLatestVersionAsync(client, projectInfo.ProjectId);
                            if (latestInfo != null)
                            {
                                _updates.Add(new UpdateItem
                                {
                                    FilePath = file,
                                    Name = projectInfo.Name ?? ExtractModName(fileName),
                                    CurrentVersion = "Unknown",
                                    LatestVersion = latestInfo.VersionNumber,
                                    ProjectId = projectInfo.ProjectId,
                                    DownloadUrl = latestInfo.DownloadUrl,
                                    NewFileName = latestInfo.FileName,
                                    FileHash = hash
                                });
                            }
                        }
                    }
                }
                TitleTextBlock.Text = _updates.Count > 0 
                    ? $"{_updates.Count} update(s) available" 
                    : "All files are up to date";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error checking for updates: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                UpdateProgressBar.IsIndeterminate = false;
                UpdateProgressBar.Visibility = Visibility.Collapsed;
            }
        }
        private class ProjectSearchResult
        {
            public string? ProjectId { get; set; }
            public string? Name { get; set; }
        }
        private async Task<ProjectSearchResult?> TryFindProjectByFilenameAsync(HttpClient client, string fileName)
        {
            try
            {
                var searchName = ExtractModName(fileName);
                if (searchName.Length < 3) return null;
                var facets = new List<string> { $"[\"project_type:{_projectType}\"]" };
                if (!string.IsNullOrEmpty(_gameVersion))
                {
                    facets.Add($"[\"versions:{_gameVersion}\"]");
                }
                if (_projectType == "mod" && !string.IsNullOrEmpty(_modLoader) && _modLoader != "vanilla")
                {
                    facets.Add($"[\"categories:{_modLoader.ToLower()}\"]");
                }
                var facetString = string.Join(",", facets);
                var url = $"https://api.modrinth.com/v2/search?query={Uri.EscapeDataString(searchName)}&facets=[{facetString}]&limit=5";
                var response = await client.GetStringAsync(url);
                var json = JsonDocument.Parse(response);
                var hits = json.RootElement.GetProperty("hits");
                foreach (var hit in hits.EnumerateArray())
                {
                    var title = hit.GetProperty("title").GetString();
                    var slug = hit.GetProperty("slug").GetString();
                    if (title != null && (
                        fileName.Contains(title, StringComparison.OrdinalIgnoreCase) ||
                        fileName.Contains(slug ?? "", StringComparison.OrdinalIgnoreCase) ||
                        title.Contains(searchName, StringComparison.OrdinalIgnoreCase)))
                    {
                        return new ProjectSearchResult
                        {
                            ProjectId = hit.GetProperty("project_id").GetString(),
                            Name = title
                        };
                    }
                }
            }
            catch { }
            return null;
        }
        private class LatestVersionInfo
        {
            public string? VersionNumber { get; set; }
            public string? DownloadUrl { get; set; }
            public string? FileName { get; set; }
        }
        private async Task<LatestVersionInfo?> GetLatestVersionAsync(HttpClient client, string? projectId)
        {
            if (string.IsNullOrEmpty(projectId)) return null;
            try
            {
                var url = $"https://api.modrinth.com/v2/project/{projectId}/version";
                var response = await client.GetStringAsync(url);
                var versions = JsonDocument.Parse(response);
                foreach (var version in versions.RootElement.EnumerateArray())
                {
                    var gameVersions = version.GetProperty("game_versions");
                    // Unknown target version: fall back to newest rather than matching nothing.
                    bool gameVersionMatch = string.IsNullOrEmpty(_gameVersion);
                    foreach (var gv in gameVersions.EnumerateArray())
                    {
                        if (gv.GetString() == _gameVersion)
                        {
                            gameVersionMatch = true;
                            break;
                        }
                    }
                    if (!gameVersionMatch) continue;
                    if (_projectType == "mod" && !string.IsNullOrEmpty(_modLoader) && _modLoader != "vanilla")
                    {
                        var loaders = version.TryGetProperty("loaders", out var l) ? l : default;
                        bool loaderMatch = false;
                        if (loaders.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var loader in loaders.EnumerateArray())
                            {
                                if (string.Equals(loader.GetString(), _modLoader, StringComparison.OrdinalIgnoreCase))
                                {
                                    loaderMatch = true;
                                    break;
                                }
                            }
                        }
                        if (!loaderMatch) continue;
                    }
                    var files = version.GetProperty("files");
                    foreach (var file in files.EnumerateArray())
                    {
                        if (file.TryGetProperty("primary", out var primary) && primary.GetBoolean())
                        {
                            return new LatestVersionInfo
                            {
                                VersionNumber = version.GetProperty("version_number").GetString(),
                                DownloadUrl = file.GetProperty("url").GetString(),
                                FileName = file.GetProperty("filename").GetString()
                            };
                        }
                    }
                    var firstFile = files.EnumerateArray().FirstOrDefault();
                    if (firstFile.ValueKind != JsonValueKind.Undefined)
                    {
                        return new LatestVersionInfo
                        {
                            VersionNumber = version.GetProperty("version_number").GetString(),
                            DownloadUrl = firstFile.GetProperty("url").GetString(),
                            FileName = firstFile.GetProperty("filename").GetString()
                        };
                    }
                }
            }
            catch { }
            return null;
        }
        private string ExtractModName(string fileName)
        {
            var name = Path.GetFileNameWithoutExtension(fileName);
            name = Regex.Replace(name, @"[-_]?\d+\.\d+.*$", "");
            name = Regex.Replace(name, @"[-_]?(forge|fabric|quilt|mc|minecraft).*$", "", RegexOptions.IgnoreCase);
            name = name.Replace("-", " ").Replace("_", " ");
            return string.IsNullOrWhiteSpace(name) ? fileName : name.Trim();
        }
        private void RemoveSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = _updates.Where(u => u.IsSelected).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("No items selected", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var result = MessageBox.Show($"Remove {selected.Count} selected file(s)?", "Confirm", 
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                foreach (var item in selected)
                {
                    try
                    {
                        if (File.Exists(item.FilePath))
                            File.Delete(item.FilePath);
                        _updates.Remove(item);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to remove {item.Name}: {ex.Message}");
                    }
                }
            }
        }
        private async void UpdateSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = _updates.Where(u => u.IsSelected).ToList();
            await UpdateItemsAsync(selected);
        }
        private async void UpdateAllButton_Click(object sender, RoutedEventArgs e)
        {
            await UpdateItemsAsync(_updates.ToList());
        }
        private async Task UpdateItemsAsync(List<UpdateItem> items)
        {
            if (items.Count == 0)
            {
                MessageBox.Show("No items to update", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            UpdateProgressBar.Visibility = Visibility.Visible;
            UpdateProgressBar.IsIndeterminate = false;
            UpdateProgressBar.Maximum = items.Count;
            UpdateProgressBar.Value = 0;
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "OrangLauncher/1.0");
            int success = 0;
            int failed = 0;
            foreach (var item in items)
            {
                try
                {
                    if (string.IsNullOrEmpty(item.DownloadUrl) || string.IsNullOrEmpty(item.NewFileName))
                    {
                        failed++;
                        continue;
                    }
                    var fileBytes = await client.GetByteArrayAsync(item.DownloadUrl);
                    var newFilePath = Path.Combine(_installPath, item.NewFileName);
                    if (File.Exists(item.FilePath))
                        File.Delete(item.FilePath);
                    await File.WriteAllBytesAsync(newFilePath, fileBytes);
                    _updates.Remove(item);
                    success++;
                }
                catch
                {
                    failed++;
                }
                UpdateProgressBar.Value++;
            }
            UpdateProgressBar.Visibility = Visibility.Collapsed;
            string message = $"Updated: {success}";
            if (failed > 0) message += $", Failed: {failed}";
            MessageBox.Show(message, "Update Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            TitleTextBlock.Text = _updates.Count > 0 
                ? $"{_updates.Count} update(s) available" 
                : "All files are up to date";
        }
    }
}