using TMPro;
using UnityEngine;

public class LobbyHostIpDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text txtHostIp;

    private void Start()
    {
        if (txtHostIp == null) return;

        // 🔥 Steam moddaysak bu script devre dışı
        if (SteamSession.IsSteam)
        {
            return;
        }

        // LAN sistemi aynen korunuyor
        string code = KempsSession.LobbyId;

        if (string.IsNullOrWhiteSpace(code))
            code = "------";

        txtHostIp.text = $"Lobby ID : {code}";
    }
}
