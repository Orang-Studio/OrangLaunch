namespace OrangLauncher.Managers
{
    public static class SetupFlags
    {
        public static bool IsSetupDone() => LauncherSettings.GetBool("setupDone");
        public static void MarkSetupDone(bool done = true) => LauncherSettings.Set("setupDone", done);
}   }