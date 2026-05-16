using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace OrangLauncher.Converters
{
    public class ThemedIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string iconName)
            {
                bool isDarkTheme = IsDarkTheme();
                string folder = isDarkTheme ? "icons_white" : "icons";
                return $"pack://siteoforigin:,,,/Other/images/{folder}/{iconName}";
            }
            return value;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
        private bool IsDarkTheme()
        {
            try
            {
                var bg = Application.Current.Resources["BackgroundColor"] as SolidColorBrush;
                if (bg != null)
                {
                    var color = bg.Color;
                    double luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;
                    return luminance < 0.5;
                }
            }
            catch { }
            return false;
        }
    }
    public class IconForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isDarkTheme = IsDarkTheme();
            return isDarkTheme ? Brushes.White : Brushes.Black;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
        private bool IsDarkTheme()
        {
            try
            {
                var bg = Application.Current.Resources["BackgroundColor"] as SolidColorBrush;
                if (bg != null)
                {
                    var color = bg.Color;
                    double luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;
                    return luminance < 0.5;
                }
            }
            catch { }
            return false;
        }
    }
}