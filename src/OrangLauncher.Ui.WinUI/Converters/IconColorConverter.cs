using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
namespace OrangLauncher.Converters
{
    public class ThemedIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (parameter is string iconName)
            {
                bool isDarkTheme = IsDarkTheme();
                string folder = isDarkTheme ? "icons_white" : "icons";
                return $"ms-appx:///Other/images/{folder}/{iconName}";
            }
            return value;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
        private static bool IsDarkTheme()
        {
            if (App.MainAppWindow?.Content is FrameworkElement fe)
                return fe.ActualTheme == ElementTheme.Dark;
            return true;
        }
    }
    public class IconForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isDarkTheme = IsDarkTheme();
            return new SolidColorBrush(isDarkTheme
                ? Microsoft.UI.Colors.White
                : Microsoft.UI.Colors.Black);
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
        private static bool IsDarkTheme()
        {
            if (App.MainAppWindow?.Content is FrameworkElement fe)
                return fe.ActualTheme == ElementTheme.Dark;
            return true;
        }
    }
    public class FileNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is string path ? System.IO.Path.GetFileName(path) : value;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is bool b && b ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value is Visibility v && v == Visibility.Visible;
        }
    }
}