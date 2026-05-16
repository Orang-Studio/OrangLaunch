using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI;
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
    public sealed partial class DownloadWindow : Window
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
            this.InitializeComponent();
            _installPath = installPath;
            _projectType = projectType;
            _gameVersion = gameVersion;
            _modLoader = modLoader;
            Title = $"Download {GetProjectTypeDisplayName()}s";
            ResultsListBox.ItemsSource = _results;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            AppWindow.GetFromWindowId(windowId)?.Resize(new Windows.Graphics.SizeInt32(900, 650));
        }
        private string GetProjectTypeDisplayName() => _projectType switch
        {
            "mod" => "Mod",
            "resourcepack" => "Resource Pack",
            "shader" => "Shader",
            _ => "Project"
        };
        private async void SearchButton_Click(object sender, RoutedEventArgs e) => await PerformSearchAsync();
        private async void SearchTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
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
                    AddSearchResult(hit);
            }
            catch (Exception ex)
            {
                await ShowMessageAsync($"Search failed: {ex.Message}", "Error");
            }
            finally
            {
                SearchProgressBar.IsIndeterminate = false;
                SearchProgressBar.Visibility = Visibility.Collapsed;
            }
        }
        private void AddSearchResult(JsonElement hit, string titleSuffix = "")
        {
            string? iconUrl = hit.TryGetProperty("icon_url", out var icon) && icon.ValueKind == JsonValueKind.String ? icon.GetString() : null;
            string? author = hit.TryGetProperty("author", out var authorEl) && authorEl.ValueKind == JsonValueKind.String ? authorEl.GetString() : null;
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
        private async void InstallProjectButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is SearchResult result)
            {
                btn.IsEnabled = false;
                btn.Content = "...";
                try
                {
                    await InstallProjectAsync(result);
                    await ShowMessageAsync($"{result.Title} installed successfully!", "Success");
                }
                catch (Exception ex)
                {
                    await ShowMessageAsync($"Installation failed: {ex.Message}", "Error");
                }
                finally
                {
                    btn.Content = "Install";
                    btn.IsEnabled = true;
                }
            }
        }
        private async Task InstallProjectAsync(SearchResult result)
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
                    if (gv.GetString() == _gameVersion) { gameVersionMatch = true; break; }
                fallbackVersion ??= version;
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
                            { loaderMatch = true; break; }
                        }
                    }
                    if (!loaderMatch) continue;
                }
                compatibleVersion = version;
                break;
            }
            if (compatibleVersion == null && fallbackVersion != null)
            {
                var confirmed = await ShowConfirmAsync(
                    $"No version found for {_gameVersion}" +
                    (_projectType == "mod" ? $" and {_modLoader}" : "") +
                    ". Install the latest version anyway?", "Version Mismatch");
                if (confirmed) compatibleVersion = fallbackVersion;
                else return;
            }
            if (compatibleVersion == null)
                throw new Exception("No versions available for this project.");
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
                throw new Exception("Could not find download URL");
            Directory.CreateDirectory(_installPath);
            var filePath = Path.Combine(_installPath, fileName);
            using (var resp = await _sharedClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                resp.EnsureSuccessStatusCode();
                var total = resp.Content.Headers.ContentLength ?? -1L;
                var canReport = total > 0;
                using var stream = await resp.Content.ReadAsStreamAsync();
                using var fs = File.Create(filePath);
                var buffer = new byte[81920];
                long read = 0;
                int bytes;
                while ((bytes = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fs.WriteAsync(buffer, 0, bytes);
                    read += bytes;
                        if (canReport)
                        {
                            var pct = (read / (double)total) * 100.0;
                            DispatcherQueue.TryEnqueue(() => { SearchProgressBar.IsIndeterminate = false; SearchProgressBar.Value = pct; });
                        }
                }
                await fs.FlushAsync();
            }
        }
        private async Task ShowMessageAsync(string message, string title = "")
        {
            var dialog = new ContentDialog
            {
                Title = string.IsNullOrEmpty(title) ? "OrangLauncher" : title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
        private async Task<bool> ShowConfirmAsync(string message, string title = "")
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                PrimaryButtonText = "Yes",
                CloseButtonText = "No",
                XamlRoot = Content.XamlRoot
            };
            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }
    }
}