using System.Windows.Controls;
using OrangLauncher.Managers;
using static OrangLauncher.UiKit;
namespace OrangLauncher.Pages
{
    public class JavaCombo : ComboBox
    {
        public const string Auto = "__auto__";
        private const string Browse = "__browse__";
        private bool _filling;
        public JavaCombo()
        {
            SetResourceReference(StyleProperty, typeof(ComboBox));
            MinWidth = 300;
            SelectionChanged += (_, _) =>
            {
                if (_filling || SelectedTag(this) as string != Browse) return;
                var path = PickFile(T("QT_SELECT_JAVA", "Select the java executable"), Filter(("Java", "java.exe;javaw.exe")) + "|" + AllFiles);
                if (path != null) Select(path);
                else SelectedIndex = 0;
            };
        }
        public async void Fill(string? current)
        {
            _filling = true;
            Items.Clear();
            Items.Add(Item(T("QT_JAVA_AUTO", "Automatic (recommended)"), Auto));
            Items.Add(Item(T("QT_JAVA_BROWSE", "Choose a java executable..."), Browse));
            SelectedIndex = 0;
            _filling = false;
            if (!string.IsNullOrEmpty(current)) Select(current);
            var found = await Task.Run(JavaResolver.FindAll);
            _filling = true;
            try
            {
                var keep = SelectedTag(this) as string;
                foreach (var (major, path) in found.OrderBy(j => j.Major).DistinctBy(j => j.Path, StringComparer.OrdinalIgnoreCase))
                {
                    if (Items.OfType<ComboBoxItem>().Any(i => string.Equals(i.Tag as string, path, StringComparison.OrdinalIgnoreCase))) continue;
                    Items.Insert(Items.Count - 1, Item($"Java {major}  -  {path}", path));
                }
                SelectTag(this, keep);
            }
            finally { _filling = false; }
        }
        public void Select(string path)
        {
            _filling = true;
            try
            {
                var existing = Items.OfType<ComboBoxItem>().FirstOrDefault(i => string.Equals(i.Tag as string, path, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    existing = Item($"{T("QT_JAVA_CUSTOM", "Custom")}  -  {path}", path);
                    Items.Insert(Items.Count - 1, existing);
                }
                SelectedItem = existing;
            }
            finally { _filling = false; }
        }
        public string? JavaPath => SelectedTag(this) is string s && s != Auto && s != Browse ? s : null;
}   }