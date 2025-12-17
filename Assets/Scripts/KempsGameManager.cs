using System;
using System.Collections;
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
    [SerializeField] private Transform discardPileRoot;   // DiscardPileRoot (konumu ayarlı)
    [SerializeField] private Transform deckRoot;

    [Header("Prefabs")]
    [SerializeField] private GameObject cardPrefab;

    [Header("Discard Visual")]
    [SerializeField] private float discardStackUpOffset = 0.005f;

    // Slots
    private readonly Transform[] _centerSlots = new Transform[8];

    // Spawned cards
    private readonly CardView[] _centerCards = new CardView[8];
    private readonly CardView[,] _handCards = new CardView[4, 4];

    private readonly int[] _pendingHandIndex = new int[4];

    // Deck/discard IDs (server)
    private readonly List<int> _drawPile = new List<int>();
    private readonly List<int> _discardIds = new List<int>();

    // Discard visual objects (network objects moved to discard root)
    private readonly List<CardView> _discardVisuals = new List<CardView>();

    // Pass state
    private readonly bool[] _passed = new bool[4];

    // ===== NETWORKED STATE =====
    public NetworkVariable<bool> GameStarted { get; private set; } = new NetworkVariable<bool>(false);
    public NetworkVariable<int> Team0Score { get; private set; } = new NetworkVariable<int>(0); // team0 = Red
    public NetworkVariable<int> Team1Score { get; private set; } = new NetworkVariable<int>(0); // team1 = Blue
    public NetworkVariable<int> RoundIndex { get; private set; } = new NetworkVariable<int>(1);

    // GAME OVER
    public NetworkVariable<int> TargetScore { get; private set; } = new NetworkVariable<int>(3);
    public NetworkVariable<bool> GameOver { get; private set; } = new NetworkVariable<bool>(false);

    private readonly int[] _unkempsWrong = new int[2];

    private Dictionary<int, ArtCardData> _db;

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer) return;

        for (int i = 0; i < 4; i++) _pendingHandIndex[i] = -1;

        CacheCenterSlotTransforms();
        LoadDeckFromResources_Server();

        TargetScore.Value = Mathf.Max(1, KempsSession.TargetScore);

        Debug.Log($"[KempsGameManager] Deck hazır. Count={_drawPile.Count} TargetScore={TargetScore.Value}");
    }

    private void CacheCenterSlotTransforms()
    {
        for (int i = 0; i < 8; i++)
            _centerSlots[i] = centerCardsRoot != null ? centerCardsRoot.Find($"CenterSlot{i}") : null;
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

        RebuildFullDeck_Server();
    }

    private void RebuildFullDeck_Server()
    {
        _drawPile.Clear();
        _discardIds.Clear();

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

    private int GetTeam(int seat) => seat % 2;
    private int GetTeammate(int seat) => (seat == 0) ? 2 : (seat == 2) ? 0 : (seat == 1) ? 3 : 1;

    private bool IsHostClientRequest(ulong senderClientId) => senderClientId == NetworkManager.ServerClientId;

    // =========================
    // NEW: Hand slot source = Player Prefab
    // =========================
    private Transform GetHandSlotFromPlayerPrefab_Server(int seat, int slot)
    {
        var players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p == null) continue;
            if (p.SeatIndex.Value != seat) continue;

            var t = p.GetHandSlot(slot);
            return t;
        }
        return null;
    }

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
        Debug.Log("[KempsGameManager] StartGameDeal: dağıtım başlıyor.");

        ClearDiscardPile_Server();
        DespawnAllHandAndCenter_Server();
        ResetRoundFlags_Server();

        RebuildFullDeck_Server();

        for (int seat = 0; seat < 4; seat++)
        {
            for (int slot = 0; slot < 4; slot++)
            {
                int id = DrawOne_Server();
                SpawnCardToHand_Server(id, seat, slot);
            }
        }

        for (int i = 0; i < 4; i++)
        {
            int id = DrawOne_Server();
            SpawnCardToCenter_Server(id, i, faceUp: true);
        }

        GameStarted.Value = true;
    }

    private void FlipCenter_Server()
    {
        for (int i = 0; i < 8; i++)
        {
            if (_centerCards[i] == null) continue;

            var cv = _centerCards[i];
            _centerCards[i] = null;

            MoveCardToDiscard_Server(cv);
        }

        for (int i = 0; i < 4; i++)
        {
            int id = DrawOne_Server();
            SpawnCardToCenter_Server(id, i, faceUp: true);
        }

        ResetPasses_Server();
        Debug.Log("[KempsGameManager] DeckFlip: discard old center + spawn new 4 center.");
    }

    private void DespawnAllHandAndCenter_Server()
    {
        for (int i = 0; i < 8; i++)
        {
            if (_centerCards[i] != null)
            {
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
        if (netObj != null && netObj.IsSpawned)
            netObj.Despawn(true);
        else
            Destroy(view.gameObject);
    }

    private void MoveCardToDiscard_Server(CardView cv)
    {
        if (cv == null) return;

        int id = cv.CardId.Value;
        _discardIds.Add(id);

        if (discardPileRoot != null)
        {
            var pos = discardPileRoot.position + (discardPileRoot.up * discardStackUpOffset * _discardVisuals.Count);
            cv.transform.SetPositionAndRotation(pos, discardPileRoot.rotation);
        }

        cv.FaceUp.Value = false;

        cv.SeatIndex.Value = -1;
        cv.SlotIndex.Value = -1;

        _discardVisuals.Add(cv);
    }

    private void ClearDiscardPile_Server()
    {
        for (int i = 0; i < _discardVisuals.Count; i++)
            DespawnCardObject_Server(_discardVisuals[i]);

        _discardVisuals.Clear();
        _discardIds.Clear();
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
            throw new Exception("Deck empty!");

        int id = _drawPile[0];
        _drawPile.RemoveAt(0);
        return id;
    }

    private void SpawnCardToHand_Server(int cardId, int seat, int slot)
    {
        var target = GetHandSlotFromPlayerPrefab_Server(seat, slot);
        if (target == null || cardPrefab == null)
        {
            Debug.LogError($"[KempsGameManager] Hand slot NULL (PlayerPrefab) seat={seat} slot={slot}");
            return;
        }

        var go = Instantiate(cardPrefab, target.position, target.rotation);
        var net = go.GetComponent<NetworkObject>();
        net.Spawn();

        var view = go.GetComponent<CardView>();
        view.Server_Setup(cardId, CardZone.Hand, seat, slot, faceUp: true);

        _handCards[seat, slot] = view;
    }

    private void SpawnCardToCenter_Server(int cardId, int centerIndex, bool faceUp)
    {
        var target = _centerSlots[centerIndex];
        if (target == null || cardPrefab == null) return;

        var go = Instantiate(cardPrefab, target.position, target.rotation);
        var net = go.GetComponent<NetworkObject>();
        net.Spawn();

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
    // PASS / KEMPS / UNKEMPS (aynı)
    // =========================
    [ServerRpc(RequireOwnership = false)]
    public void RequestPassServerRpc(ServerRpcParams rpcParams = default)
    {
        if (GameOver.Value) return;

        var sender = rpcParams.Receive.SenderClientId;
        int seat = GetSeatFromClientId(sender);
        if (seat < 0) return;

        if (!GameStarted.Value) return;

        _passed[seat] = true;
        Debug.Log($"[KempsGameManager] PASS: seat={seat}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestKempsServerRpc(ServerRpcParams rpcParams = default)
    {
        if (GameOver.Value) return;

        var sender = rpcParams.Receive.SenderClientId;
        int seat = GetSeatFromClientId(sender);
        if (seat < 0) return;

        if (!GameStarted.Value) return;

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

        var sender = rpcParams.Receive.SenderClientId;
        int seat = GetSeatFromClientId(sender);
        if (seat < 0) return;

        if (!GameStarted.Value) return;

        int team = GetTeam(seat);
        int enemyTeam = 1 - team;

        bool anyEnemyHasSet = false;
        for (int s = 0; s < 4; s++)
        {
            if (GetTeam(s) != enemyTeam) continue;
            if (HasCompleteSetInHand(s))
            {
                anyEnemyHasSet = true;
                break;
            }
        }

        if (anyEnemyHasSet)
        {
            AddScore(team, 1);
            EndRound_Server(team);
            return;
        }

        _unkempsWrong[team]++;
        if (_unkempsWrong[team] == 1) return;

        if (_unkempsWrong[team] >= 2)
        {
            AddScore(enemyTeam, 1);
            EndRound_Server(enemyTeam);
        }
    }

    private void AddScore(int team, int delta)
    {
        if (team == 0) Team0Score.Value += delta;
        else Team1Score.Value += delta;
    }

    private void EndRound_Server(int winnerTeam)
    {
        PlayRoundEndClientRpc(winnerTeam);

        GameStarted.Value = false;
        ResetRoundFlags_Server();

        ClearDiscardPile_Server();
        DespawnAllHandAndCenter_Server();

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

            ShowGameOverClientRpc(winnerTeam, t0, t1);

            Debug.Log($"[KempsGameManager] GAME OVER winnerTeam={winnerTeam} score={t0}-{t1}");
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
    // HAND -> CENTER (drop/take) (aynı, sadece hedef slot artık player prefab)
    // =========================
    [ServerRpc(RequireOwnership = false)]
    public void RequestDropHandCardServerRpc(int seatIndex, int handIndex, ServerRpcParams rpcParams = default)
    {
        if (GameOver.Value) return;
        if (!GameStarted.Value) return;

        if (seatIndex < 0 || seatIndex >= 4) return;
        if (handIndex < 0 || handIndex >= 4) return;
        if (_pendingHandIndex[seatIndex] != -1) return;

        var card = _handCards[seatIndex, handIndex];
        if (card == null) return;

        int centerIndex = -1;
        for (int i = 0; i < 8; i++)
        {
            if (_centerCards[i] == null)
            {
                centerIndex = i;
                break;
            }
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
        if (GameOver.Value) return;
        if (!GameStarted.Value) return;

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

        var slotT = GetHandSlotFromPlayerPrefab_Server(seatIndex, handIndex);
        if (slotT != null)
            MoveToSlot_NoParent(card.transform, slotT);

        _passed[seatIndex] = false;
    }

    // =========================
    // END PANEL BUTTONS (aynı)
    // =========================
    [ServerRpc(RequireOwnership = false)]
    public void RequestPlayAgainServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!IsHostClientRequest(sender)) return;

        Debug.Log("[KempsGameManager] PlayAgain requested (HOST). Reset match state.");

        GameOver.Value = false;
        GameStarted.Value = false;

        Team0Score.Value = 0;
        Team1Score.Value = 0;
        RoundIndex.Value = 1;

        ClearDiscardPile_Server();
        DespawnAllHandAndCenter_Server();
        ResetRoundFlags_Server();
        RebuildFullDeck_Server();

        ResetHudClientRpc();
    }

    [ClientRpc]
    private void ResetHudClientRpc()
    {
        if (KempsHUD.Instance != null)
            KempsHUD.Instance.ResetForPlayAgain();
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestBackToLobbyServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!IsHostClientRequest(sender)) return;

        Debug.Log("[KempsGameManager] BackToLobby requested (HOST).");

        GameOver.Value = false;
        GameStarted.Value = false;
        ClearDiscardPile_Server();
        DespawnAllHandAndCenter_Server();

        NetworkManager.Singleton.SceneManager.LoadScene("Lobby", LoadSceneMode.Single);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestMainMenuServerRpc(ServerRpcParams rpcParams = default)
    {
        Debug.Log("[KempsGameManager] MainMenu requested (Server). Returning everyone to MainMenu + shutdown.");

        ReturnToMainMenuClientRpc();
        StartCoroutine(Co_ServerShutdownAndLoadMainMenu());
    }

    [ClientRpc]
    private void ReturnToMainMenuClientRpc()
    {
        Debug.Log("[KempsGameManager] ReturnToMainMenuClientRpc received.");
        StartCoroutine(Co_ClientShutdownAndLoadMainMenu());
    }

    private IEnumerator Co_ClientShutdownAndLoadMainMenu()
    {
        yield return null;

        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            if (nm.IsListening)
                nm.Shutdown();

            Destroy(nm.gameObject);
        }

        SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
    }

    private IEnumerator Co_ServerShutdownAndLoadMainMenu()
    {
        yield return new WaitForSeconds(0.1f);

        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            if (nm.IsListening)
                nm.Shutdown();

            Destroy(nm.gameObject);
        }

        SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
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
