using PlerionClient.Api;

public static class VPSManager
{
    internal static DefaultApi API { get; private set; }

    public static string Username { get; private set; }
    public static string Password { get; private set; }

    public static void Initialize(string username, string password)
    {
        API = new DefaultApi(configuration: new PlerionClient.Client.Configuration()
        {
            Username = username,
            Password = password,
            BasePath = @"http://localhost:3000/v1/"
        });
    }
}
