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
            try
            {
                var data = new ProfilesData { Profiles = _profiles, SelectedUuid = _selectedUuid };
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_profilesPath, json);
            }
            catch { }
        }
        public List<UserProfile> GetProfiles() => _profiles.ToList();
        public UserProfile? GetSelectedProfile()
        {
            return _profiles.FirstOrDefault(p => p.Uuid == _selectedUuid) ?? _profiles.FirstOrDefault();
        }
        public void SetSelectedProfile(string uuid)
        {
            _selectedUuid = uuid;
            SaveProfiles();
        }
        public void AddProfile(string username, string type)
        {
            var profile = new UserProfile
            {
                Username = username,
                Type = type,
                Uuid = type == "Offline" ? Guid.NewGuid().ToString() : username
            };
            _profiles.Add(profile);
            if (_profiles.Count == 1) _selectedUuid = profile.Uuid;
            SaveProfiles();
        }
        public void AddOrUpdateProfile(UserProfile profile)
        {
            var existing = _profiles.FirstOrDefault(p => p.Uuid == profile.Uuid);
            if (existing != null)
            {
                existing.Username = profile.Username;
                existing.AccessToken = profile.AccessToken;
                existing.RefreshToken = profile.RefreshToken;
                existing.MinecraftToken = profile.MinecraftToken;
                existing.LastUsed = profile.LastUsed;
            }
            else
            {
                _profiles.Add(profile);
            }
            if (_profiles.Count == 1) _selectedUuid = profile.Uuid;
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