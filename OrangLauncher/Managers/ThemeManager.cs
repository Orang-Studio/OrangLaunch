using System;
using System.IO;
using System.Windows;
namespace OrangLauncher.Managers
{
    public static class ThemeManager
    {
        public static void ApplyTheme(string themeKey)
        {
            string themeName = themeKey switch
            {
                "arc" => "ArcTheme.xaml",
                "dark_prism" => "DarkPrismTheme.xaml",
                "light" => "LightTheme.xaml",
                _ => "ArcTheme.xaml"
            };
            try
            {
                var toRemove = new System.Collections.Generic.List<ResourceDictionary>();
                foreach (var dict in Application.Current.Resources.MergedDictionaries)
                {
                    if (dict.Source?.ToString().Contains("Theme") == true)
                        toRemove.Add(dict);
                }
                foreach (var dict in toRemove)
                {
                    Application.Current.Resources.MergedDictionaries.Remove(dict);
                }
                string[] searchPaths = new[]
                {
                    $"pack://application:,,,/Other/themes/{themeName}",
                    $"pack://application:,,,/Themes/{themeName}",
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Other", "themes", themeName),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Themes", themeName)
                };
                foreach (var path in searchPaths)
                {
                    try
                    {
                        ResourceDictionary newTheme;
                        if (path.StartsWith("pack://"))
                        {
                            newTheme = new ResourceDictionary { Source = new Uri(path, UriKind.Absolute) };
                        }
                        else if (File.Exists(path))
                        {
                            newTheme = new ResourceDictionary { Source = new Uri(path, UriKind.Absolute) };
                        }
                        else
                        {
                            continue;
                        }
                        Application.Current.Resources.MergedDictionaries.Add(newTheme);
                        return;
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}