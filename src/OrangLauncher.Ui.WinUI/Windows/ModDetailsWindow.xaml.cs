using System.Diagnostics;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using Windows.ApplicationModel.DataTransfer;
namespace OrangLauncher
{
    public class ProjectVersion
    {
        public string? Name { get; set; }
        public string? VersionNumber { get; set; }
        public string? GameVersions { get; set; }
        public string? Loaders { get; set; }
        public string? DatePublished { get; set; }
        public string? Changelog { get; set; }
        public string? DownloadUrl { get; set; }
        public string? FileName { get; set; }
        public string? VersionId { get; set; }
        /// <summary>"fabric, quilt  |  MC 26.2, 26.3" line for the versions list.</summary>
        public string SupportLine
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(Loaders)) parts.Add(Loaders);
                if (!string.IsNullOrEmpty(GameVersions)) parts.Add($"MC {GameVersions}");
                return string.Join("  |  ", parts);
            }
        }
    }
    public sealed partial class ModDetailsWindow : Window
    {
        private readonly string _projectId;
        private readonly string _projectType;
        private string _projectSlug;
        private string? _projectBody;
        private readonly List<ProjectVersion> _versions = [];
        public ModDetailsWindow(string projectId, string title, string description, string? iconUrl, string projectType)
        {
            this.InitializeComponent();
            _projectId = projectId;
            _projectSlug = projectId;
            _projectType = projectType;
            TitleText.Text = title;
            DescText.Text = description;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            AppWindow.GetFromWindowId(windowId)?.Resize(new Windows.Graphics.SizeInt32(900, 700));
            LoadIconAsync(iconUrl);
            LoadProjectDetailsAsync();
            LoadVersionsAsync();
        }
        private async void LoadIconAsync(string? iconUrl)
        {
            if (string.IsNullOrEmpty(iconUrl)) return;
            try
            {
                var bitmap = new BitmapImage(new Uri(iconUrl));
                IconImage.Source = bitmap;
            }
            catch { }
        }
        private async void LoadProjectDetailsAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "OrangLauncher/1.0 (github.com/Orang-Studio/OrangLaunch)");
                var response = await client.GetStringAsync($"https://api.modrinth.com/v2/project/{_projectId}");
                var project = JsonDocument.Parse(response);
                if (project.RootElement.TryGetProperty("body", out var body))
                {
                    _projectBody = body.GetString();
                    await RenderMarkdownAsync(_projectBody ?? "");
                }
                if (project.RootElement.TryGetProperty("slug", out var slug))
                    _projectSlug = slug.GetString() ?? _projectId;
            }
            catch (Exception ex)
            {
                AboutTextBlock.Text = $"Failed to load project details: {ex.Message}";
                AboutScrollViewer.Visibility = Visibility.Visible;
                AboutWebView.Visibility = Visibility.Collapsed;
            }
        }
        private async Task RenderMarkdownAsync(string markdown)
        {
            try
            {
                var env = await Managers.WebView2Helper.GetEnvironmentAsync();
                await AboutWebView.EnsureCoreWebView2Async(env);
                var html = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        * {{ box-sizing: border-box; }}
        body {{ 
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background-color: #0a0a0a; color: #e8e8e8; padding: 20px; margin: 0;
            line-height: 1.6; font-size: 14px;
        }}
        h1, h2, h3, h4, h5, h6 {{ color: #ffffff; margin-top: 1.5em; margin-bottom: 0.5em;
            border-bottom: 1px solid #2a2a2a; padding-bottom: 0.3em; }}
        h1 {{ font-size: 1.8em; }} h2 {{ font-size: 1.5em; }} h3 {{ font-size: 1.3em; }}
        p {{ margin: 0.8em 0; }}
        a {{ color: #ff8c00; text-decoration: none; }} a:hover {{ text-decoration: underline; }}
        code {{ background-color: #1a1a1a; padding: 2px 6px; border-radius: 4px; font-family: 'Consolas', monospace; font-size: 0.9em; }}
        pre {{ background-color: #1a1a1a; padding: 15px; border-radius: 6px; overflow-x: auto; }}
        pre code {{ padding: 0; background: none; }}
        img {{ max-width: 100%; height: auto; border-radius: 6px; margin: 10px 0; }}
        ul, ol {{ padding-left: 25px; margin: 0.8em 0; }}
        li {{ margin: 0.3em 0; }}
        blockquote {{ border-left: 4px solid #ff8c00; margin: 1em 0; padding: 0.5em 1em; background-color: #1a1a1a; border-radius: 0 6px 6px 0; }}
        hr {{ border: none; border-top: 1px solid #2a2a2a; margin: 1.5em 0; }}
        table {{ border-collapse: collapse; width: 100%; margin: 1em 0; }}
        th, td {{ border: 1px solid #2a2a2a; padding: 8px 12px; text-align: left; }}
        th {{ background-color: #1a1a1a; }}
    </style>
</head>
<body>{ConvertMarkdownToHtml(markdown)}</body>
</html>";
                AboutWebView.NavigateToString(html);
                AboutWebView.Visibility = Visibility.Visible;
                AboutScrollViewer.Visibility = Visibility.Collapsed;
            }
            catch
            {
                // No WebView2: show a readable plain-text rendering, not raw markdown.
                AboutTextBlock.Text = StripMarkdown(markdown);
                AboutScrollViewer.Visibility = Visibility.Visible;
                AboutWebView.Visibility = Visibility.Collapsed;
            }
        }
        private static string ConvertMarkdownToHtml(string markdown)
        {
            if (string.IsNullOrEmpty(markdown)) return "";
            var html = markdown;
            html = System.Text.RegularExpressions.Regex.Replace(html, @"^######\s+(.+)$", "<h6>$1</h6>", System.Text.RegularExpressions.RegexOptions.Multiline);
            html = System.Text.RegularExpressions.Regex.Replace(html, @"^#####\s+(.+)$", "<h5>$1</h5>", System.Text.RegularExpressions.RegexOptions.Multiline);
            html = System.Text.RegularExpressions.Regex.Replace(html, @"^####\s+(.+)$", "<h4>$1</h4>", System.Text.RegularExpressions.RegexOptions.Multiline);
            html = System.Text.RegularExpressions.Regex.Replace(html, @"^###\s+(.+)$", "<h3>$1</h3>", System.Text.RegularExpressions.RegexOptions.Multiline);
            html = System.Text.RegularExpressions.Regex.Replace(html, @"^##\s+(.+)$", "<h2>$1</h2>", System.Text.RegularExpressions.RegexOptions.Multiline);
            html = System.Text.RegularExpressions.Regex.Replace(html, @"^#\s+(.+)$", "<h1>$1</h1>", System.Text.RegularExpressions.RegexOptions.Multiline);
            html = System.Text.RegularExpressions.Regex.Replace(html, @"!\[([^\]]*)\]\(([^)]+)\)", "<img src=\"$2\" alt=\"$1\" />");
            html = System.Text.RegularExpressions.Regex.Replace(html, @"\[([^\]]+)\]\(([^)]+)\)", "<a href=\"$2\" target=\"_blank\">$1</a>");
            html = System.Text.RegularExpressions.Regex.Replace(html, @"\*\*\*(.+?)\*\*\*", "<strong><em>$1</em></strong>");
            html = System.Text.RegularExpressions.Regex.Replace(html, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
            html = System.Text.RegularExpressions.Regex.Replace(html, @"\*(.+?)\*", "<em>$1</em>");
            html = System.Text.RegularExpressions.Regex.Replace(html, @"__(.+?)__", "<strong>$1</strong>");
            html = System.Text.RegularExpressions.Regex.Replace(html, @"_(.+?)_", "<em>$1</em>");
            html = System.Text.RegularExpressions.Regex.Replace(html, @"`([^`]+)`", "<code>$1</code>");
            html = System.Text.RegularExpressions.Regex.Replace(html, @"```(\w*)\n([\s\S]*?)```", "<pre><code>$2</code></pre>");
            html = System.Text.RegularExpressions.Regex.Replace(html, @"^>\s+(.+)$", "<blockquote>$1</blockquote>", System.Text.RegularExpressions.RegexOptions.Multiline);
            html = System.Text.RegularExpressions.Regex.Replace(html, @"^---+$", "<hr/>", System.Text.RegularExpressions.RegexOptions.Multiline);
            html = System.Text.RegularExpressions.Regex.Replace(html, @"^\*\*\*+$", "<hr/>", System.Text.RegularExpressions.RegexOptions.Multiline);
            html = System.Text.RegularExpressions.Regex.Replace(html, @"^[\*\-]\s+(.+)$", "<li>$1</li>", System.Text.RegularExpressions.RegexOptions.Multiline);
            html = System.Text.RegularExpressions.Regex.Replace(html, @"^\d+\.\s+(.+)$", "<li>$1</li>", System.Text.RegularExpressions.RegexOptions.Multiline);
            html = System.Text.RegularExpressions.Regex.Replace(html, @"(<li>.*?</li>\n?)+", m => "<ul>" + m.Value + "</ul>");
            var lines = html.Split('\n');
            var result = new System.Text.StringBuilder();
            bool inParagraph = false;
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed))
                {
                    if (inParagraph) { result.Append("</p>"); inParagraph = false; }
                    result.AppendLine();
                }
                else if (trimmed.StartsWith("<h") || trimmed.StartsWith("<ul") || trimmed.StartsWith("<ol") ||
                         trimmed.StartsWith("<pre") || trimmed.StartsWith("<blockquote") || trimmed.StartsWith("<hr"))
                {
                    if (inParagraph) { result.Append("</p>"); inParagraph = false; }
                    result.AppendLine(line);
                }
                else
                {
                    if (!inParagraph) { result.Append("<p>"); inParagraph = true; }
                    result.Append(line + " ");
                }
            }
            if (inParagraph) result.Append("</p>");
            return result.ToString();
        }
        private async void LoadVersionsAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "OrangLauncher/1.0 (github.com/Orang-Studio/OrangLaunch)");
                var response = await client.GetStringAsync($"https://api.modrinth.com/v2/project/{_projectId}/version");
                var versions = JsonSerializer.Deserialize<List<JsonElement>>(response);
                _versions.Clear();
                if (versions != null)
                {
                    foreach (var v in versions)
                    {
                        var gameVersions = new List<string>();
                        if (v.TryGetProperty("game_versions", out var gv))
                            foreach (var ver in gv.EnumerateArray()) gameVersions.Add(ver.GetString() ?? "");
                        var loaders = new List<string>();
                        if (v.TryGetProperty("loaders", out var ld))
                            foreach (var loader in ld.EnumerateArray()) loaders.Add(loader.GetString() ?? "");
                        string? downloadUrl = null; string? fileName = null;
                        if (v.TryGetProperty("files", out var files))
                        {
                            foreach (var file in files.EnumerateArray())
                            {
                                if (file.TryGetProperty("primary", out var primary) && primary.GetBoolean())
                                { downloadUrl = file.GetProperty("url").GetString(); fileName = file.GetProperty("filename").GetString(); break; }
                            }
                            if (downloadUrl == null)
                            {
                                var firstFile = files.EnumerateArray().GetEnumerator();
                                if (firstFile.MoveNext())
                                { downloadUrl = firstFile.Current.GetProperty("url").GetString(); fileName = firstFile.Current.GetProperty("filename").GetString(); }
                            }
                        }
                        _versions.Add(new ProjectVersion
                        {
                            Name = v.TryGetProperty("name", out var vn) ? vn.GetString() : null,
                            VersionNumber = v.GetProperty("version_number").GetString(),
                            GameVersions = string.Join(", ", gameVersions.Count > 3 ? gameVersions.GetRange(0, 3).Concat([$"+{gameVersions.Count - 3} more"]) : gameVersions),
                            Loaders = string.Join(", ", loaders),
                            DatePublished = v.TryGetProperty("date_published", out var dp) ? DateTime.Parse(dp.GetString() ?? "").ToString("MMM dd, yyyy") : "",
                            Changelog = v.TryGetProperty("changelog", out var cl) ? cl.GetString() : "",
                            VersionId = v.GetProperty("id").GetString(),
                            DownloadUrl = downloadUrl, FileName = fileName
                        });
                    }
                }
                VersionsListBox.ItemsSource = _versions;
                // Prefill the changelog tab with the newest version so it is never blank.
                if (_versions.Count > 0) ShowChangelog(_versions[0], switchTab: false);
            }
            catch (Exception ex)
            {
                await ShowMessageAsync($"Failed to load versions: {ex.Message}", "Error");
            }
        }
        private void ShowChangelog(ProjectVersion version, bool switchTab)
        {
            ChangelogVersionText.Text = $"Version {version.VersionNumber}";
            ChangelogDateText.Text = version.DatePublished;
            ChangelogTextBlock.Text = string.IsNullOrWhiteSpace(version.Changelog)
                ? "No changelog available."
                : StripMarkdown(version.Changelog);
            if (switchTab) DetailsPivot.SelectedIndex = 2;
        }
        private void VersionsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VersionsListBox.SelectedItem is ProjectVersion version)
                ShowChangelog(version, switchTab: true);
        }
        /// <summary>Reduces markdown to readable plain text for native fallback rendering.</summary>
        internal static string StripMarkdown(string markdown)
        {
            if (string.IsNullOrEmpty(markdown)) return "";
            var t = markdown.Replace("\r\n", "\n");
            string R(string input, string pat, string rep,
                System.Text.RegularExpressions.RegexOptions o = System.Text.RegularExpressions.RegexOptions.None) =>
                System.Text.RegularExpressions.Regex.Replace(input, pat, rep, o);
            const System.Text.RegularExpressions.RegexOptions M = System.Text.RegularExpressions.RegexOptions.Multiline;
            const System.Text.RegularExpressions.RegexOptions I = System.Text.RegularExpressions.RegexOptions.IgnoreCase;
            t = R(t, @"\[!\[[^\]]*\]\([^)]*\)\]\([^)]*\)", "");       // badge links
            t = R(t, @"!\[[^\]]*\]\([^)]*\)", "");                    // images
            t = R(t, @"\[([^\]]+)\]\(([^)]+)\)", "$1");               // links -> text
            t = R(t, @"<img[^>]*>", "", I);
            t = R(t, @"<br\s*/?>", "\n", I);
            t = R(t, @"<[^>]+>", "");                                 // any other html
            t = R(t, @"^#{1,6}\s*(.+)$", "$1", M);                    // headings -> text
            t = R(t, @"^>\s?", "", M);                                // blockquotes
            t = R(t, @"^[-*_]{3,}\s*$", "───", M);     // rules
            t = t.Replace("***", "").Replace("**", "").Replace("`", "");
            t = R(t, @"\n{3,}", "\n\n");
            return t.Trim();
        }
        private void CopyLinkButton_Click(object sender, RoutedEventArgs e)
        {
            var url = $"https://modrinth.com/{_projectType}/{_projectSlug}";
            var dp = new DataPackage();
            dp.SetText(url);
            Clipboard.SetContent(dp);
            CopyLinkButton.Content = "Copied!";
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (s, args) => { CopyLinkButton.Content = "Copy Link"; timer.Stop(); };
            timer.Start();
        }
        private void OpenInBrowserButton_Click(object sender, RoutedEventArgs e)
        {
            var url = $"https://modrinth.com/{_projectType}/{_projectSlug}";
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { }
        }
        private async void InstallVersionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ProjectVersion version)
            {
                if (string.IsNullOrEmpty(version.DownloadUrl) || string.IsNullOrEmpty(version.FileName))
                {
                    await ShowMessageAsync("No download available for this version.", "Error");
                    return;
                }
                btn.IsEnabled = false; btn.Content = "...";
                try
                {
                    var subDir = _projectType switch
                    {
                        "resourcepack" => "resourcepacks",
                        "shader" => "shaderpacks",
                        "datapack" => "datapacks",
                        _ => "mods"
                    };
                    var basePath = Path.Combine(Managers.PlatformPaths.GetMinecraftDir(), subDir);
                    Directory.CreateDirectory(basePath);
                    using var client = new HttpClient();
                    client.DefaultRequestHeaders.Add("User-Agent", "OrangLauncher/1.0");
                    var fileBytes = await client.GetByteArrayAsync(version.DownloadUrl);
                    var filePath = Path.Combine(basePath, version.FileName);
                    await File.WriteAllBytesAsync(filePath, fileBytes);
                    await ShowMessageAsync($"Installed {version.FileName} successfully!", "Success");
                }
                catch (Exception ex) { await ShowMessageAsync($"Failed to install: {ex.Message}", "Error"); }
                finally { btn.Content = "Install"; btn.IsEnabled = true; }
            }
        }
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
        private async Task ShowMessageAsync(string message, string title = "")
        {
            var dialog = new ContentDialog { Title = title, Content = message, CloseButtonText = "OK", XamlRoot = Content.XamlRoot };
            await dialog.ShowAsync();
        }
    }
}