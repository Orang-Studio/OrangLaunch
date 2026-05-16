using System.Text.Json.Serialization;
namespace OrangLauncher.Backend
{
    public class VersionManifest
    {
        [JsonPropertyName("latest")]
        public VersionManifestLatest? Latest { get; set; }
        [JsonPropertyName("versions")]
        public List<VersionManifestEntry>? Versions { get; set; }
    }
    public class VersionManifestLatest
    {
        [JsonPropertyName("release")]
        public string? Release { get; set; }
        [JsonPropertyName("snapshot")]
        public string? Snapshot { get; set; }
    }
    public class VersionManifestEntry
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
        [JsonPropertyName("type")]
        public string Type { get; set; } = "release";
        [JsonPropertyName("url")]
        public string Url { get; set; } = "";
        [JsonPropertyName("releaseTime")]
        public string? ReleaseTime { get; set; }
        [JsonPropertyName("sha1")]
        public string? Sha1 { get; set; }
    }
    public class VersionMeta
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
        [JsonPropertyName("type")]
        public string? Type { get; set; }
        [JsonPropertyName("mainClass")]
        public string MainClass { get; set; } = "";
        [JsonPropertyName("inheritsFrom")]
        public string? InheritsFrom { get; set; }
        [JsonPropertyName("minecraftArguments")]
        public string? MinecraftArguments { get; set; }
        [JsonPropertyName("arguments")]
        public VersionArguments? Arguments { get; set; }
        [JsonPropertyName("libraries")]
        public List<VersionLibrary>? Libraries { get; set; }
        [JsonPropertyName("downloads")]
        public VersionDownloads? Downloads { get; set; }
        [JsonPropertyName("assetIndex")]
        public AssetIndexInfo? AssetIndex { get; set; }
        [JsonPropertyName("assets")]
        public string? Assets { get; set; }
        [JsonPropertyName("javaVersion")]
        public JavaVersionInfo? JavaVersion { get; set; }
        [JsonPropertyName("logging")]
        public Dictionary<string, LoggingConfig>? Logging { get; set; }
        [JsonPropertyName("complianceLevel")]
        public int ComplianceLevel { get; set; }
    }
    public class VersionArguments
    {
        [JsonPropertyName("game")]
        public List<object>? Game { get; set; }
        [JsonPropertyName("jvm")]
        public List<object>? Jvm { get; set; }
    }
    public class VersionLibrary
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("downloads")]
        public LibraryDownloads? Downloads { get; set; }
        [JsonPropertyName("rules")]
        public List<LibraryRule>? Rules { get; set; }
        [JsonPropertyName("natives")]
        public Dictionary<string, string>? Natives { get; set; }
        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }
    public class LibraryDownloads
    {
        [JsonPropertyName("artifact")]
        public LibraryArtifact? Artifact { get; set; }
        [JsonPropertyName("classifiers")]
        public Dictionary<string, LibraryArtifact>? Classifiers { get; set; }
    }
    public class LibraryArtifact
    {
        [JsonPropertyName("path")]
        public string? Path { get; set; }
        [JsonPropertyName("url")]
        public string? Url { get; set; }
        [JsonPropertyName("sha1")]
        public string? Sha1 { get; set; }
        [JsonPropertyName("size")]
        public long Size { get; set; }
    }
    public class LibraryRule
    {
        [JsonPropertyName("action")]
        public string Action { get; set; } = "allow";
        [JsonPropertyName("os")]
        public LibraryRuleOs? Os { get; set; }
    }
    public class LibraryRuleOs
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        [JsonPropertyName("arch")]
        public string? Arch { get; set; }
    }
    public class VersionDownloads
    {
        [JsonPropertyName("client")]
        public DownloadEntry? Client { get; set; }
        [JsonPropertyName("server")]
        public DownloadEntry? Server { get; set; }
        [JsonPropertyName("client_mappings")]
        public DownloadEntry? ClientMappings { get; set; }
    }
    public class DownloadEntry
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = "";
        [JsonPropertyName("sha1")]
        public string? Sha1 { get; set; }
        [JsonPropertyName("size")]
        public long Size { get; set; }
    }
    public class AssetIndexInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
        [JsonPropertyName("url")]
        public string Url { get; set; } = "";
        [JsonPropertyName("sha1")]
        public string? Sha1 { get; set; }
        [JsonPropertyName("size")]
        public long Size { get; set; }
        [JsonPropertyName("totalSize")]
        public long TotalSize { get; set; }
    }
    public class JavaVersionInfo
    {
        [JsonPropertyName("component")]
        public string Component { get; set; } = "java-runtime-gamma";
        [JsonPropertyName("majorVersion")]
        public int MajorVersion { get; set; } = 17;
    }
    public class LoggingConfig
    {
        [JsonPropertyName("argument")]
        public string? Argument { get; set; }
        [JsonPropertyName("file")]
        public LoggingFile? File { get; set; }
        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }
    public class LoggingFile
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
        [JsonPropertyName("url")]
        public string? Url { get; set; }
        [JsonPropertyName("sha1")]
        public string? Sha1 { get; set; }
        [JsonPropertyName("size")]
        public long Size { get; set; }
    }
    public class AssetIndex
    {
        [JsonPropertyName("objects")]
        public Dictionary<string, AssetObject>? Objects { get; set; }
    }
    public class AssetObject
    {
        [JsonPropertyName("hash")]
        public string Hash { get; set; } = "";
        [JsonPropertyName("size")]
        public long Size { get; set; }
    }
}