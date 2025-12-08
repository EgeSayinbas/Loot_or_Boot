using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class KempsGameManager : NetworkBehaviour
{
    public static KempsGameManager Instance { get; private set; }

    [Header("Card Layout Roots")]
    [SerializeField] private Transform centerCardsRoot;   // CenterCardsRoot
    [SerializeField] private Transform cardSlotRoot;      // CardSlotRoot (Seat0_HandSlots vb.)

    [Header("Card Prefab (3D)")]
    [SerializeField] private GameObject cardPrefab;       // Card3D (üstünde NetworkObject + CardView)

    // slot dizileri
    private Transform[] centerSlots = new Transform[4];       // CenterSlot0..3
    private Transform[,] handSlots = new Transform[4, 4];     // [seatIndex, slotIndex]

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    protected new void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer)
            return;

        CacheSlotTransforms();
        Debug.Log("[KempsGameManager] Server OnNetworkSpawn, slotlar cache’lendi.");

        // Þimdilik test: Game sahnesine girer girmez kartlarý spawn et
        SpawnTestCards();
    }

    /// <summary>
    /// CenterCardsRoot ve CardSlotRoot altýndaki slot Transform'larýný cache'ler.
    /// Bu fonksiyon sadece server tarafýnda çaðrýlýr.
    /// </summary>
    private void CacheSlotTransforms()
    {
        // ---- ORTA 4 SLOT ----
        if (centerCardsRoot != null)
        {
            for (int i = 0; i < 4; i++)
            {
                Transform child = centerCardsRoot.Find($"CenterSlot{i}");
                if (child != null)
                {
                    centerSlots[i] = child;
                }
                else
                {
                    Debug.LogWarning($"[KempsGameManager] CenterSlot{i} bulunamadý.");
                }
            }
        }

        // ---- HER SEAT ÝÇÝN EL SLOTLARI ----
        if (cardSlotRoot != null)
        {
            for (int seat = 0; seat < 4; seat++)
            {
                Transform seatRoot = cardSlotRoot.Find($"Seat{seat}_HandSlots");
                if (seatRoot == null)
                {
                    Debug.LogWarning($"[KempsGameManager] Seat{seat}_HandSlots bulunamadý.");
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
                        Debug.LogWarning($"[KempsGameManager] Seat{seat}_Slot{slot} bulunamadý.");
                    }
                }
            }
        }
    }

    // =======================
    //  TEST ÝÇÝN KART SPAWN
    // =======================

    private void SpawnTestCards()
    {
        Debug.Log("[KempsGameManager] SpawnTestCards çaðrýldý (sadece server).");

        if (!IsServer)
        {
            Debug.LogWarning("SpawnTestCards sadece server’da çalýþmalý.");
            return;
        }

        if (cardPrefab == null)
        {
            Debug.LogError("[KempsGameManager] CardPrefab atanmadý!");
            return;
        }

        // Ortadaki 4 kart
        for (int i = 0; i < 4; i++)
        {
            if (centerSlots[i] == null) continue;

            GameObject go = Instantiate(
                cardPrefab,
                centerSlots[i].position,
                centerSlots[i].rotation
            );

            var netObj = go.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn(); // <<< BÜTÜN CLIENT’LARA GÖNDEREN KISIM
            }

            /*// sadece debug için renk
            var view = go.GetComponent<CardView>();
            if (view != null)
            {
                view.InitDebugColor(Color.yellow);
            }*/

        }

        // Her oyuncuya 4 kart
        for (int seat = 0; seat < 4; seat++)
        {
            for (int slot = 0; slot < 4; slot++)
            {
                if (handSlots[seat, slot] == null) continue;

                GameObject go = Instantiate(
                    cardPrefab,
                    handSlots[seat, slot].position,
                    handSlots[seat, slot].rotation
                );

                var netObj = go.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn();
                }
                /*
                var view = go.GetComponent<CardView>();
                if (view != null)
                {
                    // her seat farklý renk olsun diye
                    Color c = Color.white;
                    if (seat == 0) c = Color.red;
                    if (seat == 1) c = Color.blue;
                    if (seat == 2) c = Color.green;
                    if (seat == 3) c = Color.magenta;
                    view.InitDebugColor(c);
                }
                */
            }
        }
    }

    // =======================
    //  INPUT'TAN GELECEK RPC'LER
    // =======================

    [ServerRpc(RequireOwnership = false)]
    public void RequestPassServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[KempsGameManager] PASS alýndý. Client: {senderId}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestKempsServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[KempsGameManager] KEMPS alýndý. Client: {senderId}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestUnkempsServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[KempsGameManager] UNKEMPS alýndý. Client: {senderId}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestReserveCenterCardServerRpc(int centerIndex, ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[KempsGameManager] Center card reserve isteði. Index={centerIndex}, Client={senderId}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestSwapHandCardServerRpc(int handIndex, ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[KempsGameManager] El kartý swap isteði. HandIndex={handIndex}, Client={senderId}");
    }
}
