using UnityEngine;

/// <summary>
/// Kartlarý slot transformlarýný takip ettirir.
/// - Hand zone: ilgili NetworkPlayer'ýn HandR_CardSlot_{i} slotunu takip eder (animasyonla birlikte akar)
/// - Center/Discard: takip etmez (GameManager zaten tek sefer konumlar)
/// </summary>
public class CardFollowSlots : MonoBehaviour
{
    [SerializeField] private CardView view;

    private void Awake()
    {
        if (view == null) view = GetComponent<CardView>();
    }

    private void LateUpdate()
    {
        if (view == null) return;

        // Sadece eldeyken takip
        if (view.Zone.Value != CardZone.Hand) return;

        int seat = view.SeatIndex.Value;
        int slot = view.SlotIndex.Value;
        if (seat < 0 || slot < 0) return;

        var players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        NetworkPlayer targetPlayer = null;
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && players[i].SeatIndex.Value == seat)
            {
                targetPlayer = players[i];
                break;
            }
        }
        if (targetPlayer == null) return;

        var targetSlot = targetPlayer.GetHandSlot(slot);
        if (targetSlot == null) return;

        transform.SetPositionAndRotation(targetSlot.position, targetSlot.rotation);
    }
}
