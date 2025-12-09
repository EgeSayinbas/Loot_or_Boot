using UnityEngine;
using Unity.Netcode;

public enum CardZone : byte
{
    None = 0,
    Hand = 1,
    Center = 2
}


[RequireComponent(typeof(NetworkObject))]
public class CardView : NetworkBehaviour
{
    [Header("Debug / Visual")]
    [SerializeField] private Renderer cardRenderer;

    private NetworkVariable<CardZone> zone = new NetworkVariable<CardZone>(
        CardZone.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<int> seatIndex = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<int> slotIndex = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public CardZone Zone => zone.Value;
    public int SeatIndex => seatIndex.Value;
    public int SlotIndex => slotIndex.Value;

    private void Awake()
    {
        if (cardRenderer == null)
            cardRenderer = GetComponentInChildren<Renderer>();
    }

    // === Server tarafý setup fonksiyonlarý ===

    public void SetupAsCenter(int centerIndex)
    {
        if (!IsServer) return;

        zone.Value = CardZone.Center;
        seatIndex.Value = -1;
        slotIndex.Value = centerIndex;
    }

    public void SetupAsHand(int seat, int handSlot)
    {
        if (!IsServer) return;

        zone.Value = CardZone.Hand;
        seatIndex.Value = seat;
        slotIndex.Value = handSlot;
    }

    // Transform'u belli bir slota taþý
    public void MoveToSlot(Transform slotTf)
    {
        if (slotTf == null) return;

        transform.SetPositionAndRotation(slotTf.position, slotTf.rotation);
        transform.SetParent(slotTf, true);
    }

    // Debug renk
    public void InitDebugColor(Color c)
    {
        if (cardRenderer != null)
            cardRenderer.material.color = c;
    }
}
