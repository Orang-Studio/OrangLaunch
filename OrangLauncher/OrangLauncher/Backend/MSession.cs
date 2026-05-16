namespace OrangLauncher.Backend
{
    public class MSession
    {
        public string Username { get; set; } = "";
        public string Uuid { get; set; } = "";
        public string AccessToken { get; set; } = "";
        public string ClientToken { get; set; } = "";
        public MSession() { }
        public MSession(string username, string accessToken, string uuid)
        {
            Username = username;
            AccessToken = accessToken;
            Uuid = uuid;
        }
        public static MSession CreateOffline(string username)
        {
            return new MSession
            {
                Username = username,
                Uuid = "00000000-0000-0000-0000-000000000000",
                AccessToken = "0",
                ClientToken = "0"
            };
        }
        public static MSession CreateOfflineSession(string username) => CreateOffline(username);
        public static MSession CreateMicrosoft(string username, string uuid, string accessToken)
        {
            return new MSession
            {
                Username = username,
                Uuid = uuid,
                AccessToken = accessToken,
                ClientToken = ""
            };
        }
    }
    public class MArgument
    {
        public string Value { get; }
        public MArgument(string value)
        {
            Value = value;
        }
        public override string ToString() => Value;
    }
}