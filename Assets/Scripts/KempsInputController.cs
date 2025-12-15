using UnityEngine;

public class KempsInputController : MonoBehaviour
{
    public static KempsInputController Instance { get; private set; }

    private CardView selectedHandCard;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ===== In-Game Buttons =====
    public void OnClickPass()
    {
        Debug.Log("[UI] PASS clicked");
        if (KempsGameManager.Instance == null) return;
        KempsGameManager.Instance.RequestPassServerRpc();
    }

    public void OnClickKemps()
    {
        Debug.Log("[UI] KEMPS clicked");
        if (KempsGameManager.Instance == null) return;
        KempsGameManager.Instance.RequestKempsServerRpc();
    }

    public void OnClickUnkemps()
    {
        Debug.Log("[UI] UNKEMPS clicked");
        if (KempsGameManager.Instance == null) return;
        KempsGameManager.Instance.RequestUnkempsServerRpc();
    }

    public void OnDeckClicked()
    {
        Debug.Log("[UI] DECK clicked");
        if (KempsGameManager.Instance == null) return;
        KempsGameManager.Instance.RequestDeckClickServerRpc();
    }

    // ===== End Panels Buttons =====
    public void OnClickPlayAgain()
    {
        Debug.Log("[UI] PLAY AGAIN clicked");
        if (KempsGameManager.Instance == null) return;
        KempsGameManager.Instance.RequestPlayAgainServerRpc();
    }

    public void OnClickBackToLobby()
    {
        Debug.Log("[UI] BACK TO LOBBY clicked");
        if (KempsGameManager.Instance == null) return;
        KempsGameManager.Instance.RequestBackToLobbyServerRpc();
    }

    public void OnClickMainMenu()
    {
        Debug.Log("[UI] MAIN MENU clicked");
        if (KempsGameManager.Instance == null) return;
        KempsGameManager.Instance.RequestMainMenuServerRpc();
    }

    // ===== Card Click =====
    public void OnCardClicked(Card3D card3D)
    {
        if (KempsGameManager.Instance == null) return;
        if (card3D == null || card3D.View == null) return;

        var card = card3D.View;

        var local = NetworkPlayer.Local;
        if (local == null) return;

        int mySeat = local.SeatIndex.Value;

        // Discard gibi "slotIndex < 0" kartlara tıklayınca hiçbir şey yapma
        if (card.SlotIndex.Value < 0) return;

        // 1) Hand click -> drop to center extra
        if (card.Zone.Value == CardZone.Hand)
        {
            if (card.SeatIndex.Value != mySeat) return;

            if (selectedHandCard != null)
            {
                Debug.Log("[Input] Zaten bir kart attın, önce center’dan kart al.");
                return;
            }

            selectedHandCard = card;
            KempsGameManager.Instance.RequestDropHandCardServerRpc(mySeat, card.SlotIndex.Value);
            return;
        }

        // 2) Center click -> take (only if previously dropped)
        if (card.Zone.Value == CardZone.Center)
        {
            if (selectedHandCard == null)
            {
                Debug.Log("[Input] Önce elinden bir kart atmalısın.");
                return;
            }

            KempsGameManager.Instance.RequestTakeCenterCardServerRpc(mySeat, card.SlotIndex.Value);
            selectedHandCard = null;
        }
    }
}
