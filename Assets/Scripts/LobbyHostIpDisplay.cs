using TMPro;
using UnityEngine;

public class LobbyHostIpDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text txtHostIp;

    private void Start()
    {
        if (txtHostIp == null) return;

        // Host da client da aynı yerden okuyor:
        // Host: KempsSession.LobbyId = host code
        // Client: KempsSession.LobbyId = join input code
        string code = KempsSession.LobbyId;

        if (string.IsNullOrWhiteSpace(code))
            code = "------";

        txtHostIp.text = $"Lobby ID : {code}";
    }
}
