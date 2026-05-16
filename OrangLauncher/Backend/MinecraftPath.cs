using System;
using System.IO;
namespace OrangLauncher.Backend
{
    public class MinecraftPath
    {
        public string BasePath { get; }
        public string Versions => Path.Combine(BasePath, "versions");
        public string Libraries => Path.Combine(BasePath, "libraries");
        public string Assets => Path.Combine(BasePath, "assets");
        public string AssetsIndexes => Path.Combine(Assets, "indexes");
        public string AssetsObjects => Path.Combine(Assets, "objects");
        public string Runtime => Path.Combine(BasePath, "runtime");
        public string Natives => Path.Combine(BasePath, "natives");
        public MinecraftPath(string basePath)
        {
            BasePath = basePath;
        }
        public MinecraftPath() : this(GetDefaultPath()) { }
        public static string GetDefaultPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                ".minecraft");
        }
        public void CreateDirectories()
        {
            Directory.CreateDirectory(BasePath);
            Directory.CreateDirectory(Versions);
            Directory.CreateDirectory(Libraries);
            Directory.CreateDirectory(Assets);
            Directory.CreateDirectory(AssetsIndexes);
            Directory.CreateDirectory(AssetsObjects);
            Directory.CreateDirectory(Runtime);
        }
        public string GetVersionDir(string versionId) =>
            Path.Combine(Versions, versionId);
        public string GetVersionJsonPath(string versionId) =>
            Path.Combine(GetVersionDir(versionId), $"{versionId}.json");
        public string GetVersionJarPath(string versionId) =>
            Path.Combine(GetVersionDir(versionId), $"{versionId}.jar");
        public string GetNativesDir(string versionId) =>
            Path.Combine(GetVersionDir(versionId), "natives");
    }
}