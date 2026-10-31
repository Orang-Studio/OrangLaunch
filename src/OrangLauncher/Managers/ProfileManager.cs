using System.Text.Json;
using OrangLauncher.Models;
namespace OrangLauncher.Managers
{
    public class ProfileManager
    {
        private static ProfileManager? _instance;
        public static ProfileManager Instance => _instance ??= new ProfileManager();
        private List<UserProfile> _profiles = new();
        private string? _selectedUuid;
        private readonly string _profilesPath;
        private readonly object _lock = new();
        public event Action? Changed;
        private ProfileManager()
        {
            _profilesPath = Path.Combine(PlatformPaths.GetDataDir(), "profiles.json");
            LoadProfiles();
        }
        private void LoadProfiles()
        {
            try
            {
                if (File.Exists(_profilesPath))
                {
                    var json = File.ReadAllText(_profilesPath);
                    var data = JsonSerializer.Deserialize<ProfilesData>(json);
                    if (data != null)
                    {
                        _profiles = data.Profiles ?? new List<UserProfile>();
                        _selectedUuid = data.SelectedUuid;
                    }
                }
            }
            catch { }
        }
        private void SaveProfiles()
        {
            lock (_lock)
            {
                try
                {
                    var data = new ProfilesData { Profiles = _profiles, SelectedUuid = _selectedUuid };
                    var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                    var tmp = _profilesPath + ".tmp";
                    File.WriteAllText(tmp, json);
                    File.Move(tmp, _profilesPath, true);
                }
                catch { }
            }
            Changed?.Invoke();
        }
        public List<UserProfile> GetProfiles() => _profiles.ToList();
        public UserProfile? GetSelectedProfile()
        {
            return _profiles.FirstOrDefault(p => p.Uuid == _selectedUuid) ?? _profiles.FirstOrDefault();
        }
        public void SetSelectedProfile(string uuid)
        {
            if (_selectedUuid == uuid) return;
            _selectedUuid = uuid;
            SaveProfiles();
        }
        public UserProfile AddProfile(string username, string type)
        {
            var existing = _profiles.FirstOrDefault(p => p.Type == type && string.Equals(p.Username, username, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                _selectedUuid = existing.Uuid;
                SaveProfiles();
                return existing;
            }
            var profile = new UserProfile
            {
                Username = username,
                Type = type,
                Uuid = type == "Offline" ? OfflineUuid(username) : username
            };
            _profiles.Add(profile);
            _selectedUuid = profile.Uuid;
            SaveProfiles();
            return profile;
        }
        public static string OfflineUuid(string username)
        {
            var hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes("OfflinePlayer:" + username));
            hash[6] = (byte)((hash[6] & 0x0f) | 0x30);
            hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
            var hex = Convert.ToHexString(hash).ToLowerInvariant();
            return $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..]}";
        }
        public void AddOrUpdateProfile(UserProfile profile, bool select = false)
        {
            var existing = _profiles.FirstOrDefault(p => p.Uuid == profile.Uuid);
            if (existing != null)
            {
                existing.Username = profile.Username;
                existing.AccessToken = profile.AccessToken;
                existing.RefreshToken = profile.RefreshToken;
                existing.MinecraftToken = profile.MinecraftToken;
                existing.MsClientId = profile.MsClientId;
                existing.LastUsed = profile.LastUsed;
            }
            else
            {
                _profiles.Add(profile);
            }
            if (select || _profiles.Count == 1) _selectedUuid = profile.Uuid;
            SaveProfiles();
        }
        public void RemoveProfile(string uuid)
        {
            _profiles.RemoveAll(p => p.Uuid == uuid);
            if (_selectedUuid == uuid) _selectedUuid = _profiles.FirstOrDefault()?.Uuid;
            SaveProfiles();
        }
        private class ProfilesData
        {
            public List<UserProfile>? Profiles { get; set; }
            public string? SelectedUuid { get; set; }
        }
    }
}
