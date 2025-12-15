using UnityEngine;

public class KempsInputController : MonoBehaviour
{
    public static KempsInputController Instance { get; private set; }

    private CardView selectedHandCard;

    private void Awake()
    {
        // KRİTİK: Artık duplicate varsa Destroy ETMİYORUZ.
        // Çünkü inspector’daki Button.OnClick referansı o objeyi hedefliyor olabilir.
        Instance = this;
        Debug.Log("[UI] KempsInputController Awake (alive)");
    }

    // ====== GAMEPLAY BUTTONS ======
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

    // ====== END GAME BUTTONS ======
    public void OnClickPlayAgain()
    {
        Debug.Log("[UI] PLAY AGAIN clicked");
        if (KempsGameManager.Instance == null) return;
        KempsGameManager.Instance.RequestPlayAgainServerRpc();
    }

    public void OnClickBackToLobby()
    {
        Debug.Log("[UI] LOBBY clicked");
        if (KempsGameManager.Instance == null) return;
        KempsGameManager.Instance.RequestGoLobbyServerRpc();
    }

    public void OnClickMainMenu()
    {
        Debug.Log("[UI] MAIN MENU clicked");
        if (KempsGameManager.Instance == null) return;
        KempsGameManager.Instance.RequestGoMainMenuServerRpc();
    }

    // ====== CARD CLICK (swap flow) ======
    public void OnCardClicked(Card3D card3D)
    {
        if (KempsGameManager.Instance == null) return;
        if (card3D == null || card3D.View == null) return;

        var card = card3D.View;

        var local = NetworkPlayer.Local;
        if (local == null) return;
        int mySeat = local.SeatIndex.Value;

        // Hand card
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

        // Center card
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
