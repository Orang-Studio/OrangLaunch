using OrangLauncher.Managers;
namespace OrangLauncher
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            if (GameBarFix.IsNoopLaunch(args)) return;
            StartupState.CaptureArgs(args);
            if (StartupState.TryHandOffToRunningInstance()) return;
            StartupState.RegisterMrpackFileAssociation();
            StartupState.StartListening();
            EntryPoint.Run();
}   }   }