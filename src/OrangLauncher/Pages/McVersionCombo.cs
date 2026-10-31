using System.Windows;
using System.Windows.Controls;
using OrangLauncher.Managers;
using static OrangLauncher.UiKit;
namespace OrangLauncher.Pages
{
    public class McVersionCombo : ComboBox
    {
        private bool _refreshed;
        public event Action<string>? VersionChanged;
        private bool _suppress;
        public McVersionCombo()
        {
            SetResourceReference(StyleProperty, typeof(ComboBox));
            MaxDropDownHeight = 420;
            MinWidth = 180;
            Fill();
            SelectionChanged += (_, _) => { if (!_suppress) VersionChanged?.Invoke(Version); };
            Action<string> onSetting = key => { if (key is "showSnapshots" or "showBetas" or "showAlphas") OnUi(() => Fill()); };
            Loaded += (_, _) => LauncherSettings.Changed += onSetting;
            Unloaded += (_, _) => LauncherSettings.Changed -= onSetting;
            DropDownOpened += async (_, _) =>
            {
                if (_refreshed) return;
                _refreshed = true;
                await VersionService.GetVersionsAsync();
                Fill();
            };
            Loaded += async (_, _) =>
            {
                if (VersionService.Cached.Count > 0 && _refreshed) return;
                await VersionService.GetVersionsAsync();
                _refreshed = true;
                Fill();
            };
        }
        public string Version => SelectedItem as string ?? "";
        public void Fill(string? keep = null)
        {
            var before = Version;
            keep ??= before;
            var types = LauncherSettings.VisibleVersionTypes();
            var ids = VersionService.Cached.Where(v => types.Contains(v.Type)).Select(v => v.Id).ToList();
            if (!string.IsNullOrEmpty(keep) && !ids.Contains(keep)) ids.Insert(0, keep);
            _suppress = true;
            try
            {
                if (!ids.SequenceEqual(Items.OfType<string>()))
                {
                    Items.Clear();
                    foreach (var id in ids) Items.Add(id);
                }
                var idx = string.IsNullOrEmpty(keep) ? -1 : ids.IndexOf(keep);
                SelectedIndex = idx >= 0 ? idx : (ids.Count > 0 ? 0 : -1);
            }
            finally { _suppress = false; }
            if (Version != before) VersionChanged?.Invoke(Version);
        }
        public void Select(string version) => Fill(version);
    }
}