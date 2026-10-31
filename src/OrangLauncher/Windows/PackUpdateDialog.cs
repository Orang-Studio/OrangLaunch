using System.Windows;
using System.Windows.Controls;
using OrangLauncher.Managers;
using static OrangLauncher.UiKit;
namespace OrangLauncher
{
    public class PackUpdateDialog : OlWindow
    {
        private readonly MainWindow _launcher;
        private readonly string _dir, _loader, _gameVersion, _title;
        private readonly string[] _exts;
        private readonly Action? _onDone;
        private readonly CancellationTokenSource _cts = new();
        private readonly TextBlock _head;
        private readonly ProgressBar _bar = new() { Minimum = 0, Maximum = 100, Margin = new Thickness(0, 10, 0, 6) };
        private readonly TextBlock _status = Label(T("QT_HASHING_FILES", "Hashing files..."), "muted", true);
        private readonly StackPanel _items = new();
        private readonly ScrollViewer _scroll;
        private readonly CheckBox _force = new() { Content = T("QT_ALLOW_DOWNGRADES", "Allow downgrades / re-downloads (force)"), Visibility = Visibility.Collapsed, Margin = new Thickness(0, 8, 0, 0) };
        private readonly Button _selectAll, _deselect, _apply;
        private readonly List<(CheckBox Box, UpdateCandidate Candidate)> _rows = new();
        public PackUpdateDialog(MainWindow launcher, string directory, string loader, string gameVersion, string[] exts, string title, Action? onDone)
            : base(title, 680, 480)
        {
            _launcher = launcher;
            _dir = directory;
            _loader = loader;
            _gameVersion = gameVersion;
            _exts = exts;
            _title = title;
            _onDone = onDone;
            var root = new DockPanel { Margin = new Thickness(18, 14, 18, 14) };
            _head = Label(T("QT_CHECKING_ON_MODRINTH", "Checking {name} on Modrinth...", ("name", System.IO.Path.GetFileName(directory))), "h2");
            DockPanel.SetDock(_head, Dock.Top);
            root.Children.Add(_head);
            DockPanel.SetDock(_bar, Dock.Top);
            root.Children.Add(_bar);
            DockPanel.SetDock(_status, Dock.Top);
            root.Children.Add(_status);
            _selectAll = Button(T("SELECT_ALL", "Select all"), () => SetAll(true));
            _deselect = Button(T("DESELECT_ALL", "Deselect all"), () => SetAll(false));
            _apply = Button(T("QT_UPDATE_SELECTED", "Update selected"), ApplySelected, "accent");
            foreach (var b in new[] { _selectAll, _deselect, _apply }) b.Visibility = Visibility.Collapsed;
            var buttons = Row(8, _selectAll, _deselect, _apply, Button(T("CANCEL", "Cancel"), () => { _cts.Cancel(); Close(); }));
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            buttons.Margin = new Thickness(0, 10, 0, 0);
            DockPanel.SetDock(buttons, Dock.Bottom);
            root.Children.Add(buttons);
            DockPanel.SetDock(_force, Dock.Bottom);
            root.Children.Add(_force);
            _scroll = Scroll(_items);
            _scroll.Margin = new Thickness(0, 8, 0, 0);
            _scroll.Visibility = Visibility.Collapsed;
            root.Children.Add(_scroll);
            Content = root;
            Closed += (_, _) => _cts.Cancel();
        }
        private void SetAll(bool value)
        {
            foreach (var (box, _) in _rows) box.IsChecked = value;
        }
        public async void Start()
        {
            Show();
            try
            {
                var list = await Task.Run(() => ModrinthUpdater.ScanAsync(_dir, _loader, _gameVersion, _exts,
                    (cur, total, msg) => OnUi(() => { _bar.Value = cur * 100.0 / Math.Max(1, total); _status.Text = msg; }), _cts.Token));
                if (_cts.IsCancellationRequested) return;
                if (list.Count == 0)
                {
                    Close();
                    Info(_title, T("QT_ALL_UP_TO_DATE", "Everything is up to date (or not found on Modrinth)."));
                    _onDone?.Invoke();
                    return;
                }
                var downgrades = list.Count(c => c.IsDowngrade);
                _head.Text = T("QT_UPDATES_AVAILABLE", "Updates available");
                _bar.Value = 100;
                _status.Text = T("QT_UPDATES_FOUND", "{n} update(s) found", ("n", list.Count - downgrades)) +
                    (downgrades > 0 ? ", " + T("QT_DOWNGRADES_SKIPPED", "{n} skipped by downgrade protection", ("n", downgrades)) : "");
                foreach (var c in list)
                {
                    var text = $"{c.ProjectTitle}: {(string.IsNullOrEmpty(c.CurrentVersion) ? "?" : c.CurrentVersion)}  →  {c.NewVersion}    ({c.FileName})" +
                        (c.IsDowngrade ? $"  [{T("QT_OLDER_THAN_INSTALLED", "older than installed")}]" : "");
                    var box = new CheckBox { Content = text, IsChecked = !c.IsDowngrade, Margin = new Thickness(0, 3, 0, 3) };
                    box.SetResourceReference(FrameworkElement.StyleProperty, "OlTickBox");
                    if (c.IsDowngrade) box.Opacity = 0.6;
                    _rows.Add((box, c));
                    _items.Children.Add(box);
                }
                _scroll.Visibility = Visibility.Visible;
                _force.Visibility = downgrades > 0 ? Visibility.Visible : Visibility.Collapsed;
                foreach (var b in new[] { _selectAll, _deselect, _apply }) b.Visibility = Visibility.Visible;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _status.Text = T("QT_FAILED_WITH", "Failed: {error}", ("error", ex.Message));
                _bar.Value = 0;
            }
        }
        private async void ApplySelected()
        {
            var chosen = _rows.Where(r => r.Box.IsChecked == true).Select(r => r.Candidate).ToList();
            if (chosen.Count == 0) return;
            if (chosen.Any(c => c.IsDowngrade) && _force.IsChecked != true)
                chosen = chosen.Where(c => !c.IsDowngrade).ToList();
            _apply.IsEnabled = false;
            _head.Text = T("QT_UPDATING", "Updating...");
            _bar.Value = 0;
            int done = 0;
            var errors = new List<string>();
            for (int i = 0; i < chosen.Count; i++)
            {
                if (_cts.IsCancellationRequested) break;
                var c = chosen[i];
                _bar.Value = i * 100.0 / chosen.Count;
                _status.Text = T("QT_UPDATING_NAME", "Updating {name}...", ("name", c.FileName));
                var (ok, msg) = await ModrinthUpdater.ApplyAsync(c);
                if (ok) done++; else errors.Add($"{c.FileName}: {msg}");
            }
            Close();
            var text = T("QT_UPDATED_N_OF", "Updated {done} of {total}.", ("done", done), ("total", chosen.Count));
            if (errors.Count > 0) text += "\n\n" + string.Join("\n", errors.Take(10));
            _launcher.AppendLog($"[Updater] {text}");
            Info(_title, text);
            _onDone?.Invoke();
        }
    }
}