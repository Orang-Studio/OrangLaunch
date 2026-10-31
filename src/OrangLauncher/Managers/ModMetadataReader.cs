using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace OrangLauncher.Managers
{
    public class ModMetadata
    {
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string Id { get; set; } = "";
        public string Loader { get; set; } = "";
        public string Description { get; set; } = "";
    }
    public static class ModMetadataReader
    {
        private static readonly ConcurrentDictionary<(string, long, long), ModMetadata> Cache = new();
        public static ModMetadata? GetCached(string path)
        {
            try
            {
                var fi = new FileInfo(path);
                return Cache.TryGetValue((path, fi.Length, fi.LastWriteTimeUtc.Ticks), out var m) ? m : null;
            }
            catch { return null; }
        }
        public static ModMetadata Read(string path)
        {
            var fi = new FileInfo(path);
            if (!fi.Exists) return new ModMetadata();
            var key = (path, fi.Length, fi.LastWriteTimeUtc.Ticks);
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var meta = new ModMetadata();
            try
            {
                using var zip = ZipFile.OpenRead(path);
                string? ReadEntry(string name)
                {
                    var e = zip.GetEntry(name);
                    if (e == null) return null;
                    using var r = new StreamReader(e.Open());
                    return r.ReadToEnd();
                }
                JsonElement? ParseJson(string text)
                {
                    var opts = new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
                    try { return JsonDocument.Parse(text.Replace("\n", " ").Replace("\r", " ").Replace("\t", " "), opts).RootElement.Clone(); }
                    catch { return null; }
                }
                static string S(JsonElement e, string name) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) ? (v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ToString()) : "";
                string Manifest(string k)
                {
                    var mf = ReadEntry("META-INF/MANIFEST.MF");
                    if (mf == null) return "";
                    foreach (var line in mf.Split('\n'))
                        if (line.StartsWith(k + ":", StringComparison.OrdinalIgnoreCase)) return line[(k.Length + 1)..].Trim();
                    return "";
                }
                if (ReadEntry("fabric.mod.json") is string fabric && ParseJson(fabric) is JsonElement fj)
                {
                    meta = new ModMetadata { Name = S(fj, "name"), Version = S(fj, "version"), Id = S(fj, "id"), Loader = "fabric", Description = S(fj, "description") };
                    if (meta.Name.Length == 0) meta.Name = meta.Id;
                }
                else if (ReadEntry("quilt.mod.json") is string quilt && ParseJson(quilt) is JsonElement qj && qj.TryGetProperty("quilt_loader", out var ql))
                {
                    var md = ql.TryGetProperty("metadata", out var m) ? m : default;
                    meta = new ModMetadata { Name = S(md, "name"), Version = S(ql, "version"), Id = S(ql, "id"), Loader = "quilt", Description = S(md, "description") };
                    if (meta.Name.Length == 0) meta.Name = meta.Id;
                }
                else if ((ReadEntry("META-INF/neoforge.mods.toml") ?? ReadEntry("META-INF/mods.toml")) is string toml)
                {
                    var isNeo = zip.GetEntry("META-INF/neoforge.mods.toml") != null;
                    var section = Regex.Match(toml, @"\[\[mods\]\](.*?)(?=\n\s*\[|\z)", RegexOptions.Singleline);
                    var body = section.Success ? section.Groups[1].Value : toml;
                    string Val(string k)
                    {
                        var m = Regex.Match(body, $@"(?m)^\s*{k}\s*=\s*(?:""""""(.*?)""""""|""([^""]*)""|'([^']*)')", RegexOptions.Singleline);
                        return m.Success ? (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value).Trim() : "";
                    }
                    var version = Val("version");
                    if (version.Length == 0 || version.Contains("${")) version = Manifest("Implementation-Version");
                    meta = new ModMetadata { Name = Val("displayName"), Version = version, Id = Val("modId"), Loader = isNeo ? "neoforge" : "forge", Description = Val("description") };
                    if (meta.Name.Length == 0) meta.Name = meta.Id;
                }
                else if (ReadEntry("mcmod.info") is string mcmod && ParseJson(mcmod) is JsonElement mj)
                {
                    var first = mj.ValueKind == JsonValueKind.Array && mj.GetArrayLength() > 0 ? mj[0]
                        : mj.ValueKind == JsonValueKind.Object && mj.TryGetProperty("modList", out var ml) && ml.GetArrayLength() > 0 ? ml[0] : default;
                    if (first.ValueKind == JsonValueKind.Object)
                        meta = new ModMetadata { Name = S(first, "name"), Version = S(first, "version"), Id = S(first, "modid"), Loader = "forge", Description = S(first, "description") };
                }
                else
                {
                    var title = Manifest("Implementation-Title");
                    if (title.Length == 0) title = Manifest("Specification-Title");
                    if (title.Length > 0) meta = new ModMetadata { Name = title, Version = Manifest("Implementation-Version") };
                }
            }
            catch { }
            meta.Name = meta.Name.Trim();
            Cache[key] = meta;
            return meta;
        }
    }
}
