using System.Windows;
using System.Windows.Controls;
using OrangLauncher.Managers;
using static OrangLauncher.UiKit;
namespace OrangLauncher
{
    public class ModrinthVersionDialog : OlWindow
    {
        public ModrinthVersion? Chosen { get; private set; }
        private readonly TextBlock _status = Label(T("QT_VERSION_PICKER_LOADING", "Loading versions..."), "muted", true);
        private readonly ListBox _list = new();
        private readonly Button _install;
        public ModrinthVersionDialog(string title, Func<Task<List<ModrinthVersion>>> fetch, Func<ModrinthVersion, bool>? accept)
            : base(T("QT_VERSION_PICKER_TITLE", "{title} - versions", ("title", title)), 720, 480)
        {
            var root = new DockPanel { Margin = new Thickness(18, 14, 18, 14) };
            var head = Label(title, "h2");
            DockPanel.SetDock(head, Dock.Top);
            root.Children.Add(head);
            _status.Margin = new Thickness(0, 4, 0, 8);
            DockPanel.SetDock(_status, Dock.Top);
            root.Children.Add(_status);
            _install = Button(T("CONTENT_INSTALL_BTN", "Install"), Accept, "accent");
            _install.IsEnabled = false;
            var bar = Row(8, _install, Button(T("CANCEL", "Cancel"), Close));
            bar.HorizontalAlignment = HorizontalAlignment.Right;
            bar.Margin = new Thickness(0, 10, 0, 0);
            DockPanel.SetDock(bar, Dock.Bottom);
            root.Children.Add(bar);
            _list.MouseDoubleClick += (_, _) => Accept();
            root.Children.Add(_list);
            Content = root;
            Loaded += async (_, _) =>
            {
                try
                {
                    var versions = await fetch();
                    if (accept != null) versions = versions.Where(accept).ToList();
                    var recommended = ModrinthClient.PickBestVersion(versions);
                    var ordered = versions.OrderByDescending(v => v.DatePublished).ToList();
                    Fill(ordered, recommended);
                }
                catch (Exception ex)
                {
                    _status.Text = T("QT_SEARCH_FAILED", "Search failed: {error}", ("error", ex.Message));
                }
            };
        }
        private void Fill(List<ModrinthVersion> ordered, ModrinthVersion? recommended)
        {
            if (ordered.Count == 0)
            {
                _status.Text = T("QT_VERSION_PICKER_EMPTY", "No versions match the current filters.");
                return;
            }
            _status.Text = T("QT_VERSION_PICKER_COUNT", "{count} versions - newest first", ("count", ordered.Count));
            foreach (var v in ordered)
            {
                var name = string.IsNullOrEmpty(v.Name) ? v.VersionNumber : v.Name;
                var head = string.IsNullOrEmpty(v.VersionNumber) || name.Contains(v.VersionNumber) ? name : $"{name}  ({v.VersionNumber})";
                if (v.Id == recommended?.Id) head += "  ·  " + T("QT_VERSION_RECOMMENDED", "Recommended");
                var gv = string.Join(", ", v.GameVersions);
                if (gv.Length > 60) gv = gv[..57] + "...";
                var size = v.PrimaryFile is { Size: > 0 } f ? HumanSize(f.Size) : "";
                var detail = string.Join("  ·  ", new[] { gv, string.Join(", ", v.Loaders), v.VersionType.ToLowerInvariant(), v.DatePublished.ToString("yyyy-MM-dd"), size }.Where(x => x.Length > 0));
                var sp = new StackPanel { Tag = v };
                sp.Children.Add(Label(head, "h3"));
                sp.Children.Add(Label(detail, "muted"));
                _list.Items.Add(sp);
                if (v.Id == recommended?.Id) _list.SelectedItem = sp;
            }
            if (_list.SelectedItem == null) _list.SelectedIndex = 0;
            _install.IsEnabled = true;
        }
        private void Accept()
        {
            if (_list.SelectedItem is not FrameworkElement { Tag: ModrinthVersion v }) return;
            Chosen = v;
            DialogResult = true;
        }
    }
}