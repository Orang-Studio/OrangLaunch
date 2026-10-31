using System;
using System.Linq;
using System.Windows;
namespace OrangLauncher.Managers
{
    public static class ThemeManager
    {
        public static readonly string[] Themes = ["arc", "dark_prism", "light"];
        public static event Action<string>? ThemeChanged;
        public static string Current { get; private set; } = "arc";
        private static ResourceDictionary? _active;
        private static readonly (string Alias, string Key)[] Aliases =
        [
            ("OlButton", "DefaultButtonStyle"),
            ("OlAccentButton", "DefaultButtonStyle"),
            ("OlDangerButton", "DefaultButtonStyle"),
            ("OlFlatButton", "DefaultButtonStyle"),
            ("OlPlayButton", "PlayButtonStyle"),
            ("OlSwitch", "ModernToggleSwitchStyle")
        ];
        public static void EnsureControls()
        {
            var merged = Application.Current.Resources.MergedDictionaries;
            if (merged.Any(d => d.Source?.ToString().Contains("OlControls") == true)) return;
            merged.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/OrangLauncher;component/Styles/OlControls.xaml", UriKind.Absolute) });
        }
        private static readonly string[] AccentKeys = ["AccentPrimaryBrush", "AccentHoverBrush", "AccentPressedBrush", "ProgressBarBrush"];
        public static bool TryParseColor(string? text, out System.Windows.Media.Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var t = text.Trim();
            if (!t.StartsWith('#')) t = "#" + t;
            if (t.Length != 7) return false;
            try
            {
                color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(t);
                return true;
            }
            catch { return false; }
        }
        private static System.Windows.Media.Color Mix(System.Windows.Media.Color c, byte target, double amount)
            => System.Windows.Media.Color.FromRgb(
                (byte)(c.R + (target - c.R) * amount),
                (byte)(c.G + (target - c.G) * amount),
                (byte)(c.B + (target - c.B) * amount));
        public static void ApplyAccent(string? hex)
        {
            var res = Application.Current.Resources;
            if (!TryParseColor(hex, out var c))
            {
                foreach (var k in AccentKeys) res.Remove(k);
                ThemeChanged?.Invoke(Current);
                return;
            }
            System.Windows.Media.SolidColorBrush Brush(System.Windows.Media.Color col)
            {
                var b = new System.Windows.Media.SolidColorBrush(col);
                b.Freeze();
                return b;
            }
            res["AccentPrimaryBrush"] = Brush(c);
            res["ProgressBarBrush"] = Brush(c);
            res["AccentHoverBrush"] = Brush(Mix(c, 255, 0.2));
            res["AccentPressedBrush"] = Brush(Mix(c, 0, 0.2));
            ThemeChanged?.Invoke(Current);
        }
        public static void ApplyTheme(string themeKey)
        {
            string themeName = themeKey switch
            {
                "dark_prism" => "DarkPrismTheme.xaml",
                "light" => "LightTheme.xaml",
                _ => "ArcTheme.xaml"
            };
            try
            {
                var dict = new ResourceDictionary { Source = new Uri($"pack://application:,,,/OrangLauncher;component/Styles/themes/{themeName}", UriKind.Absolute) };
                foreach (var (alias, key) in Aliases)
                    if (dict.Contains(key)) dict[alias] = dict[key];
                var merged = Application.Current.Resources.MergedDictionaries;
                if (_active != null) merged.Remove(_active);
                merged.Insert(0, dict);
                _active = dict;
                Current = Themes.Contains(themeKey) ? themeKey : "arc";
                EnsureControls();
                ThemeChanged?.Invoke(Current);
            }
            catch { }
        }
    }
}
