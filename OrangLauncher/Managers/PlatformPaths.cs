using System;
using System.IO;
namespace OrangLauncher.Managers
{
    public static class PlatformPaths
    {
        public static string GetMinecraftDir()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
        }
        public static string GetDataDir()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OrangLauncher");
            Directory.CreateDirectory(path);
            return path;
        }
        public static string GetInstancesDir()
        {
            var path = Path.Combine(GetDataDir(), "instances");
            Directory.CreateDirectory(path);
            return path;
        }
    }
}