using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

public class SteamLobbyRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text txtPlayerName;
    [SerializeField] private Button btnJoin;

    private Action _onJoin;

    public void Bind(string ownerName, int members, string hostIp, string hostPort, Action onJoin)
    {
        _onJoin = onJoin;

        if (txtPlayerName != null)
            txtPlayerName.text = $"{ownerName}  ({members}/4)  {hostIp}:{hostPort}";

        if (btnJoin != null)
        {
            btnJoin.onClick.RemoveAllListeners();
            btnJoin.onClick.AddListener(() => _onJoin?.Invoke());
        }
    }
}
