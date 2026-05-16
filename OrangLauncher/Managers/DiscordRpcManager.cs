using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
namespace OrangLauncher.Managers
{
    public class DiscordRpcManager : IDisposable
    {
        private NamedPipeClientStream? _pipe;
        private volatile bool _disposed;
        private volatile bool _isReady;
        private readonly object _lock = new();
        private const string ClientId = "1411624079701573703";
        private DateTime _sessionStart;
        private string? _currentState;
        private string? _currentDetails;
        public bool IsConnected => _isReady && _pipe is { IsConnected: true };
        public void Initialize()
        {
            try
            {
                Cleanup();
                _disposed = false;
                _sessionStart = DateTime.UtcNow;
                Task.Run(() => ConnectAndHandshake());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Discord RPC] Initialize exception: {ex.Message}");
            }
        }
        private void ConnectAndHandshake()
        {
            for (int i = 0; i < 10; i++)
            {
                if (_disposed) return;
                try
                {
                    var pipe = new NamedPipeClientStream(".", $"discord-ipc-{i}",
                        PipeDirection.InOut, PipeOptions.Asynchronous);
                    pipe.Connect(1000);
                    if (!pipe.IsConnected)
                    {
                        pipe.Dispose();
                        continue;
                    }
                    Debug.WriteLine($"[Discord RPC] Pipe discord-ipc-{i} connected");
                    var handshake = JsonSerializer.Serialize(new { v = 1, client_id = ClientId });
                    WriteFrame(pipe, 0, handshake);
                    var (opcode, payload) = ReadFrame(pipe);
                    if (opcode == 1)
                    {
                        Debug.WriteLine($"[Discord RPC] Handshake success: {Truncate(payload, 200)}");
                        lock (_lock)
                        {
                            _pipe = pipe;
                            _isReady = true;
                        }
                        SetPresenceInternal("Idling in launcher", "Using OrangLauncher");
                        return;
                    }
                    Debug.WriteLine($"[Discord RPC] Unexpected handshake response opcode: {opcode}");
                    pipe.Dispose();
                }
                catch (TimeoutException)
                {
                }
                catch (IOException ex)
                {
                    Debug.WriteLine($"[Discord RPC] Pipe {i} IO error: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Discord RPC] Pipe {i} error: {ex.Message}");
                }
            }
            Debug.WriteLine("[Discord RPC] Could not connect to any Discord IPC pipe");
        }
        public void UpdatePresence(string state, string details = "")
        {
            if (_disposed) return;
            SetPresenceInternal(state, string.IsNullOrEmpty(details) ? "Using OrangLauncher" : details);
        }
        public void ResetTimestamp()
        {
            _sessionStart = DateTime.UtcNow;
        }
        private void SetPresenceInternal(string state, string details)
        {
            _currentState = state;
            _currentDetails = details;
            if (!_isReady) return;
            try
            {
                lock (_lock)
                {
                    if (_pipe == null || !_pipe.IsConnected) return;
                    var epoch = new DateTimeOffset(_sessionStart).ToUnixTimeSeconds();
                    var payload = JsonSerializer.Serialize(new SetActivityCommand
                    {
                        Cmd = "SET_ACTIVITY",
                        Nonce = Guid.NewGuid().ToString(),
                        Args = new SetActivityArgs
                        {
                            Pid = Environment.ProcessId,
                            Activity = new ActivityPayload
                            {
                                State = state,
                                Details = details,
                                Timestamps = new TimestampPayload { Start = epoch },
                                Assets = new AssetsPayload
                                {
                                    LargeImage = "logo",
                                    LargeText = "OrangLauncher"
                                }
                            }
                        }
                    });
                    WriteFrame(_pipe, 1, payload);
                    Debug.WriteLine($"[Discord RPC] Presence set: {details} - {state}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Discord RPC] SetPresence error: {ex.Message}");
                _isReady = false;
            }
        }
        private static void WriteFrame(Stream pipe, int opcode, string payload)
        {
            var data = Encoding.UTF8.GetBytes(payload);
            var header = new byte[8];
            BitConverter.GetBytes(opcode).CopyTo(header, 0);
            BitConverter.GetBytes(data.Length).CopyTo(header, 4);
            pipe.Write(header, 0, 8);
            pipe.Write(data, 0, data.Length);
            pipe.Flush();
        }
        private static (int opcode, string payload) ReadFrame(Stream pipe)
        {
            var header = ReadExact(pipe, 8);
            int opcode = BitConverter.ToInt32(header, 0);
            int length = BitConverter.ToInt32(header, 4);
            if (length <= 0 || length > 65536)
                return (opcode, string.Empty);
            var data = ReadExact(pipe, length);
            return (opcode, Encoding.UTF8.GetString(data));
        }
        private static byte[] ReadExact(Stream stream, int count)
        {
            var buffer = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int read = stream.Read(buffer, offset, count - offset);
                if (read == 0) throw new IOException("Pipe closed during read");
                offset += read;
            }
            return buffer;
        }
        private static string Truncate(string s, int max) =>
            s.Length <= max ? s : s[..max] + "...";
        private void Cleanup()
        {
            lock (_lock)
            {
                _isReady = false;
                var pipe = _pipe;
                _pipe = null;
                if (pipe != null)
                {
                    try { pipe.Dispose(); } catch { }
                }
            }
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Cleanup();
            GC.SuppressFinalize(this);
        }
        private sealed class SetActivityCommand
        {
            [JsonPropertyName("cmd")] public string Cmd { get; set; } = "";
            [JsonPropertyName("nonce")] public string Nonce { get; set; } = "";
            [JsonPropertyName("args")] public SetActivityArgs Args { get; set; } = new();
        }
        private sealed class SetActivityArgs
        {
            [JsonPropertyName("pid")] public int Pid { get; set; }
            [JsonPropertyName("activity")] public ActivityPayload Activity { get; set; } = new();
        }
        private sealed class ActivityPayload
        {
            [JsonPropertyName("state")] public string State { get; set; } = "";
            [JsonPropertyName("details")] public string Details { get; set; } = "";
            [JsonPropertyName("timestamps")] public TimestampPayload Timestamps { get; set; } = new();
            [JsonPropertyName("assets")] public AssetsPayload Assets { get; set; } = new();
        }
        private sealed class TimestampPayload
        {
            [JsonPropertyName("start")] public long Start { get; set; }
        }
        private sealed class AssetsPayload
        {
            [JsonPropertyName("large_image")] public string LargeImage { get; set; } = "";
            [JsonPropertyName("large_text")] public string LargeText { get; set; } = "";
        }
    }
}