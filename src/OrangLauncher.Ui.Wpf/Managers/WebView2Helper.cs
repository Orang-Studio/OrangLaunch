using System;
using System.IO;
using Microsoft.Web.WebView2.Wpf;

namespace OrangLauncher.Managers
{
    public static class WebView2Helper
    {
        public static string UserDataFolder
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OrangStudio", "OrangLauncher", "webview2");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        // call before the control initializes. 
        public static void Prepare(WebView2 webView)
        {
            try
            {
                webView.CreationProperties ??= new CoreWebView2CreationProperties();
                webView.CreationProperties.UserDataFolder = UserDataFolder;
            }
            catch { }
        }
    }
}
