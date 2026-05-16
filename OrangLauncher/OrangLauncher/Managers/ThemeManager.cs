using Microsoft.UI.Xaml;
namespace OrangLauncher.Managers
{
    public static class ThemeManager
    {
        public static void ApplyTheme(string themeKey, FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = themeKey switch
            {
                "dark" => ElementTheme.Dark,
                "light" => ElementTheme.Light,
                "system" => ElementTheme.Default,
                _ => ElementTheme.Dark
            };
        }
        public static ElementTheme GetElementTheme(string themeKey)
        {
            return themeKey switch
            {
                "dark" => ElementTheme.Dark,
                "light" => ElementTheme.Light,
                "system" => ElementTheme.Default,
                _ => ElementTheme.Dark
            };
        }
        public static bool IsDarkTheme(FrameworkElement element)
        {
            var theme = element.ActualTheme;
            return theme == ElementTheme.Dark;
        }
    }
}