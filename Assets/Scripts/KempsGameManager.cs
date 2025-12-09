using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class KempsGameManager : NetworkBehaviour
{
    public static KempsGameManager Instance { get; private set; }

    [Header("Card Layout Roots")]
    [SerializeField] private Transform centerCardsRoot;   // Altında CenterSlot0..7
    [SerializeField] private Transform discardPileRoot;   // Şimdilik kullanılmıyor
    [SerializeField] private Transform cardSlotRoot;      // Altında Seat0_HandSlots vb.

    [Header("Card Prefab (3D)")]
    [SerializeField] private GameObject cardPrefab;       // NetworkObject + NetworkTransform + CardView + Card3D

    [Header("Card Data (şimdilik boş kalabilir)")]
    [SerializeField] private List<ArtCardData> allCards = new List<ArtCardData>();

    // Slot referansları
    private Transform[] centerSlots = new Transform[8];      // CenterSlot0..7
    private Transform[,] handSlots = new Transform[4, 4];    // [seatIndex, slotIndex]

    // Kart referansları
    private CardView[] centerCards = new CardView[8];        // 0–7: tüm center slotlar
    private CardView[,] handCards = new CardView[4, 4];      // [seat, handSlot]

    // Her oyuncu için bekleyen "elden atılan slot" (-1 = yok)
    private int[] pendingSwapHandIndex = new int[4];

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroycstm()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            for (int i = 0; i < 4; i++)
                pendingSwapHandIndex[i] = -1;

            CacheSlotTransforms();
            Debug.Log("[KempsGameManager] OnNetworkSpawn (Server) – slotlar cache'lendi.");

            SpawnTestCards();
        }
        else
        {
            CacheSlotTransforms(); // client tarafında da referanslar dolu olsun
        }
    }

    /// <summary>
    /// CenterCardsRoot ve CardSlotRoot altındaki slot Transform'larını cache'ler.
    /// </summary>
    private void CacheSlotTransforms()
    {
        // ---- ORTA 8 SLOT ----
        if (centerCardsRoot != null)
        {
            for (int i = 0; i < 8; i++)
            {
                Transform child = centerCardsRoot.Find($"CenterSlot{i}");
                if (child != null)
                {
                    centerSlots[i] = child;
                }
                else
                {
                    Debug.LogWarning($"[KempsGameManager] CenterSlot{i} bulunamadı.");
                }
            }
        }
        else
        {
            Debug.LogWarning("[KempsGameManager] centerCardsRoot atanmadı.");
        }

        // ---- HER SEAT İÇİN EL SLOTLARI ----
        if (cardSlotRoot != null)
        {
            for (int seat = 0; seat < 4; seat++)
            {
                Transform seatRoot = cardSlotRoot.Find($"Seat{seat}_HandSlots");
                if (seatRoot == null)
                {
                    Debug.LogWarning($"[KempsGameManager] Seat{seat}_HandSlots bulunamadı.");
                    continue;
                }

                for (int slot = 0; slot < 4; slot++)
                {
                    Transform slotTf = seatRoot.Find($"Seat{seat}_Slot{slot}");
                    if (slotTf != null)
                    {
                        handSlots[seat, slot] = slotTf;
                    }
                    else
                    {
                        Debug.LogWarning($"[KempsGameManager] Seat{seat}_Slot{slot} bulunamadı.");
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning("[KempsGameManager] cardSlotRoot atanmadı.");
        }
    }

    /// <summary>
    /// Sadece görsel test için, ortada ve ellerde dummy kartlar spawn eder.
    /// </summary>
    private void SpawnTestCards()
    {
        if (cardPrefab == null)
        {
            Debug.LogWarning("[KempsGameManager] CardPrefab atanmadığı için test kartları spawn edilmedi.");
            return;
        }

        // Dizileri temizle
        for (int i = 0; i < centerCards.Length; i++)
            centerCards[i] = null;

        for (int s = 0; s < 4; s++)
            for (int h = 0; h < 4; h++)
                handCards[s, h] = null;

        // --- Orta 4 kart (CenterSlot0..3) ---
        for (int i = 0; i < 4; i++)
        {
            if (centerSlots[i] == null)
                continue;

            GameObject go = Instantiate(
                cardPrefab,
                centerSlots[i].position,
                centerSlots[i].rotation
            );

            var netObj = go.GetComponent<NetworkObject>();
            if (netObj != null)
                netObj.Spawn();

            var view = go.GetComponent<CardView>();
            if (view != null)
            {
                view.SetupAsCenter(i);
                centerCards[i] = view;
            }
        }

        // --- Her oyuncuya 4 kart ---
        for (int seat = 0; seat < 4; seat++)
        {
            for (int slot = 0; slot < 4; slot++)
            {
                if (handSlots[seat, slot] == null)
                    continue;

                GameObject go = Instantiate(
                    cardPrefab,
                    handSlots[seat, slot].position,
                    handSlots[seat, slot].rotation
                );

                var netObj = go.GetComponent<NetworkObject>();
                if (netObj != null)
                    netObj.Spawn();

                var view = go.GetComponent<CardView>();
                if (view != null)
                {
                    view.SetupAsHand(seat, slot);
                    handCards[seat, slot] = view;
                }
            }
        }
    }

    // =======================
    //  ROUND / INPUT İSKELETİ
    // =======================

    [ServerRpc(RequireOwnership = false)]
    public void RequestPassServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[KempsGameManager] PASS isteği alındı. Client={senderId}");
        // PASS mantığını daha sonra yazacağız.
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestKempsServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[KempsGameManager] KEMPS isteği alındı. Client={senderId}");
        // KEMPS kontrolü burada olacak.
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestUnkempsServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[KempsGameManager] UNKEMPS isteği alındı. Client={senderId}");
        // UNKEMPS kontrolü burada olacak.
    }

    // ==============
    //  EL → MERKEZ
    // ==============

    [ServerRpc(RequireOwnership = false)]
    public void RequestDropHandCardServerRpc(int seatIndex, int handIndex, ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[KempsGameManager] DropHand isteği: seat={seatIndex}, hand={handIndex}, client={senderId}");

        if (seatIndex < 0 || seatIndex >= 4) return;
        if (handIndex < 0 || handIndex >= 4) return;

        var card = handCards[seatIndex, handIndex];
        if (card == null) return;

        // Zaten bir kart atmışsa
        if (pendingSwapHandIndex[seatIndex] != -1)
            return;

        // Boş bir center slotu bul (0..7 içinde)
        int centerIndex = -1;
        for (int i = 0; i < 8; i++)
        {
            if (centerCards[i] == null)
            {
                centerIndex = i;
                break;
            }
        }

        if (centerIndex == -1)
        {
            Debug.Log("[KempsGameManager] Merkezi slotlarda yer yok, kart atılamadı.");
            return;
        }

        // Dizileri güncelle
        handCards[seatIndex, handIndex] = null;
        centerCards[centerIndex] = card;
        pendingSwapHandIndex[seatIndex] = handIndex;

        // Transform'u yeni slota taşı
        var targetSlot = centerSlots[centerIndex];
        card.MoveToSlot(targetSlot);
        card.SetupAsCenter(centerIndex);

        Debug.Log($"[KempsGameManager] Kart seat={seatIndex}, hand={handIndex} -> centerIndex={centerIndex}");
    }

    // ==============
    //  MERKEZ → EL
    // ==============

    [ServerRpc(RequireOwnership = false)]
    public void RequestTakeCenterCardServerRpc(int seatIndex, int centerIndex, ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[KempsGameManager] TakeCenter isteği: seat={seatIndex}, center={centerIndex}, client={senderId}");

        if (seatIndex < 0 || seatIndex >= 4) return;
        if (centerIndex < 0 || centerIndex >= 8) return;

        int handIndex = pendingSwapHandIndex[seatIndex];
        if (handIndex == -1)
        {
            // Önceden elden kart atmamış -> alamaz
            Debug.Log("[KempsGameManager] Bu oyuncu daha önce kart atmadı, alamaz.");
            return;
        }

        var card = centerCards[centerIndex];
        if (card == null)
            return;

        if (handCards[seatIndex, handIndex] != null)
            return;

        // Dizileri güncelle
        centerCards[centerIndex] = null;
        handCards[seatIndex, handIndex] = card;
        pendingSwapHandIndex[seatIndex] = -1;

        // Transform'u eldeki slota taşı
        var targetSlot = handSlots[seatIndex, handIndex];
        card.MoveToSlot(targetSlot);
        card.SetupAsHand(seatIndex, handIndex);

        Debug.Log($"[KempsGameManager] Kart center={centerIndex} -> seat={seatIndex}, hand={handIndex}");
    }
}
