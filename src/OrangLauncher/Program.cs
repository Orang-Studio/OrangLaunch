using OrangLauncher.Managers;
namespace OrangLauncher.Host
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (ctx, name) =>
            {
                if (name.Name is "Microsoft.Web.WebView2.Core" or "Microsoft.Web.WebView2.Wpf")
                {
                    var path = System.IO.Path.Combine(AppContext.BaseDirectory, "UiWpf", name.Name + ".dll");
                    if (System.IO.File.Exists(path))
                    {
                        var asm = ctx.LoadFromAssemblyPath(path);
                        if (name.Name == "Microsoft.Web.WebView2.Core")
                        {
                            System.Runtime.InteropServices.NativeLibrary.SetDllImportResolver(asm, (lib, _, _) =>
                                lib.StartsWith("WebView2Loader", StringComparison.OrdinalIgnoreCase)
                                    ? System.Runtime.InteropServices.NativeLibrary.Load(
                                        System.IO.Path.Combine(AppContext.BaseDirectory, "UiWpf", "WebView2Loader.dll"))
                                    : IntPtr.Zero);
                        }
                        return asm;
                    }
                }
                return null;
            };
            StartupState.CaptureArgs(args);
            StartupState.RegisterMrpackFileAssociation();
            var ui = UiModeManager.Resolve(args);
            if (ui == UiModeManager.WinUi)
                OrangLauncher.Ui.WinUI.EntryPoint.Run();
            else
                OrangLauncher.Ui.Wpf.EntryPoint.Run();
}}}