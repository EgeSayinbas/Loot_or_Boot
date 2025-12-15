using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class KempsGameManager : NetworkBehaviour
{
    public static KempsGameManager Instance { get; private set; }

    [Header("Scene Roots")]
    [SerializeField] private Transform centerCardsRoot;
    [SerializeField] private Transform discardPileRoot;
    [SerializeField] private Transform deckRoot;
    [SerializeField] private Transform cardSlotRoot;

    [Header("Prefabs")]
    [SerializeField] private GameObject cardPrefab;

    private readonly Transform[] _centerSlots = new Transform[8];
    private readonly Transform[,] _handSlots = new Transform[4, 4];

    private readonly CardView[] _centerCards = new CardView[8];
    private readonly CardView[,] _handCards = new CardView[4, 4];

    private readonly int[] _pendingHandIndex = new int[4];

    private readonly List<int> _drawPile = new List<int>();
    private readonly List<int> _discardPile = new List<int>();

    private readonly bool[] _passed = new bool[4];

    // ===== NETWORKED STATE =====
    public NetworkVariable<bool> GameStarted { get; private set; } = new NetworkVariable<bool>(false);
    public NetworkVariable<int> Team0Score { get; private set; } = new NetworkVariable<int>(0); // team0 = Red (seat 0&2)
    public NetworkVariable<int> Team1Score { get; private set; } = new NetworkVariable<int>(0); // team1 = Blue (seat 1&3)
    public NetworkVariable<int> RoundIndex { get; private set; } = new NetworkVariable<int>(1);

    public NetworkVariable<int> TargetScore { get; private set; } = new NetworkVariable<int>(3);
    public NetworkVariable<bool> GameOver { get; private set; } = new NetworkVariable<bool>(false);

    private readonly int[] _unkempsWrong = new int[2];

    // Play Again votes (server only)
    private readonly bool[] _playAgainVote = new bool[4];

    private Dictionary<int, ArtCardData> _db;

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer) return;

        for (int i = 0; i < 4; i++)
        {
            _pendingHandIndex[i] = -1;
            _playAgainVote[i] = false;
        }

        CacheSlotTransforms();
        LoadDeckFromResources_Server();

        TargetScore.Value = Mathf.Max(1, KempsSession.TargetScore);

        Debug.Log($"[KempsGameManager] Deck hazır. Count={_drawPile.Count} TargetScore={TargetScore.Value}");
    }

    private void CacheSlotTransforms()
    {
        for (int i = 0; i < 8; i++)
            _centerSlots[i] = centerCardsRoot != null ? centerCardsRoot.Find($"CenterSlot{i}") : null;

        for (int seat = 0; seat < 4; seat++)
        {
            var seatRoot = cardSlotRoot != null ? cardSlotRoot.Find($"Seat{seat}_HandSlots") : null;
            if (seatRoot == null) continue;

            for (int slot = 0; slot < 4; slot++)
                _handSlots[seat, slot] = seatRoot.Find($"Seat{seat}_Slot{slot}");
        }
    }

    private void LoadDeckFromResources_Server()
    {
        _db = new Dictionary<int, ArtCardData>();

        var all = Resources.LoadAll<ArtCardData>("Cards/Data");
        foreach (var d in all)
        {
            if (d == null) continue;
            _db[d.cardId] = d;
        }

        _drawPile.Clear();
        _discardPile.Clear();

        foreach (var id in _db.Keys.OrderBy(x => x))
            _drawPile.Add(id);

        Shuffle(_drawPile);
    }

    private static void Shuffle(List<int> list)
    {
        var rng = new System.Random();
        for (int i = list.Count - 1; i > 0; i--)
        {
            int k = rng.Next(i + 1);
            (list[i], list[k]) = (list[k], list[i]);
        }
    }

    private int GetTeam(int seat) => seat % 2; // team0: seat 0&2 (Red), team1: seat 1&3 (Blue)
    private int GetTeammate(int seat) => (seat == 0) ? 2 : (seat == 2) ? 0 : (seat == 1) ? 3 : 1;
    private bool IsHostClientRequest(ulong senderClientId) => senderClientId == NetworkManager.ServerClientId;

    // =========================
    // DECK CLICK (HOST)
    // =========================
    [ServerRpc(RequireOwnership = false)]
    public void RequestDeckClickServerRpc(ServerRpcParams rpcParams = default)
    {
        if (GameOver.Value) return;

        ulong sender = rpcParams.Receive.SenderClientId;
        if (!IsHostClientRequest(sender)) return;

        if (!GameStarted.Value)
        {
            StartRoundDeal_Server();
            return;
        }

        if (!AllPlayersPassed())
        {
            Debug.Log("[KempsGameManager] DeckClick: Herkes PASS değil, flip yapılamaz.");
            return;
        }

        FlipCenter_Server();
    }

    private void StartRoundDeal_Server()
    {
        CleanupAllSpawnedCards_Server();
        ResetRoundFlags_Server();
        Shuffle(_drawPile);

        for (int seat = 0; seat < 4; seat++)
            for (int slot = 0; slot < 4; slot++)
                SpawnCardToHand_Server(DrawOne_Server(), seat, slot);

        for (int i = 0; i < 4; i++)
            SpawnCardToCenter_Server(DrawOne_Server(), i, faceUp: true);

        GameStarted.Value = true;
    }

    private void FlipCenter_Server()
    {
        for (int i = 0; i < 8; i++)
        {
            if (_centerCards[i] == null) continue;

            int cardId = _centerCards[i].CardId.Value;
            _discardPile.Add(cardId);

            DespawnCardObject_Server(_centerCards[i]);
            _centerCards[i] = null;
        }

        for (int i = 0; i < 4; i++)
            SpawnCardToCenter_Server(DrawOne_Server(), i, faceUp: true);

        ResetPasses_Server();
    }

    private void CleanupAllSpawnedCards_Server()
    {
        for (int i = 0; i < 8; i++)
        {
            if (_centerCards[i] != null)
            {
                _discardPile.Add(_centerCards[i].CardId.Value);
                DespawnCardObject_Server(_centerCards[i]);
                _centerCards[i] = null;
            }
        }

        for (int seat = 0; seat < 4; seat++)
        {
            for (int slot = 0; slot < 4; slot++)
            {
                if (_handCards[seat, slot] != null)
                {
                    _discardPile.Add(_handCards[seat, slot].CardId.Value);
                    DespawnCardObject_Server(_handCards[seat, slot]);
                    _handCards[seat, slot] = null;
                }
            }
            _pendingHandIndex[seat] = -1;
        }
    }

    private void DespawnCardObject_Server(CardView view)
    {
        if (view == null) return;

        var netObj = view.GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned) netObj.Despawn(true);
        else Destroy(view.gameObject);
    }

    private void ResetRoundFlags_Server()
    {
        ResetPasses_Server();
        _unkempsWrong[0] = 0;
        _unkempsWrong[1] = 0;
    }

    private void ResetPasses_Server()
    {
        for (int i = 0; i < 4; i++) _passed[i] = false;
    }

    private bool AllPlayersPassed()
    {
        for (int i = 0; i < 4; i++)
            if (!_passed[i]) return false;
        return true;
    }

    private int DrawOne_Server()
    {
        if (_drawPile.Count == 0)
        {
            if (_discardPile.Count > 0)
            {
                _drawPile.AddRange(_discardPile);
                _discardPile.Clear();
                Shuffle(_drawPile);
            }
        }

        if (_drawPile.Count == 0)
            throw new Exception("Deck empty!");

        int id = _drawPile[0];
        _drawPile.RemoveAt(0);
        return id;
    }

    private void SpawnCardToHand_Server(int cardId, int seat, int slot)
    {
        var target = _handSlots[seat, slot];
        if (target == null || cardPrefab == null) return;

        var go = Instantiate(cardPrefab, target.position, target.rotation);
        go.GetComponent<NetworkObject>().Spawn();

        var view = go.GetComponent<CardView>();
        view.Server_Setup(cardId, CardZone.Hand, seat, slot, faceUp: true);

        _handCards[seat, slot] = view;
    }

    private void SpawnCardToCenter_Server(int cardId, int centerIndex, bool faceUp)
    {
        var target = _centerSlots[centerIndex];
        if (target == null || cardPrefab == null) return;

        var go = Instantiate(cardPrefab, target.position, target.rotation);
        go.GetComponent<NetworkObject>().Spawn();

        var view = go.GetComponent<CardView>();
        view.Server_Setup(cardId, CardZone.Center, seatIndex: -1, slotIndex: centerIndex, faceUp: faceUp);

        _centerCards[centerIndex] = view;
    }

    private void MoveToSlot_NoParent(Transform obj, Transform slot)
    {
        if (obj == null || slot == null) return;
        obj.SetPositionAndRotation(slot.position, slot.rotation);
    }

    // =========================
    // PASS / KEMPS / UNKEMPS
    // =========================
    [ServerRpc(RequireOwnership = false)]
    public void RequestPassServerRpc(ServerRpcParams rpcParams = default)
    {
        if (GameOver.Value) return;

        int seat = GetSeatFromClientId(rpcParams.Receive.SenderClientId);
        if (seat < 0 || !GameStarted.Value) return;

        _passed[seat] = true;
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestKempsServerRpc(ServerRpcParams rpcParams = default)
    {
        if (GameOver.Value) return;

        int seat = GetSeatFromClientId(rpcParams.Receive.SenderClientId);
        if (seat < 0 || !GameStarted.Value) return;

        int team = GetTeam(seat);
        int mate = GetTeammate(seat);

        bool mateHasSet = HasCompleteSetInHand(mate);

        int winnerTeam;
        if (mateHasSet)
        {
            AddScore(team, 1);
            winnerTeam = team;
        }
        else
        {
            AddScore(1 - team, 1);
            winnerTeam = 1 - team;
        }

        EndRound_Server(winnerTeam);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestUnkempsServerRpc(ServerRpcParams rpcParams = default)
    {
        if (GameOver.Value) return;

        int seat = GetSeatFromClientId(rpcParams.Receive.SenderClientId);
        if (seat < 0 || !GameStarted.Value) return;

        int team = GetTeam(seat);
        int enemyTeam = 1 - team;

        bool anyEnemyHasSet = false;
        for (int s = 0; s < 4; s++)
        {
            if (GetTeam(s) != enemyTeam) continue;
            if (HasCompleteSetInHand(s)) { anyEnemyHasSet = true; break; }
        }

        if (anyEnemyHasSet)
        {
            AddScore(team, 1);
            EndRound_Server(team);
            return;
        }

        _unkempsWrong[team]++;
        if (_unkempsWrong[team] == 1) return;

        AddScore(enemyTeam, 1);
        EndRound_Server(enemyTeam);
    }

    private void AddScore(int team, int delta)
    {
        if (team == 0) Team0Score.Value += delta; // Red
        else Team1Score.Value += delta;           // Blue
    }

    private void EndRound_Server(int winnerTeam)
    {
        // round end ui
        PlayRoundEndClientRpc(winnerTeam);

        GameStarted.Value = false;
        ResetRoundFlags_Server();
        CleanupAllSpawnedCards_Server();
        RoundIndex.Value += 1;

        CheckGameOver_Server();
    }

    private void CheckGameOver_Server()
    {
        if (GameOver.Value) return;

        int t0 = Team0Score.Value;
        int t1 = Team1Score.Value;
        int target = Mathf.Max(1, TargetScore.Value);

        if (t0 >= target || t1 >= target)
        {
            int winnerTeam = (t0 >= target) ? 0 : 1;

            GameOver.Value = true;
            GameStarted.Value = false;

            // oyun bitti -> end panel
            ShowGameOverClientRpc(winnerTeam, t0, t1);

            // play-again oylarını sıfırla
            for (int i = 0; i < 4; i++) _playAgainVote[i] = false;
        }
    }

    [ClientRpc]
    private void ShowGameOverClientRpc(int winnerTeam, int team0Score, int team1Score)
    {
        if (KempsHUD.Instance != null)
            KempsHUD.Instance.ShowGameOver(winnerTeam, team0Score, team1Score);
    }

    [ClientRpc]
    private void PlayRoundEndClientRpc(int winnerTeam)
    {
        if (KempsHUD.Instance != null)
            KempsHUD.Instance.PlayRoundEndSequence(winnerTeam);
    }

    private bool HasCompleteSetInHand(int seat)
    {
        var groupsByItem = new Dictionary<string, HashSet<CardGroup>>();

        for (int slot = 0; slot < 4; slot++)
        {
            var cv = _handCards[seat, slot];
            if (cv == null) return false;

            int id = cv.CardId.Value;
            if (!_db.TryGetValue(id, out var data) || data == null) return false;

            if (!groupsByItem.TryGetValue(data.itemName, out var set))
            {
                set = new HashSet<CardGroup>();
                groupsByItem.Add(data.itemName, set);
            }
            set.Add(data.group);
        }

        return groupsByItem.Any(kv => kv.Value.Count == 4);
    }

    // =========================
    // HAND -> CENTER (drop/take)
    // =========================
    [ServerRpc(RequireOwnership = false)]
    public void RequestDropHandCardServerRpc(int seatIndex, int handIndex, ServerRpcParams rpcParams = default)
    {
        if (GameOver.Value || !GameStarted.Value) return;
        if (seatIndex < 0 || seatIndex >= 4) return;
        if (handIndex < 0 || handIndex >= 4) return;
        if (_pendingHandIndex[seatIndex] != -1) return;

        var card = _handCards[seatIndex, handIndex];
        if (card == null) return;

        int centerIndex = -1;
        for (int i = 4; i < 8; i++)
        {
            if (_centerCards[i] == null) { centerIndex = i; break; }
        }
        if (centerIndex == -1) return;

        _handCards[seatIndex, handIndex] = null;
        _centerCards[centerIndex] = card;
        _pendingHandIndex[seatIndex] = handIndex;

        card.Zone.Value = CardZone.Center;
        card.SeatIndex.Value = -1;
        card.SlotIndex.Value = centerIndex;
        card.FaceUp.Value = true;

        MoveToSlot_NoParent(card.transform, _centerSlots[centerIndex]);
        _passed[seatIndex] = false;
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestTakeCenterCardServerRpc(int seatIndex, int centerIndex, ServerRpcParams rpcParams = default)
    {
        if (GameOver.Value || !GameStarted.Value) return;
        if (seatIndex < 0 || seatIndex >= 4) return;
        if (centerIndex < 0 || centerIndex >= 8) return;

        int handIndex = _pendingHandIndex[seatIndex];
        if (handIndex == -1) return;

        var card = _centerCards[centerIndex];
        if (card == null) return;
        if (_handCards[seatIndex, handIndex] != null) return;

        _centerCards[centerIndex] = null;
        _handCards[seatIndex, handIndex] = card;
        _pendingHandIndex[seatIndex] = -1;

        card.Zone.Value = CardZone.Hand;
        card.SeatIndex.Value = seatIndex;
        card.SlotIndex.Value = handIndex;
        card.FaceUp.Value = true;

        MoveToSlot_NoParent(card.transform, _handSlots[seatIndex, handIndex]);
        _passed[seatIndex] = false;
    }

    // =========================
    // END MENU ACTIONS (NEW)
    // =========================

    // Her oyuncu basar, server oy verir. Herkes basınca Game sahnesini tekrar yükler (same teams).
    [ServerRpc(RequireOwnership = false)]
    public void RequestPlayAgainServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (!GameOver.Value) return;

        int seat = GetSeatFromClientId(rpcParams.Receive.SenderClientId);
        if (seat < 0) return;

        _playAgainVote[seat] = true;
        Debug.Log($"[END] PlayAgain vote seat={seat}");

        // herkes oy verdi mi?
        for (int i = 0; i < 4; i++)
        {
            // oyunda olmayan seat’ler varsa da (2 kişi vs) istersen bu kısmı “connected seat”e göre yaparız.
            if (_playAgainVote[i] == false)
                return;
        }

        // reset match state
        GameOver.Value = false;
        GameStarted.Value = false;
        Team0Score.Value = 0;
        Team1Score.Value = 0;
        RoundIndex.Value = 1;

        for (int i = 0; i < 4; i++) _playAgainVote[i] = false;

        // target skor aynı kalsın
        KempsSession.TargetScore = TargetScore.Value;

        Debug.Log("[END] Everyone voted -> Reload Game scene");
        NetworkManager.SceneManager.LoadScene("Game", LoadSceneMode.Single);
    }

    // Lobby’ye dön: takımları sıfırla (None) ve ready false, seat -1
    [ServerRpc(RequireOwnership = false)]
    public void RequestGoLobbyServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (!IsHostClientRequest(rpcParams.Receive.SenderClientId)) return;

        ResetPlayersForLobby_Server();

        GameOver.Value = false;
        GameStarted.Value = false;
        Team0Score.Value = 0;
        Team1Score.Value = 0;
        RoundIndex.Value = 1;

        Debug.Log("[END] Host -> Lobby scene");
        NetworkManager.SceneManager.LoadScene("Lobby", LoadSceneMode.Single);
    }

    // MainMenu: network’ü kapatıp herkesi main menuye at
    [ServerRpc(RequireOwnership = false)]
    public void RequestGoMainMenuServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (!IsHostClientRequest(rpcParams.Receive.SenderClientId)) return;

        Debug.Log("[END] Host -> MainMenu (shutdown)");
        GoMainMenuClientRpc();
        NetworkManager.Shutdown();
        SceneManager.LoadScene("MainMenu");
    }

    [ClientRpc]
    private void GoMainMenuClientRpc()
    {
        if (IsServer) return; // host zaten kendi tarafında yaptı
        NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene("MainMenu");
    }

    private void ResetPlayersForLobby_Server()
    {
        var players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p == null) continue;
            p.PlayerTeam.Value = Team.None;
            p.IsReady.Value = false;
            p.SeatIndex.Value = -1;
        }
    }

    // =========================
    // Seat mapping
    // =========================
    private int GetSeatFromClientId(ulong clientId)
    {
        var players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p == null) continue;
            if (p.OwnerClientId == clientId)
                return p.SeatIndex.Value;
        }
        return -1;
    }
}
