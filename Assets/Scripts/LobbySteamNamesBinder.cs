using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class LobbySteamNamesBinder : MonoBehaviour
{
    [Header("Slot UI references (0..MaxPlayers-1)")]
    [SerializeField] private List<LobbySlotUI> slots = new();

    private void Update()
    {
        // Basit: her frame günceller (istersen 0.5sn throttle yaparýz)
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return;

        var identities = FindObjectsByType<SteamIdentityNet>(FindObjectsSortMode.None);

        // Slotlarý temizle
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
                slots[i].SetName("-");
        }

        // Doldur (bulduðu sýraya göre)
        int index = 0;
        foreach (var id in identities)
        {
            if (index >= slots.Count) break;
            if (slots[index] == null) { index++; continue; }

            string name = id.DisplayName.Value.ToString();
            if (string.IsNullOrWhiteSpace(name)) name = "Player";

            slots[index].SetName(name);
            index++;
        }
    }
}
