public static class KempsSession
{
    // Lobby’de host ayarlayýp Game’e taþýnacak deðer
    public static int TargetScore = 3;

    public static string LobbyId;
    public static string HostLobbyCode = "";
    public static string LastEnteredLobbyCode = "";
    public static void ResetDefaults()
    {
        TargetScore = 3;
    }
}
