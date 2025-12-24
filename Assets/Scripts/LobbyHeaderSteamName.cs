using TMPro;
using UnityEngine;

public class LobbyHeaderSteamName : MonoBehaviour
{
    [SerializeField] private TMP_Text headerText;

    private void Reset()
    {
        if (headerText == null)
            headerText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (headerText == null) return;

        if (SteamSession.IsSteam && !string.IsNullOrWhiteSpace(SteamSession.HostName))
        {
            headerText.text = $"{SteamSession.HostName}'s Ship";
        }
        else
        {
            headerText.text = "Lobby";
        }
    }
}
