using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace OrangLauncher
{
    public class SearchResult
    {
        public string? ProjectId { get; set; }
        public string? Slug { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Author { get; set; }
        public string? IconUrl { get; set; }
        public int Downloads { get; set; }
    }
    public partial class DownloadWindow : Window
    {
        private readonly string _installPath;
        private readonly string _projectType;
        private readonly string _gameVersion;
        private readonly string _modLoader;
        private readonly ObservableCollection<SearchResult> _results = new();
        private static readonly HttpClient _sharedClient = new() { Timeout = TimeSpan.FromSeconds(30) };
        static DownloadWindow()
        {
            _sharedClient.DefaultRequestHeaders.Add("User-Agent", "OrangLauncher/1.0 (github.com/Orang-Studio/OrangLaunch)");
        }
        public DownloadWindow(string installPath, string projectType, string gameVersion, string modLoader = "")
        {
            InitializeComponent();
            _installPath = installPath;
            _projectType = projectType;
            _gameVersion = gameVersion;
            _modLoader = modLoader;
            Title = $"Download {GetProjectTypeDisplayName()}s";
            ResultsListBox.ItemsSource = _results;
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
        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            await PerformSearchAsync();
        }
        private async void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                await PerformSearchAsync();
        }
        private async Task PerformSearchAsync()
        {
            var query = SearchTextBox.Text.Trim();
            if (string.IsNullOrEmpty(query)) return;
            SearchProgressBar.Visibility = Visibility.Visible;
            SearchProgressBar.IsIndeterminate = true;
            _results.Clear();
            try
            {
                var facetParts = new List<string>();
                var projectTypeFacet = _projectType switch
                {
                    "mod" => "project_type:mod",
                    "resourcepack" => "project_type:resourcepack",
                    "shader" => "project_type:shader",
                    _ => ""
                };
                if (!string.IsNullOrEmpty(projectTypeFacet))
                    facetParts.Add($"[\"{projectTypeFacet}\"]");
                var facets = facetParts.Count > 0 ? $"[{string.Join(",", facetParts)}]" : "";
                var url = $"https://api.modrinth.com/v2/search?query={Uri.EscapeDataString(query)}&limit=20";
                if (!string.IsNullOrEmpty(facets))
                    url += $"&facets={Uri.EscapeDataString(facets)}";
                var response = await _sharedClient.GetStringAsync(url);
                var json = JsonDocument.Parse(response);
                var hits = json.RootElement.GetProperty("hits");
                foreach (var hit in hits.EnumerateArray())
                {
                    AddSearchResult(hit);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Search failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SearchProgressBar.IsIndeterminate = false;
                SearchProgressBar.Visibility = Visibility.Collapsed;
            }
        }
        private void AddSearchResult(JsonElement hit, string titleSuffix = "")
        {
            string? iconUrl = null;
            if (hit.TryGetProperty("icon_url", out var icon) && icon.ValueKind == JsonValueKind.String)
            {
                iconUrl = icon.GetString();
            }
            string? author = null;
            if (hit.TryGetProperty("author", out var authorEl) && authorEl.ValueKind == JsonValueKind.String)
            {
                author = authorEl.GetString();
            }
            _results.Add(new SearchResult
            {
                ProjectId = hit.GetProperty("project_id").GetString(),
                Slug = hit.GetProperty("slug").GetString(),
                Title = hit.GetProperty("title").GetString() + titleSuffix,
                Description = hit.GetProperty("description").GetString(),
                Author = author ?? "",
                IconUrl = iconUrl,
                Downloads = hit.TryGetProperty("downloads", out var dl) ? dl.GetInt32() : 0
            });
        }
        private void ViewDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is SearchResult result)
            {
                var detailsWindow = new ModDetailsWindow(
                    result.Slug ?? result.ProjectId ?? "",
                    result.Title ?? "",
                    result.Description ?? "",
                    result.IconUrl,
                    _projectType,
                    _installPath,
                    _gameVersion,
                    _modLoader
                );
                detailsWindow.Owner = this;
                detailsWindow.ShowDialog();
            }
        }
        private async void InstallProjectButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is SearchResult result)
            {
                btn.IsEnabled = false;
                btn.Content = "...";
                try
                {
                    var ok = await InstallProjectAsync(result);
                    if (ok)
                        MessageBox.Show($"{result.Title} installed successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    else
                        MessageBox.Show($"Installation cancelled or no compatible version was installed for {result.Title}.", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Installation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    btn.Content = "Install";
                    btn.IsEnabled = true;
                }
            }
        }
        private async Task<bool> InstallProjectAsync(SearchResult result)
        {
            var versionsUrl = $"https://api.modrinth.com/v2/project/{result.ProjectId}/version";
            var versionsResponse = await _sharedClient.GetStringAsync(versionsUrl);
            var versions = JsonDocument.Parse(versionsResponse);
            JsonElement? compatibleVersion = null;
            JsonElement? fallbackVersion = null;
            foreach (var version in versions.RootElement.EnumerateArray())
            {
                var gameVersions = version.GetProperty("game_versions");
                var loaders = version.TryGetProperty("loaders", out var l) ? l : default;
                bool gameVersionMatch = false;
                foreach (var gv in gameVersions.EnumerateArray())
                {
                    if (gv.GetString() == _gameVersion)
                    {
                        gameVersionMatch = true;
                        break;
                    }
                }
                if (fallbackVersion == null)
                    fallbackVersion = version;
                if (!gameVersionMatch) continue;
                if (_projectType == "mod" && !string.IsNullOrEmpty(_modLoader) && _modLoader.ToLower() != "vanilla")
                {
                    bool loaderMatch = false;
                    if (loaders.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var loader in loaders.EnumerateArray())
                        {
                            var loaderStr = loader.GetString()?.ToLower();
                            var targetLoader = _modLoader.ToLower();
                            if (loaderStr == targetLoader || 
                                (targetLoader == "neoforge" && loaderStr == "forge") ||
                                (targetLoader == "forge" && loaderStr == "neoforge"))
                            {
                                loaderMatch = true;
                                break;
                            }
                        }
                    }
                    if (!loaderMatch) continue;
                }
                compatibleVersion = version;
                break;
            }
            if (compatibleVersion == null && fallbackVersion != null)
            {
                var result2 = MessageBox.Show(
                    $"No version found for {_gameVersion}" + 
                    (_projectType == "mod" ? $" and {_modLoader}" : "") +
                    ". Install the latest version anyway?",
                    "Version Mismatch",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (result2 == MessageBoxResult.Yes)
                    compatibleVersion = fallbackVersion;
                else
                    return false;
            }
            if (compatibleVersion == null)
            {
                throw new Exception("No versions available for this project.");
            }
            var files = compatibleVersion.Value.GetProperty("files");
            string? downloadUrl = null;
            string? fileName = null;
            foreach (var file in files.EnumerateArray())
            {
                if (file.TryGetProperty("primary", out var primary) && primary.GetBoolean())
                {
                    downloadUrl = file.GetProperty("url").GetString();
                    fileName = file.GetProperty("filename").GetString();
                    break;
                }
            }
            if (downloadUrl == null)
            {
                var firstFile = files.EnumerateArray().FirstOrDefault();
                if (firstFile.ValueKind != JsonValueKind.Undefined)
                {
                    downloadUrl = firstFile.GetProperty("url").GetString();
                    fileName = firstFile.GetProperty("filename").GetString();
                }
            }
            if (string.IsNullOrEmpty(downloadUrl) || string.IsNullOrEmpty(fileName))
            {
                throw new Exception("Could not find download URL");
            }
            Directory.CreateDirectory(_installPath);
            var filePath = Path.Combine(_installPath, fileName);
            var fileBytes = await _sharedClient.GetByteArrayAsync(downloadUrl);
            await File.WriteAllBytesAsync(filePath, fileBytes);
            return true;
        }
    }
}