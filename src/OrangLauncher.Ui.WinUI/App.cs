using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
namespace OrangLauncher
{
    public class App : Application, IXamlMetadataProvider
    {
        private readonly IXamlMetadataProvider _xamlMetadata =
            (IXamlMetadataProvider)Activator.CreateInstance(
                Type.GetType("OrangLauncher.Ui.WinUI.OrangLauncher_Ui_WinUI_XamlTypeInfo.XamlMetaDataProvider, OrangLauncher.Ui.WinUI")!)!;
        public IXamlType? GetXamlType(Type type) => _xamlMetadata.GetXamlType(type);
        public IXamlType? GetXamlType(string fullName) => _xamlMetadata.GetXamlType(fullName);
        public XmlnsDefinition[] GetXmlnsDefinitions() => _xamlMetadata.GetXmlnsDefinitions();
        private Window? m_window;
        private static void Log(string msg)
        {
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "oranglauncher-winui.log"), $"{DateTime.Now:HH:mm:ss.fff} {msg}\r\n"); } catch { }
        }
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            UnhandledException += (s, e) =>
            {
                Log($"UNHANDLED: {e.Exception}");
                e.Handled = true;
            };
            try
            {
                Log("merging XamlControlsResources");
                Resources.MergedDictionaries.Add(new XamlControlsResources());
                Log("merging ThemeResources");
                Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///OrangLauncher.Ui.WinUI/Other/themes/ThemeResources.xaml") });
                Log("merging Icons");
                Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///OrangLauncher.Ui.WinUI/Other/themes/Icons.xaml") });
                Log("creating MainWindow");
                m_window = new MainWindow();
                Log("activating MainWindow");
                m_window.Activate();
                Log("activated");
            }
            catch (Exception ex)
            {
                Log($"STARTUP FAILED: {ex}");
                throw;
            }
        }
        public static Window? MainAppWindow => ((App)Current).m_window;
    }
}