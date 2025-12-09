using UnityEngine;

/// <summary>
/// PASS / KEMPS / UNKEMPS UI butonlarýný dinler
/// ve istekleri KempsGameManager'a iletir.
/// Ayrýca Card3D týklamalarýný da yönetir.
/// </summary>
public class KempsInputController : MonoBehaviour
{
    public static KempsInputController Instance { get; private set; }

    private CardView selectedHandCard;   // Oyuncunun elinden attýðý kart (swap için)

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ===== UI Butonlarý =====

    public void OnClickPass()
    {
        Debug.Log("[Input] PASS týklandý");

        if (KempsGameManager.Instance != null)
        {
            KempsGameManager.Instance.RequestPassServerRpc();
        }
    }

    public void OnClickKemps()
    {
        Debug.Log("[Input] KEMPS týklandý");

        if (KempsGameManager.Instance != null)
        {
            KempsGameManager.Instance.RequestKempsServerRpc();
        }
    }

    public void OnClickUnkemps()
    {
        Debug.Log("[Input] UNKEMPS týklandý");

        if (KempsGameManager.Instance != null)
        {
            KempsGameManager.Instance.RequestUnkempsServerRpc();
        }
    }

    // ===== Kart Týklamalarý =====

    public void OnCardClicked(Card3D card3D)
    {
        if (card3D == null || card3D.View == null)
            return;

        CardView card = card3D.View;

        NetworkPlayer localPlayer = NetworkPlayer.Local;
        if (localPlayer == null)
            return;

        int mySeat = localPlayer.SeatIndex.Value;

        // 1) EL KARTINA TIKLAMA
        if (card.Zone == CardZone.Hand)
        {
            // Sadece kendi eline izin
            if (card.SeatIndex != mySeat)
                return;

            if (selectedHandCard != null)
            {
                Debug.Log("[Input] Zaten bir kart attýn, önce yerden kart al.");
                return;
            }

            Debug.Log($"[Input] El kartý seçildi: Seat={card.SeatIndex}, Slot={card.SlotIndex}");
            selectedHandCard = card;

            // Server'a bildir: elden masaya at
            if (KempsGameManager.Instance != null)
            {
                KempsGameManager.Instance.RequestDropHandCardServerRpc(
                    mySeat,
                    card.SlotIndex
                );
            }

            return;
        }

        // 2) YERDEKÝ KARTA TIKLAMA
        if (card.Zone == CardZone.Center)
        {
            if (selectedHandCard == null)
            {
                Debug.Log("[Input] Önce elinden bir kart atmalýsýn.");
                return;
            }

            Debug.Log($"[Input] Yerden kart alýndý: CenterSlot={card.SlotIndex}");

            if (KempsGameManager.Instance != null)
            {
                KempsGameManager.Instance.RequestTakeCenterCardServerRpc(
                    mySeat,
                    card.SlotIndex
                );
            }

            selectedHandCard = null;
        }
    }
}
