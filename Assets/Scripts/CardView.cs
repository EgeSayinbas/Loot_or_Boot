using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CardView : NetworkBehaviour
{
    [Header("Renderers")]
    [SerializeField] private MeshRenderer frontRenderer;
    [SerializeField] private MeshRenderer backRenderer;

    [Header("Scale")]
    [SerializeField] private float centerScaleMultiplier = 1.5f;

    private Vector3 defaultScale;
    private bool defaultScaleCaptured;

    // Networked state
    public NetworkVariable<int> CardId = new NetworkVariable<int>(-1);
    public NetworkVariable<int> SeatIndex = new NetworkVariable<int>(-1);
    public NetworkVariable<int> SlotIndex = new NetworkVariable<int>(-1);
    public NetworkVariable<CardZone> Zone = new NetworkVariable<CardZone>(CardZone.Deck);
    public NetworkVariable<bool> FaceUp = new NetworkVariable<bool>(false);

    private static Dictionary<int, ArtCardData> _db;
    private MaterialPropertyBlock _mpb;

    private void Awake()
    {
        CaptureDefaultScaleIfNeeded();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        CaptureDefaultScaleIfNeeded();
        EnsureDbLoaded();

        _mpb ??= new MaterialPropertyBlock();

        CardId.OnValueChanged += (_, __) => RefreshVisual();
        SeatIndex.OnValueChanged += (_, __) => RefreshVisual();
        SlotIndex.OnValueChanged += (_, __) => RefreshVisual();
        FaceUp.OnValueChanged += (_, __) => RefreshVisual();

        // Scale sadece Zone’a baðlý (Center’da 1.5x, diðerlerinde normal)
        Zone.OnValueChanged += (_, __) =>
        {
            RefreshScale();
            RefreshVisual();
        };

        RefreshScale();
        RefreshVisual();
    }

    private void CaptureDefaultScaleIfNeeded()
    {
        if (defaultScaleCaptured) return;
        defaultScale = transform.localScale;
        defaultScaleCaptured = true;
    }

    private void RefreshScale()
    {
        if (!defaultScaleCaptured) CaptureDefaultScaleIfNeeded();

        if (Zone.Value == CardZone.Center)
            transform.localScale = defaultScale * centerScaleMultiplier;
        else
            transform.localScale = defaultScale;
    }

    private void EnsureDbLoaded()
    {
        if (_db != null) return;

        _db = new Dictionary<int, ArtCardData>();
        var all = Resources.LoadAll<ArtCardData>("Cards/Data");
        foreach (var d in all)
        {
            if (d == null) continue;
            _db[d.cardId] = d;
        }
    }

    public bool IsMyHandCard()
    {
        var local = NetworkPlayer.Local;
        if (local == null) return false;
        return Zone.Value == CardZone.Hand && SeatIndex.Value == local.SeatIndex.Value;
    }

    public void RefreshVisual()
    {
        if (frontRenderer == null || backRenderer == null) return;

        EnsureDbLoaded();

        bool shouldShowFront = false;

        // Center açýk mý?
        if (Zone.Value == CardZone.Center && FaceUp.Value)
            shouldShowFront = true;

        // Hand ise sadece sahibi görsün
        if (Zone.Value == CardZone.Hand)
            shouldShowFront = IsMyHandCard();

        // Deck/Discard: genelde back
        if (!_db.TryGetValue(CardId.Value, out var data) || data == null)
        {
            frontRenderer.gameObject.SetActive(false);
            backRenderer.gameObject.SetActive(true);
            return;
        }

        // Texture bas
        if (shouldShowFront)
        {
            ApplyTexture(frontRenderer, data.frontTexture);
            frontRenderer.gameObject.SetActive(true);
            backRenderer.gameObject.SetActive(false);
        }
        else
        {
            ApplyTexture(backRenderer, data.backTexture);
            backRenderer.gameObject.SetActive(true);
            frontRenderer.gameObject.SetActive(false);
        }
    }

    private void ApplyTexture(MeshRenderer r, Texture2D tex)
    {
        if (r == null || tex == null) return;

        r.GetPropertyBlock(_mpb);
        _mpb.SetTexture("_BaseMap", tex);
        _mpb.SetTexture("_MainTex", tex);
        r.SetPropertyBlock(_mpb);
    }

    // Server sadece bu fonksiyonu çaðýrýr (spawn sonrasý)
    public void Server_Setup(int cardId, CardZone zone, int seatIndex, int slotIndex, bool faceUp)
    {
        if (!IsServer) return;

        CardId.Value = cardId;
        Zone.Value = zone;
        SeatIndex.Value = seatIndex;
        SlotIndex.Value = slotIndex;
        FaceUp.Value = faceUp;

        // Zone set edildiði için scale otomatik güncellenir, yine de güvenli:
        RefreshScale();
        RefreshVisual();
    }
}
