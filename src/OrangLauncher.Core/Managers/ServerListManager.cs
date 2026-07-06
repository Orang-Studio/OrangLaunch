using OrangLauncher.Models;
using fNbt;
namespace OrangLauncher.Managers
{
    public class ServerListManager
    {
        private static ServerListManager? _instance;
        public static ServerListManager Instance => _instance ??= new ServerListManager();
        public List<ServerInfo> LoadServers(string minecraftDir)
        {
            var servers = new List<ServerInfo>();
            var serversDatPath = Path.Combine(minecraftDir, "servers.dat");
            if (File.Exists(serversDatPath))
            {
                try
                {
                    var nbtFile = new NbtFile();
                    nbtFile.LoadFromFile(serversDatPath);
                    var rootTag = nbtFile.RootTag;
                    var serversList = rootTag.Get<NbtList>("servers");
                    if (serversList != null)
                    {
                        foreach (var serverTag in serversList)
                        {
                            if (serverTag is NbtCompound compound)
                            {
                                var ip = compound.Get<NbtString>("ip")?.Value ?? "";
                                if (ip.Equals("hypixel.net", StringComparison.OrdinalIgnoreCase))
                                {
                                    ip = "mc.hypixel.net";
                                }
                                var server = new ServerInfo
                                {
                                    Name = compound.Get<NbtString>("name")?.Value ?? "",
                                    Ip = ip,
                                    IsHidden = compound.Get<NbtByte>("hideAddress")?.Value == 1,
                                    PreventsChatReports = compound.Get<NbtByte>("preventsChatReports")?.Value == 1
                                };
                                var iconTag = compound.Get<NbtString>("icon");
                                if (iconTag != null)
                                {
                                    server.Icon = iconTag.Value;
                                }
                                var acceptTexturesTag = compound.Get<NbtByte>("acceptTextures");
                                if (acceptTexturesTag != null)
                                {
                                    server.AcceptTextures = acceptTexturesTag.Value;
                                }
                                servers.Add(server);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading servers.dat: {ex.Message}");
                }
            }
            return servers;
        }
        public void SaveServers(string minecraftDir, List<ServerInfo> servers)
        {
            Directory.CreateDirectory(minecraftDir);
            var serversDatPath = Path.Combine(minecraftDir, "servers.dat");
            System.Diagnostics.Debug.WriteLine($"[ServerListManager] Saving servers to: {serversDatPath}");
            System.Diagnostics.Debug.WriteLine($"[ServerListManager] Server count: {servers.Count}");
            int maxRetries = 5;
            int retryDelayMs = 200;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    if (File.Exists(serversDatPath))
                    {
                        var backupPath = serversDatPath + ".bak";
                        File.Copy(serversDatPath, backupPath, true);
                        System.Diagnostics.Debug.WriteLine($"[ServerListManager] Backup created at: {backupPath}");
                    }
                    var rootTag = new NbtCompound("")
                    {
                        new NbtList("servers", NbtTagType.Compound)
                    };
                    var serversList = rootTag.Get<NbtList>("servers");
                    if (serversList != null)
                    {
                        foreach (var server in servers)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ServerListManager] Adding server: {server.Name} ({server.Ip})");
                            var serverCompound = new NbtCompound
                            {
                                new NbtString("name", server.Name),
                                new NbtString("ip", server.Ip)
                            };
                            if (server.IsHidden)
                            {
                                serverCompound.Add(new NbtByte("hideAddress", 1));
                            }
                            if (server.PreventsChatReports)
                            {
                                serverCompound.Add(new NbtByte("preventsChatReports", 1));
                            }
                            if (!string.IsNullOrEmpty(server.Icon))
                            {
                                serverCompound.Add(new NbtString("icon", server.Icon));
                            }
                            if (server.AcceptTextures.HasValue)
                            {
                                serverCompound.Add(new NbtByte("acceptTextures", (byte)server.AcceptTextures.Value));
                            }
                            serversList.Add(serverCompound);
                        }
                    }
                    var nbtFile = new NbtFile(rootTag);
                    using (var memoryStream = new MemoryStream())
                    {
                        nbtFile.SaveToStream(memoryStream, NbtCompression.None);
                        var bytes = memoryStream.ToArray();
                        using (var verifyStream = new MemoryStream(bytes))
                        {
                            var verifyFile = new NbtFile();
                            verifyFile.LoadFromStream(verifyStream, NbtCompression.AutoDetect);
                            var verifyServers = verifyFile.RootTag.Get<NbtList>("servers");
                            if (verifyServers == null || verifyServers.Count != servers.Count)
                            {
                                throw new Exception($"NBT verification failed: expected {servers.Count} servers, got {verifyServers?.Count ?? 0}");
                            }
                            System.Diagnostics.Debug.WriteLine($"[ServerListManager] NBT verification passed: {verifyServers.Count} servers");
                        }
                        var tempPath = serversDatPath + ".tmp";
                        File.WriteAllBytes(tempPath, bytes);
                        if (File.Exists(serversDatPath))
                        {
                            File.Delete(serversDatPath);
                        }
                        File.Move(tempPath, serversDatPath);
                    }
                    System.Diagnostics.Debug.WriteLine($"[ServerListManager] Successfully saved {servers.Count} servers");
                    System.Diagnostics.Debug.WriteLine($"[ServerListManager] File exists: {File.Exists(serversDatPath)}");
                    if (File.Exists(serversDatPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"[ServerListManager] File size: {new FileInfo(serversDatPath).Length} bytes");
                    }
                    break;
                }
                catch (IOException ex) when (attempt < maxRetries)
                {
                    System.Diagnostics.Debug.WriteLine($"[ServerListManager] Attempt {attempt} failed: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[ServerListManager] Retrying in {retryDelayMs}ms...");
                    System.Threading.Thread.Sleep(retryDelayMs);
                    retryDelayMs *= 2;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ServerListManager] Error saving servers.dat: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[ServerListManager] Stack trace: {ex.StackTrace}");
                    var backupPath = serversDatPath + ".bak";
                    if (File.Exists(backupPath))
                    {
                        try
                        {
                            File.Copy(backupPath, serversDatPath, true);
                            System.Diagnostics.Debug.WriteLine($"[ServerListManager] Restored from backup");
                        }
                        catch { }
                    }
                    if (attempt == maxRetries)
                    {
                        throw; 
                    }
                }
            }
        }
    }
}