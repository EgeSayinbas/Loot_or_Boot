using UnityEngine;

/// <summary>
/// Kartý her client'ta ilgili slot transformuna yapýþtýrýr.
/// NetworkTransform KULLANMA: kart prefabýndan NetworkTransform'u kaldýr (veya pos/rot sync kapat).
/// Böylece host/client farký olmadan herkes kendi sahnesinde doðru yere yerleþtirir.
/// </summary>
public class CardFollowSlots : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CardView view;

    [Header("Scene Names")]
    [SerializeField] private string centerCardsRootName = "CenterCardsRoot";
    [SerializeField] private string discardPileRootName = "DiscardPileRoot";
    [SerializeField] private string deckRootName = "DeckRoot";

    [Header("Center Slot Naming")]
    [SerializeField] private string centerSlotPrefix = "CenterSlot"; // CenterSlot0..7

    [Header("Discard Stack")]
    [SerializeField] private float discardStackUpOffset = 0.005f; // KempsGameManager ile ayný

    [Header("Smoothing (optional)")]
    [SerializeField] private bool smooth = false;
    [SerializeField] private float followSpeed = 20f;

    private Transform _centerRoot;
    private Transform _discardRoot;
    private Transform _deckRoot;

    private void Awake()
    {
        if (view == null) view = GetComponent<CardView>();
    }

    private void LateUpdate()
    {
        if (view == null || !view.IsSpawned) return;

        var target = ResolveTargetSlot();
        if (target == null) return;

        Vector3 targetPos = target.position;
        Quaternion targetRot = target.rotation;

        // Discard stack offset (SlotIndex = stack index)
        if (view.Zone.Value == CardZone.Discard)
        {
            int stackIndex = Mathf.Max(0, view.SlotIndex.Value);
            targetPos += target.up * (discardStackUpOffset * stackIndex);
        }

        if (!smooth)
        {
            transform.SetPositionAndRotation(targetPos, targetRot);
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, targetPos, followSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, followSpeed * Time.deltaTime);
        }
    }

    private Transform ResolveTargetSlot()
    {
        switch (view.Zone.Value)
        {
            case CardZone.Hand:
                return ResolveHandSlot(view.SeatIndex.Value, view.SlotIndex.Value);

            case CardZone.Center:
                return ResolveCenterSlot(view.SlotIndex.Value);

            case CardZone.Discard:
                return ResolveDiscardRoot();

            case CardZone.Deck:
                return ResolveDeckRoot();

            default:
                return null;
        }
    }

    private Transform ResolveHandSlot(int seat, int slot)
    {
        if (seat < 0 || seat > 3) return null;
        if (slot < 0 || slot > 3) return null;

        var players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            var p = players[i];
            if (p == null) continue;
            if (p.SeatIndex.Value != seat) continue;

            return p.GetHandSlot(slot);
        }

        return null;
    }

    private Transform ResolveCenterSlot(int centerIndex)
    {
        if (centerIndex < 0) return null;

        if (_centerRoot == null)
        {
            var go = GameObject.Find(centerCardsRootName);
            if (go != null) _centerRoot = go.transform;
        }
        if (_centerRoot == null) return null;

        return _centerRoot.Find($"{centerSlotPrefix}{centerIndex}");
    }

    private Transform ResolveDiscardRoot()
    {
        if (_discardRoot == null)
        {
            var go = GameObject.Find(discardPileRootName);
            if (go != null) _discardRoot = go.transform;
        }
        return _discardRoot;
    }

    private Transform ResolveDeckRoot()
    {
        if (_deckRoot == null)
        {
            var go = GameObject.Find(deckRootName);
            if (go != null) _deckRoot = go.transform;
        }
        return _deckRoot;
    }
}
