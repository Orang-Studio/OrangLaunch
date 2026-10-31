using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using fNbt;
using OrangLauncher.Models;
namespace OrangLauncher.Managers
{
    public class WorldInfo
    {
        public string Folder { get; set; } = "";
        public string Path { get; set; } = "";
        public string Name { get; set; } = "";
        public long Size { get; set; }
        public DateTime Modified { get; set; }
    }
    public static class WorldManager
    {
        public static List<WorldInfo> List(MinecraftInstance instance)
        {
            var result = new List<WorldInfo>();
            var saves = FileLinks.Resolve(instance.SavesDir);
            if (!Directory.Exists(saves)) return result;
            foreach (var dir in new DirectoryInfo(saves).GetDirectories().OrderByDescending(d => d.LastWriteTime))
            {
                var level = System.IO.Path.Combine(dir.FullName, "level.dat");
                if (!File.Exists(level)) continue;
                long size = 0;
                try { size = dir.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length); } catch { }
                result.Add(new WorldInfo
                {
                    Folder = dir.Name,
                    Path = dir.FullName,
                    Name = ReadLevelName(level) ?? dir.Name,
                    Size = size,
                    Modified = dir.LastWriteTime
                });
            }
            return result;
        }
        public static string? ReadLevelName(string levelDat)
        {
            try
            {
                var file = new NbtFile();
                file.LoadFromFile(levelDat);
                return file.RootTag.Get<NbtCompound>("Data")?.Get<NbtString>("LevelName")?.Value;
            }
            catch { return null; }
        }
        public static bool SetLevelName(string levelDat, string name)
        {
            try
            {
                var file = new NbtFile();
                file.LoadFromFile(levelDat);
                var data = file.RootTag.Get<NbtCompound>("Data");
                if (data == null) return false;
                try { File.Copy(levelDat, levelDat + "_old", true); } catch { }
                var tag = data.Get<NbtString>("LevelName");
                if (tag != null) tag.Value = name;
                else data.Add(new NbtString("LevelName", name));
                file.SaveToFile(levelDat, NbtCompression.GZip);
                return true;
            }
            catch { return false; }
        }
    }
    public class ServerPingResult
    {
        public int Online { get; set; }
        public int Max { get; set; }
        public long LatencyMs { get; set; }
        public string Motd { get; set; } = "";
        public string? Favicon { get; set; }
    }
    public static class ServerPinger
    {
        private static void WriteVarInt(Stream s, int value)
        {
            uint v = (uint)value;
            while (true)
            {
                if ((v & ~0x7Fu) == 0) { s.WriteByte((byte)v); return; }
                s.WriteByte((byte)((v & 0x7F) | 0x80));
                v >>= 7;
            }
        }
        private static int ReadVarInt(Stream s)
        {
            int value = 0, position = 0;
            while (true)
            {
                int b = s.ReadByte();
                if (b < 0) throw new EndOfStreamException();
                value |= (b & 0x7F) << position;
                if ((b & 0x80) == 0) return value;
                position += 7;
                if (position >= 32) throw new InvalidDataException("VarInt too big");
            }
        }
        private static void ReadExactly(Stream s, byte[] buffer)
        {
            int read = 0;
            while (read < buffer.Length)
            {
                var n = s.Read(buffer, read, buffer.Length - read);
                if (n <= 0) throw new EndOfStreamException();
                read += n;
            }
        }
        public static async Task<ServerPingResult?> PingAsync(string address, int timeoutMs = 3000)
        {
            var host = address.Trim();
            var port = 25565;
            var colon = host.LastIndexOf(':');
            if (colon > 0 && int.TryParse(host[(colon + 1)..], out var p)) { port = p; host = host[..colon]; }
            try
            {
                return await Task.Run(() =>
                {
                    using var client = new TcpClient();
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    if (!client.ConnectAsync(host, port).Wait(timeoutMs)) return null;
                    var latency = sw.ElapsedMilliseconds;
                    client.ReceiveTimeout = timeoutMs;
                    client.SendTimeout = timeoutMs;
                    using var stream = client.GetStream();
                    using var handshake = new MemoryStream();
                    WriteVarInt(handshake, 0x00);
                    WriteVarInt(handshake, 763);
                    var hostBytes = Encoding.UTF8.GetBytes(host);
                    WriteVarInt(handshake, hostBytes.Length);
                    handshake.Write(hostBytes);
                    handshake.WriteByte((byte)(port >> 8));
                    handshake.WriteByte((byte)(port & 0xFF));
                    WriteVarInt(handshake, 1);
                    using var packet = new MemoryStream();
                    WriteVarInt(packet, (int)handshake.Length);
                    handshake.WriteTo(packet);
                    WriteVarInt(packet, 1);
                    WriteVarInt(packet, 0x00);
                    stream.Write(packet.ToArray());
                    ReadVarInt(stream);
                    ReadVarInt(stream);
                    var len = ReadVarInt(stream);
                    var buf = new byte[len];
                    ReadExactly(stream, buf);
                    using var doc = JsonDocument.Parse(buf);
                    var root = doc.RootElement;
                    var result = new ServerPingResult { LatencyMs = latency };
                    if (root.TryGetProperty("players", out var players))
                    {
                        result.Online = players.TryGetProperty("online", out var o) ? o.GetInt32() : 0;
                        result.Max = players.TryGetProperty("max", out var m) ? m.GetInt32() : 0;
                    }
                    if (root.TryGetProperty("description", out var desc)) result.Motd = FlattenText(desc);
                    if (root.TryGetProperty("favicon", out var fav) && fav.ValueKind == JsonValueKind.String) result.Favicon = fav.GetString();
                    return result;
                });
            }
            catch { return null; }
        }
        private static string FlattenText(JsonElement e)
        {
            if (e.ValueKind == JsonValueKind.String) return System.Text.RegularExpressions.Regex.Replace(e.GetString() ?? "", "§.", "");
            var sb = new StringBuilder();
            if (e.ValueKind == JsonValueKind.Object)
            {
                if (e.TryGetProperty("text", out var t)) sb.Append(FlattenText(t));
                if (e.TryGetProperty("extra", out var extra) && extra.ValueKind == JsonValueKind.Array)
                    foreach (var x in extra.EnumerateArray()) sb.Append(FlattenText(x));
            }
            else if (e.ValueKind == JsonValueKind.Array)
                foreach (var x in e.EnumerateArray()) sb.Append(FlattenText(x));
            return sb.ToString();
        }
    }
}