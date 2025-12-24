using Steamworks;

public static class SteamSession
{
    public static bool IsSteam { get; private set; }

    public static string LocalPlayerName { get; private set; } = "";
    public static string HostName { get; private set; } = "";
    public static CSteamID LobbyId { get; private set; } = CSteamID.Nil;

    public static void SetSteamEnabled(bool enabled)
    {
        IsSteam = enabled;
    }

    public static void SetLocalNameFromSteam()
    {
        if (!IsSteam) return;

        // Steamworks.NET
        LocalPlayerName = SteamFriends.GetPersonaName();
    }

    public static void SetLobby(CSteamID lobbyId, string hostName)
    {
        LobbyId = lobbyId;
        HostName = hostName ?? "";
    }

    public static void ClearLobby()
    {
        LobbyId = CSteamID.Nil;
        HostName = "";
    }
}
