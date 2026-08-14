using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using OrangLauncher.Managers;
using OrangLauncher.Models;

namespace OrangLauncher
{
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
        public Visibility DetailsVisibility => Visibility.Visible;
        private string _installLabel = "Install";
        public string InstallLabel { get => _installLabel; set { _installLabel = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InstallLabel))); } }
        private System.Windows.Media.ImageSource? _icon;
        public System.Windows.Media.ImageSource? Icon { get => _icon; set { _icon = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon))); } }
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

        private (string? Version, string? Loader) GetContentTargetSetup(MinecraftInstance? instance)
        {
            if (instance != null)
            {
                var gameVersion = InstalledVersionDetector.ResolveGameVersion(
                    instance.InstalledVersionName ?? instance.Version,
                    instance.MinecraftDir, PlatformPaths.GetMinecraftDir());
                return (gameVersion, NormalizedLoader(instance));
            }
            _contentDefaultSetup ??= InstalledVersionDetector.Detect(PlatformPaths.GetMinecraftDir());
            var loader = _contentDefaultSetup.Loader;
            if (loader is not ("fabric" or "forge" or "quilt" or "neoforge")) loader = null;
            return (_contentDefaultSetup.GameVersion, loader);
        }

        private void ApplyNewUiLocalization()
        {
            try
            {
                ContentTabHeaderText.Text = LocalizationManager.GetString("CONTENT_TAB", "Content");
                ContentTabMods.Content = LocalizationManager.GetString("CONTENT_MODS", "Mods");
                ContentTabResourcePacks.Content = LocalizationManager.GetString("CONTENT_RESOURCE_PACKS", "Resource Packs");
                ContentTabDataPacks.Content = LocalizationManager.GetString("CONTENT_DATA_PACKS", "Data Packs");
                ContentTabShaders.Content = LocalizationManager.GetString("CONTENT_SHADERS", "Shaders");
                ContentTabModpacks.Content = LocalizationManager.GetString("CONTENT_MODPACKS", "Modpacks");
                ContentSearchButton.Content = LocalizationManager.GetString("CONTENT_SEARCH", "Search");
                if (ContentSortComboBox.Items.Count > 3)
                {
                    ((ComboBoxItem)ContentSortComboBox.Items[0]).Content = LocalizationManager.GetString("CONTENT_SORT_RELEVANCE", "Relevance");
                    ((ComboBoxItem)ContentSortComboBox.Items[1]).Content = LocalizationManager.GetString("CONTENT_SORT_DOWNLOADS", "Downloads");
                    ((ComboBoxItem)ContentSortComboBox.Items[2]).Content = LocalizationManager.GetString("CONTENT_SORT_NEWEST", "Newest");
                    ((ComboBoxItem)ContentSortComboBox.Items[3]).Content = LocalizationManager.GetString("CONTENT_SORT_UPDATED", "Recently Updated");
                }
                ContentPrevButton.Content = LocalizationManager.GetString("CONTENT_PREV", "Previous");
                ContentNextButton.Content = LocalizationManager.GetString("CONTENT_NEXT", "Next");
                SettingsUiStyleLabel.Text = LocalizationManager.GetString("SETTINGS_UI_STYLE", "Interface Style");
                SettingsUiStyleDesc.Text = LocalizationManager.GetString("SETTINGS_UI_STYLE_DESC", "Modern (Windows 11) or Classic (Windows 10) interface. Switching restarts the launcher.");
                if (UiStyleComboBox.Items.Count > 1)
                {
                    ((ComboBoxItem)UiStyleComboBox.Items[0]).Content = LocalizationManager.GetString("SETTINGS_UI_STYLE_CLASSIC", "Classic (Windows 10 style)");
                    ((ComboBoxItem)UiStyleComboBox.Items[1]).Content = LocalizationManager.GetString("SETTINGS_UI_STYLE_MODERN", "Modern (Windows 11 style)");
                }
                SkinBodyViewer.ToolTip = LocalizationManager.GetString("SKIN_DRAG_HINT", "Drag to rotate");
            }
            catch { }
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
            var (defaultVersion, defaultLoader) = GetContentTargetSetup(null);
            var defaultName = LocalizationManager.GetString("CONTENT_DEFAULT_TARGET", "Default (.minecraft)");
            if (defaultVersion != null) defaultName += $" - {defaultLoader ?? "vanilla"} {defaultVersion}";
            var names = new List<string> { defaultName };
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

        private static bool IsVanillaInstance(MinecraftInstance? instance)
        {
            var l = instance?.ModLoader?.ToLowerInvariant() ?? "vanilla";
            return l is "vanilla" or "none" or "";
        }

        private void ContentTabButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not string tag) return;
            _contentType = tag;
            _contentOffset = 0;
            _ = RunContentSearchAsync();
        }

        private void ContentSearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { _contentOffset = 0; _ = RunContentSearchAsync(); }
        }

        private void ContentSearchButton_Click(object sender, RoutedEventArgs e) { _contentOffset = 0; _ = RunContentSearchAsync(); }

        private void ContentFilter_Changed(object sender, RoutedEventArgs e)
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
            if (_contentType == "mod" && instance != null && IsVanillaInstance(instance))
            {
                ContentInfoText.Text = LocalizationManager.GetString("CONTENT_VANILLA_BLOCKED", "This profile is vanilla - mods need Fabric/Forge/Quilt/NeoForge or the OptiFine loader.");
                UpdateContentPaging();
                return;
            }
            try
            {
                ContentInfoText.Text = LocalizationManager.GetString("CONTENT_SEARCHING", "Searching Modrinth...");
                var sort = (ContentSortComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "relevance";
                var (targetVersion, targetLoader) = GetContentTargetSetup(instance);
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

        private async Task LoadContentIconAsync(ContentItemVm vm, string? iconUrl)
        {
            var bytes = await ModrinthClient.GetIconAsync(iconUrl);
            if (bytes == null) return;
            // Decode in Core via SkiaSharp handles WebP
            var decoded = Rendering.SkinTextureLoader.DecodeToBgra(bytes);
            if (decoded == null) return;
            await Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var (bgra, w, h) = decoded.Value;
                    var wb = new WriteableBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32, null);
                    wb.WritePixels(new Int32Rect(0, 0, w, h), bgra, w * 4, 0);
                    wb.Freeze();
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
            if ((sender as FrameworkElement)?.Tag is not ContentItemVm vm) return;
            try
            {
                var instance = GetContentTargetInstance();
                var (targetVersion, targetLoader) = GetContentTargetSetup(instance);
                string baseDir = instance?.MinecraftDir ?? PlatformPaths.GetMinecraftDir();
                string installPath = vm.ProjectType switch
                {
                    "mod" => instance?.ModsDir ?? Path.Combine(baseDir, "mods"),
                    "resourcepack" => instance?.ResourcePacksDir ?? Path.Combine(baseDir, "resourcepacks"),
                    "shader" => instance?.ShaderPacksDir ?? Path.Combine(baseDir, "shaderpacks"),
                    "modpack" => "",
                    _ => Path.Combine(baseDir, "datapacks")
                };
                var win = new ModDetailsWindow(vm.Slug, vm.Title, vm.Description, vm.IconUrl, vm.ProjectType,
                    installPath, targetVersion ?? "", vm.ProjectType == "mod" ? targetLoader ?? "" : "")
                { Owner = this };
                win.ShowDialog();
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

        private async void ContentInstallButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not ContentItemVm vm) return;
            var instance = GetContentTargetInstance();
            vm.InstallLabel = "...";
            try
            {
                if (vm.ProjectType == "modpack") { await InstallModrinthModpackAsync(vm); return; }
                if (vm.ProjectType == "mod" && instance != null && IsVanillaInstance(instance))
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
