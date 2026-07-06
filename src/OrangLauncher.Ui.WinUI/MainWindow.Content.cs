using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using OrangLauncher.Managers;
using OrangLauncher.Models;

namespace OrangLauncher
{
    [Microsoft.UI.Xaml.Data.Bindable]
    public class ContentItemVm : INotifyPropertyChanged
    {
        public string Title { get; set; } = "";
        public string Author { get; set; } = "";
        public string Description { get; set; } = "";
        public string Meta { get; set; } = "";
        public string Slug { get; set; } = "";
        public string ProjectType { get; set; } = "";
        public string? IconUrl { get; set; }
        public string DetailsLabel { get; set; } = "Details";
        /// <summary>Modrinth projects have a detail page; OrangLib packs do not.</summary>
        public Visibility DetailsVisibility => ProjectType == "oranglib" ? Visibility.Collapsed : Visibility.Visible;
        private string _installLabel = "Install";
        public string InstallLabel { get => _installLabel; set { _installLabel = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InstallLabel))); } }
        private Microsoft.UI.Xaml.Media.ImageSource? _icon;
        public Microsoft.UI.Xaml.Media.ImageSource? Icon { get => _icon; set { _icon = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public partial class MainWindow
    {
        private const int ContentPageSize = 20;
        private string _contentType = "mod";
        private int _contentOffset;
        private long _contentTotal;
        private bool _contentInitialized;
        private List<MinecraftInstance> _contentInstances = new();
        private readonly ObservableCollection<ContentItemVm> _contentItems = new();
        private DetectedGameSetup? _contentDefaultSetup;
        private bool _suppressContentSearch;

        /// <summary>
        /// Game version + loader the install should match. For the default .minecraft
        /// target this is detected from the installed versions folder, so a Fabric
        /// 26.2 setup gets 26.2 files instead of the newest release.
        /// </summary>
        private (string? Version, string? Loader) GetContentTargetSetup(MinecraftInstance? instance)
        {
            if (instance != null) return (instance.Version, NormalizedLoader(instance));
            _contentDefaultSetup ??= InstalledVersionDetector.Detect(PlatformPaths.GetMinecraftDir());
            var loader = _contentDefaultSetup.Loader;
            if (loader is not ("fabric" or "forge" or "quilt" or "neoforge")) loader = null;
            return (_contentDefaultSetup.GameVersion, loader);
        }

        private void InitContentPage()
        {
            if (_contentInitialized)
            {
                RefreshContentInstances();
                return;
            }
            _contentInitialized = true;
            ContentResultsList.ItemsSource = _contentItems;
            RefreshContentInstances();
            _ = RunContentSearchAsync();
        }

        private void RefreshContentInstances()
        {
            var selected = ContentInstanceComboBox.SelectedIndex;
            _contentDefaultSetup = null; // re-detect .minecraft on next use
            _contentInstances = InstanceManager.Instance.GetInstances();
            var names = new List<string> { LocalizationManager.GetString("CONTENT_DEFAULT_TARGET", "Default (.minecraft)") };
            names.AddRange(_contentInstances.Select(i => $"{i.Name} ({i.ModLoader} {i.Version})"));
            ContentInstanceComboBox.ItemsSource = names;
            ContentInstanceComboBox.SelectedIndex = selected >= 0 && selected < names.Count ? selected : 0;
        }

        private MinecraftInstance? GetContentTargetInstance()
        {
            int idx = ContentInstanceComboBox.SelectedIndex - 1; // 0 = default .minecraft
            return idx >= 0 && idx < _contentInstances.Count ? _contentInstances[idx] : null;
        }

        private static string? NormalizedLoader(MinecraftInstance? instance)
        {
            var l = instance?.ModLoader?.ToLowerInvariant();
            return l is "fabric" or "forge" or "quilt" or "neoforge" ? l : null;
        }

        private static bool IsVanilla(MinecraftInstance? instance)
        {
            var l = instance?.ModLoader?.ToLowerInvariant() ?? "vanilla";
            return l is "vanilla" or "none" or "";
        }

        private void ContentNavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            {
                _contentType = tag;
                _contentOffset = 0;
                _ = RunContentSearchAsync();
            }
        }

        private void ContentSearchBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter) { _contentOffset = 0; _ = RunContentSearchAsync(); }
        }

        private void ContentSearchButton_Click(object sender, RoutedEventArgs e) { _contentOffset = 0; _ = RunContentSearchAsync(); }

        private void ContentFilter_Changed(object sender, object e)
        {
            if (!_contentInitialized) return;
            if (_suppressContentSearch) return;
            _contentOffset = 0;
            _ = RunContentSearchAsync();
        }

        private void ContentPrevButton_Click(object sender, RoutedEventArgs e)
        {
            _contentOffset = Math.Max(0, _contentOffset - ContentPageSize);
            _ = RunContentSearchAsync();
        }

        private void ContentNextButton_Click(object sender, RoutedEventArgs e)
        {
            _contentOffset += ContentPageSize;
            _ = RunContentSearchAsync();
        }

        private async Task RunContentSearchAsync()
        {
            _contentItems.Clear();
            var instance = GetContentTargetInstance();
            if (_contentType == "mod" && instance != null && IsVanilla(instance))
            {
                ContentInfoText.Text = LocalizationManager.GetString("CONTENT_VANILLA_BLOCKED", "This profile is vanilla - mods need Fabric/Forge/Quilt/NeoForge or the OptiFine loader.");
                UpdateContentPaging();
                return;
            }
            if (_contentType == "oranglib")
            {
                await RunOrangLibSearchAsync();
                return;
            }
            try
            {
                ContentInfoText.Text = LocalizationManager.GetString("CONTENT_SEARCHING", "Searching Modrinth...");
                var sort = (ContentSortComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "relevance";
                var (targetVersion, targetLoader) = GetContentTargetSetup(instance);
                // Modpacks install into their own new instance, so the current
                // instance's game version/loader must not restrict them.
                var mcVersion = _contentType == "modpack" ? null : targetVersion;
                var loader = _contentType == "mod" ? targetLoader : null;
                var result = await ModrinthClient.SearchAsync(ContentSearchBox.Text, _contentType,
                    mcVersion, loader, sort, _contentOffset, ContentPageSize);
                _contentTotal = result.TotalHits;
                foreach (var hit in result.Hits)
                {
                    var vm = new ContentItemVm
                    {
                        Title = hit.Title,
                        Author = string.IsNullOrEmpty(hit.Author) ? "" : $"by {hit.Author}",
                        Description = hit.Description,
                        Meta = $"{hit.Downloads:N0} downloads  |  {string.Join(", ", hit.Categories.Take(4))}",
                        Slug = string.IsNullOrEmpty(hit.Slug) ? hit.ProjectId : hit.Slug,
                        ProjectType = _contentType,
                        IconUrl = hit.IconUrl,
                        DetailsLabel = LocalizationManager.GetString("CONTENT_DETAILS", "Details"),
                        InstallLabel = LocalizationManager.GetString("CONTENT_INSTALL", "Install")
                    };
                    _contentItems.Add(vm);
                    _ = LoadContentIconAsync(vm, hit.IconUrl);
                }
                ContentInfoText.Text = result.TotalHits == 0
                    ? LocalizationManager.GetString("CONTENT_NO_RESULTS", "No results.")
                    : $"{result.TotalHits:N0} results{(mcVersion != null ? $" for {mcVersion}" : "")}{(loader != null ? $" ({loader})" : "")}";
            }
            catch (Exception ex)
            {
                ContentInfoText.Text = $"Search failed: {ex.Message}";
            }
            UpdateContentPaging();
        }

        private async Task RunOrangLibSearchAsync()
        {
            try
            {
                ContentInfoText.Text = LocalizationManager.GetString("CONTENT_SEARCHING_ORANGLIB", "Loading OrangLib modpacks...");
                var packs = await OrangLibClient.GetModpacksAsync();
                var query = ContentSearchBox.Text?.Trim() ?? "";
                if (query.Length > 0)
                    packs = packs.Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                                          || p.Description.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
                _contentTotal = packs.Count;
                foreach (var pack in packs.Skip(_contentOffset).Take(ContentPageSize))
                {
                    var vm = new ContentItemVm
                    {
                        Title = pack.Name,
                        Author = string.IsNullOrEmpty(pack.Author) ? "" : $"by {pack.Author}",
                        Description = pack.Description,
                        Meta = $"{pack.Downloads:N0} downloads{(string.IsNullOrEmpty(pack.GameVersion) ? "" : $"  |  MC {pack.GameVersion}")}  |  OrangLib",
                        Slug = pack.Id.ToString(),
                        ProjectType = "oranglib",
                        IconUrl = pack.AbsoluteIconUrl,
                        InstallLabel = LocalizationManager.GetString("CONTENT_INSTALL", "Install")
                    };
                    _contentItems.Add(vm);
                    _ = LoadContentIconAsync(vm, pack.AbsoluteIconUrl);
                }
                ContentInfoText.Text = _contentTotal == 0
                    ? LocalizationManager.GetString("CONTENT_NO_RESULTS", "No results.")
                    : $"{_contentTotal:N0} OrangLib modpacks";
            }
            catch (Exception ex)
            {
                ContentInfoText.Text = $"OrangLib load failed: {ex.Message}";
            }
            UpdateContentPaging();
        }

        private async Task LoadContentIconAsync(ContentItemVm vm, string? iconUrl)
        {
            var bytes = await ModrinthClient.GetIconAsync(iconUrl);
            if (bytes == null) return;
            // Decode in Core via SkiaSharp - WebP icons render blank/black through WIC.
            var decoded = Rendering.SkinTextureLoader.DecodeToBgra(bytes);
            if (decoded == null) return;
            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    var (bgra, w, h) = decoded.Value;
                    var wb = new WriteableBitmap(w, h);
                    using (var stream = wb.PixelBuffer.AsStream())
                        stream.Write(bgra, 0, bgra.Length);
                    wb.Invalidate();
                    vm.Icon = wb;
                }
                catch { }
            });
        }

        private void UpdateContentPaging()
        {
            ContentPrevButton.IsEnabled = _contentOffset > 0;
            ContentNextButton.IsEnabled = _contentOffset + ContentPageSize < _contentTotal;
            ContentPageText.Text = $"{LocalizationManager.GetString("CONTENT_PAGE", "Page")} {_contentOffset / ContentPageSize + 1}";
        }


        /// <summary>
        /// Makes a freshly imported modpack instance visible everywhere without the
        /// combo refresh kicking off a new search that would clobber the status text.
        /// </summary>
        private void RefreshInstancesAfterModpackInstall()
        {
            _suppressContentSearch = true;
            try
            {
                RefreshContentInstances();
                LoadGameProfiles();
            }
            catch { }
            finally { _suppressContentSearch = false; }
        }
        private void ContentDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not ContentItemVm vm || vm.ProjectType == "oranglib") return;
            try
            {
                new ModDetailsWindow(vm.Slug, vm.Title, vm.Description, vm.IconUrl, vm.ProjectType).Activate();
            }
            catch (Exception ex)
            {
                ContentInfoText.Text = $"Could not open details: {ex.Message}";
            }
        }

        private async Task InstallModrinthModpackAsync(ContentItemVm vm)
        {
            ContentInfoText.Text = $"Downloading modpack {vm.Title}...";
            var tempDir = Path.Combine(Path.GetTempPath(), "OrangLauncher", "modpacks");
            var mrpack = await ModrinthClient.InstallAsync(vm.Slug, tempDir);
            if (mrpack == null || !mrpack.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase))
            {
                vm.InstallLabel = LocalizationManager.GetString("CONTENT_NO_FILE", "No file");
                ContentInfoText.Text = $"No .mrpack file found for {vm.Title}.";
                return;
            }
            ContentInfoText.Text = $"Installing modpack {vm.Title}...";
            var importer = new ModpackImporter();
            var (success, message, _) = await importer.ImportMrPackAsync(mrpack);
            vm.InstallLabel = success
                ? LocalizationManager.GetString("CONTENT_INSTALLED", "Installed")
                : LocalizationManager.GetString("CONTENT_FAILED", "Failed");
            if (success) RefreshInstancesAfterModpackInstall();
            ContentInfoText.Text = message;
        }

        private async Task InstallOrangLibPackAsync(ContentItemVm vm)
        {
            ContentInfoText.Text = $"Loading versions of {vm.Title}...";
            var versions = await OrangLibClient.GetVersionsAsync(vm.Slug);
            var version = versions.FirstOrDefault();
            if (version == null)
            {
                vm.InstallLabel = LocalizationManager.GetString("CONTENT_NO_FILE", "No file");
                ContentInfoText.Text = $"{vm.Title} has no downloadable versions.";
                return;
            }
            bool isMrpack = version.FileName.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase);
            var destDir = isMrpack
                ? Path.Combine(Path.GetTempPath(), "OrangLauncher", "modpacks")
                : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            ContentInfoText.Text = $"Downloading {version.FileName}...";
            var file = await OrangLibClient.DownloadVersionAsync(vm.Slug, version, destDir);
            if (isMrpack)
            {
                ContentInfoText.Text = $"Installing modpack {vm.Title}...";
                var importer = new ModpackImporter();
                var (success, message, _) = await importer.ImportMrPackAsync(file);
                vm.InstallLabel = success
                    ? LocalizationManager.GetString("CONTENT_INSTALLED", "Installed")
                    : LocalizationManager.GetString("CONTENT_FAILED", "Failed");
                if (success) RefreshInstancesAfterModpackInstall();
                ContentInfoText.Text = message;
            }
            else
            {
                vm.InstallLabel = LocalizationManager.GetString("CONTENT_INSTALLED", "Installed");
                ContentInfoText.Text = $"Downloaded to {file}";
            }
        }

        private async void ContentInstallButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not ContentItemVm vm) return;
            var instance = GetContentTargetInstance();
            vm.InstallLabel = "...";
            try
            {
                if (vm.ProjectType == "modpack") { await InstallModrinthModpackAsync(vm); return; }
                if (vm.ProjectType == "oranglib") { await InstallOrangLibPackAsync(vm); return; }
                if (vm.ProjectType == "mod" && IsVanilla(instance) && instance != null)
                {
                    ContentInfoText.Text = LocalizationManager.GetString("CONTENT_VANILLA_BLOCKED", "Vanilla profiles cannot use mods.");
                    vm.InstallLabel = "Install";
                    return;
                }
                string baseDir = instance?.MinecraftDir ?? PlatformPaths.GetMinecraftDir();
                string destDir = vm.ProjectType switch
                {
                    "mod" => instance?.ModsDir ?? Path.Combine(baseDir, "mods"),
                    "resourcepack" => instance?.ResourcePacksDir ?? Path.Combine(baseDir, "resourcepacks"),
                    "shader" => instance?.ShaderPacksDir ?? Path.Combine(baseDir, "shaderpacks"),
                    _ => Path.Combine(baseDir, "datapacks")
                };
                var (targetVersion, targetLoader) = GetContentTargetSetup(instance);
                var loader = vm.ProjectType == "mod" ? targetLoader : null;
                var installed = await ModrinthClient.InstallAsync(vm.Slug, destDir, targetVersion, loader);
                vm.InstallLabel = installed != null ? LocalizationManager.GetString("CONTENT_INSTALLED", "Installed") : LocalizationManager.GetString("CONTENT_NO_FILE", "No file");
                ContentInfoText.Text = installed != null
                    ? $"Installed {Path.GetFileName(installed)} â†’ {destDir}"
                    : $"No compatible file of {vm.Title} for {(targetVersion ?? "any version")}{(loader != null ? $" ({loader})" : "")}.";
            }
            catch (Exception ex)
            {
                vm.InstallLabel = LocalizationManager.GetString("CONTENT_FAILED", "Failed");
                ContentInfoText.Text = $"Install failed: {ex.Message}";
            }
        }
    }
}
