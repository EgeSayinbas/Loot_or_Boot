using TMPro;
using UnityEngine;

public class LobbySlotUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI playerNameText;

    private NetworkPlayer boundPlayer;

    private void Reset()
    {
        if (playerNameText == null)
            playerNameText = GetComponentInChildren<TextMeshProUGUI>();
    }

    public void SetPlayer(NetworkPlayer player)
    {
        boundPlayer = player;

        if (playerNameText != null)
        {
            // Steam name varsa onu yaz, yoksa fallback
            string steamName = player != null ? player.GetDisplayName() : "";
            if (!string.IsNullOrWhiteSpace(steamName))
                playerNameText.text = steamName;
            else
                playerNameText.text = $"Player {player.OwnerClientId}";
        }

        gameObject.SetActive(true);
    }

    // ✅ Eski kodların çağırıyor olabilir: slot.SetName("Ege")
    public void SetName(string name)
    {
        if (playerNameText != null)
            playerNameText.text = string.IsNullOrWhiteSpace(name) ? "Empty" : name;

        gameObject.SetActive(true);
    }

    public void Clear()
    {
        boundPlayer = null;

        if (playerNameText != null)
            playerNameText.text = "Empty";

        gameObject.SetActive(true);
    }

    public bool IsEmpty()
    {
        return boundPlayer == null;
    }
}
