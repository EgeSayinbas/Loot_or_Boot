using TMPro;
using UnityEngine;

public class LobbySlotUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI playerNameText;

    private NetworkPlayer boundPlayer;

    private void Reset()
    {
        // Prefab'te otomatik doldurmak için
        if (playerNameText == null)
            playerNameText = GetComponentInChildren<TextMeshProUGUI>();
    }

    public void SetPlayer(NetworkPlayer player)
    {
        boundPlayer = player;

        if (playerNameText != null)
        {
            playerNameText.text = $"Player {player.OwnerClientId}";
        }

        gameObject.SetActive(true);
    }

    public void Clear()
    {
        boundPlayer = null;

        if (playerNameText != null)
            playerNameText.text = "Empty";

        gameObject.SetActive(true); // boþ slot yine görünsün
    }

    public bool IsEmpty()
    {
        return boundPlayer == null;
    }
}
