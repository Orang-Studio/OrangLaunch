using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Media.Imaging;
using OrangLauncher.Models;
namespace OrangLauncher.ViewModels
{
    public class ServerViewModel : INotifyPropertyChanged
    {
        private readonly HttpClient _httpClient = new();
        public ServerInfo Server { get; }
        private string _name;
        private string _ip;
        private string? _icon;
        private int _ping = -1;
        private int _playersOnline;
        private int _maxPlayers;
        private string? _pingError;
        private bool _isHidden;
        private bool _supportsQuickPlay;
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
        public string Ip { get => _ip; set { _ip = value; OnPropertyChanged(); } }
        public string? Icon { get => _icon; set { _icon = value; OnPropertyChanged(); } }
        public bool IsHidden { get => _isHidden; set { _isHidden = value; OnPropertyChanged(); } }
        public bool SupportsQuickPlay { get => _supportsQuickPlay; set { _supportsQuickPlay = value; OnPropertyChanged(); } }
        public int Ping
        {
            get => _ping;
            set
            {
                _ping = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PingText));
                OnPropertyChanged(nameof(PingIcon));
            }
        }
        public int PlayersOnline
        {
            get => _playersOnline;
            set
            {
                _playersOnline = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PlayerCountText));
            }
        }
        public int MaxPlayers
        {
            get => _maxPlayers;
            set
            {
                _maxPlayers = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PlayerCountText));
            }
        }
        public string? PingError
        {
            get => _pingError;
            set { _pingError = value; OnPropertyChanged(); }
        }
        public string PingText => Ping >= 0 ? $"{Ping}ms" : "Offline";
        public string PlayerCountText => MaxPlayers > 0 ? $"{PlayersOnline}/{MaxPlayers}" : "";
        public BitmapImage? PingIcon
        {
            get
            {
                string iconName;
                if (Ping < 0) iconName = "incompatible.png";
                else if (Ping < 50) iconName = "ping_5.png";
                else if (Ping < 100) iconName = "ping_4.png";
                else if (Ping < 200) iconName = "ping_3.png";
                else if (Ping < 300) iconName = "ping_2.png";
                else iconName = "ping_1.png";
                string[] searchPaths = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Other", "images", "ping", iconName),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", "ping", iconName),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Other", "images", "ping", iconName),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Other", "images", "ping", iconName),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Other", "images", "ping", iconName)
                };
                foreach (var path in searchPaths)
                {
                    var fullPath = Path.GetFullPath(path);
                    if (File.Exists(fullPath))
                    {
                        try
                        {
                            return new BitmapImage(new Uri(fullPath, UriKind.Absolute));
                        }
                        catch { }
                    }
                }
                return null;
            }
        }
        public ServerViewModel(ServerInfo server)
        {
            Server = server;
            _name = server.Name;
            _ip = server.Ip;
            _icon = server.Icon;
            _isHidden = server.IsHidden;
            // servers.dat stores the icon as base64 PNG; show it right away and let
            // the live ping favicon replace it later.
            if (!string.IsNullOrEmpty(server.Icon))
            {
                try
                {
                    var bytes = Convert.FromBase64String(server.Icon);
                    var ms = new MemoryStream(bytes);
                    var bmp = new BitmapImage();
                    bmp.SetSource(ms.AsRandomAccessStream());
                    ServerIconImage = bmp;
                }
                catch { }
            }
            _ = PingServerAsync();
        }
        private async Task PingServerAsync()
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var parts = Ip.Split(':');
                var host = parts[0];
                var port = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : 25565;
                using var client = new System.Net.Sockets.TcpClient();
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 5000;
                var connectTask = client.ConnectAsync(host, port);
                if (await Task.WhenAny(connectTask, Task.Delay(5000)) == connectTask)
                {
                    await connectTask;                    
                    var stream = client.GetStream();                    var handshake = new System.Collections.Generic.List<byte>();
                    handshake.Add(0x00);                    WriteVarInt(handshake, -1);
                    WriteString(handshake, host);
                    handshake.Add((byte)(port >> 8));
                    handshake.Add((byte)(port & 0xFF));
                    WriteVarInt(handshake, 1);
                    var handshakePacket = new System.Collections.Generic.List<byte>();
                    WriteVarInt(handshakePacket, handshake.Count);
                    handshakePacket.AddRange(handshake);
                    await stream.WriteAsync(handshakePacket.ToArray(), 0, handshakePacket.Count);                    var statusRequest = new byte[] { 0x01, 0x00 };                    await stream.WriteAsync(statusRequest, 0, statusRequest.Length);
                    sw.Stop();
                    Ping = (int)sw.ElapsedMilliseconds;                    var length = await ReadVarIntAsync(stream);
                    var packetId = await ReadVarIntAsync(stream);
                    var jsonLength = await ReadVarIntAsync(stream);
                    var buffer = new byte[jsonLength];
                    var totalRead = 0;
                    while (totalRead < jsonLength)
                    {
                        var read = await stream.ReadAsync(buffer, totalRead, jsonLength - totalRead);
                        if (read == 0) break;
                        totalRead += read;
                    }
                    var json = System.Text.Encoding.UTF8.GetString(buffer);
                    ParseServerStatus(json);
                }
                else
                {
                    Ping = -1;
                    PingError = "Connection timed out";
                }
            }
            catch (Exception ex)
            {
                Ping = -1;
                PingError = ex.Message;
            }
        }
        private void WriteVarInt(System.Collections.Generic.List<byte> buffer, int value)
        {
            uint uValue = (uint)value;
            do
            {
                byte temp = (byte)(uValue & 0x7F);
                uValue >>= 7;
                if (uValue != 0) temp |= 0x80;
                buffer.Add(temp);
            } while (uValue != 0);
        }
        private void WriteString(System.Collections.Generic.List<byte> buffer, string str)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(str);
            WriteVarInt(buffer, bytes.Length);
            buffer.AddRange(bytes);
        }
        private async Task<int> ReadVarIntAsync(System.Net.Sockets.NetworkStream stream)
        {
            int numRead = 0;
            int result = 0;
            byte read;
            do
            {
                var buffer = new byte[1];
                await stream.ReadAsync(buffer, 0, 1);
                read = buffer[0];
                int value = (read & 0x7F);
                result |= (value << (7 * numRead));
                numRead++;
                if (numRead > 5) throw new InvalidOperationException("VarInt is too big");
            } while ((read & 0x80) != 0);
            return result;
        }
        private void ParseServerStatus(string json)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("players", out var players))
                {
                    if (players.TryGetProperty("online", out var online))
                        PlayersOnline = online.GetInt32();
                    if (players.TryGetProperty("max", out var max))
                        MaxPlayers = max.GetInt32();
                }
                if (root.TryGetProperty("favicon", out var favicon))
                {
                    var faviconStr = favicon.GetString();
                    if (!string.IsNullOrEmpty(faviconStr))
                    {
                        try
                        {
                            var base64 = faviconStr.Contains(',')
                                ? faviconStr[(faviconStr.IndexOf(',') + 1)..]
                                : faviconStr;
                            var bytes = Convert.FromBase64String(base64);
                            var ms = new MemoryStream(bytes);
                            var bmp = new BitmapImage();
                            bmp.SetSource(ms.AsRandomAccessStream());
                            ServerIconImage = bmp;
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }
        private BitmapImage? _serverIconImage;
        public BitmapImage? ServerIconImage
        {
            get => _serverIconImage;
            set { _serverIconImage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasServerIcon)); }
        }
        public Microsoft.UI.Xaml.Visibility HasServerIcon =>
            _serverIconImage != null ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}