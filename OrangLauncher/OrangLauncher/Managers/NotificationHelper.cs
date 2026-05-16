using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
namespace OrangLauncher.Managers
{
    public static class NotificationHelper
    {
        private static bool _initialized;
        private static string? _iconPath;
        public static void Initialize()
        {
            if (_initialized) return;
            try
            {
                var manager = AppNotificationManager.Default;
                manager.Register();
                _initialized = true;
                _iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Other", "images", "orange.png");
                if (!File.Exists(_iconPath)) _iconPath = null;
            }
            catch { }
        }
        public static void ShowNotification(string title, string body)
        {
            try
            {
                if (!_initialized) Initialize();
                if (_iconPath != null)
                {
                    var xml = $@"<toast>
  <visual>
    <binding template=""ToastGeneric"">
      <image placement=""appLogoOverride"" src=""file:///{_iconPath.Replace('\\', '/')}"" hint-crop=""circle""/>
      <text>{EscapeXml(title)}</text>
      <text>{EscapeXml(body)}</text>
    </binding>
  </visual>
</toast>";
                    var notification = new AppNotification(xml);
                    AppNotificationManager.Default.Show(notification);
                }
                else
                {
                    var builder = new AppNotificationBuilder()
                        .AddText(title)
                        .AddText(body);
                    AppNotificationManager.Default.Show(builder.BuildNotification());
                }
            }
            catch { }
        }
        private static string EscapeXml(string s) =>
            s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}