using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyManager : NetworkBehaviour
{
    public static LobbyManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private TMP_Text txtTargetScore;

    [Header("Settings")]
    [SerializeField] private int minScore = 1;
    [SerializeField] private int maxScore = 10;

    [Header("Ready Button Color Presets")]
    [SerializeField] private ColorBlock readyOffColors;
    [SerializeField] private ColorBlock readyOnColors;

    [Header("Slot Parents")]
    public Transform redColumn;
    public Transform greyColumn;
    public Transform blueColumn;

    [Header("Buttons")]
    public Button startButton;
    public Button readyButton;

    [Header("Transition")]
    [SerializeField] private NetworkTransitionHook transitionHook; // ✅ EKLENDİ

    private readonly List<NetworkPlayer> players = new List<NetworkPlayer>();

    public NetworkVariable<int> TargetScore = new NetworkVariable<int>(
        3,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        TargetScore.OnValueChanged += (_, newValue) => UpdateTargetScoreUI(newValue);
        UpdateTargetScoreUI(TargetScore.Value);

        if (readyButton != null)
        {
            readyOffColors = readyButton.colors;

            readyOnColors = readyOffColors;
            readyOnColors.normalColor = readyOffColors.disabledColor;
            readyOnColors.highlightedColor = readyOffColors.disabledColor;
            readyOnColors.pressedColor = readyOffColors.disabledColor;
        }

        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        // ✅ TransitionHook otomatik bulma (inspector atamadıysan)
        if (transitionHook == null)
            transitionHook = FindFirstObjectByType<NetworkTransitionHook>();

        RefreshUI();
    }

    private void OnDestroy()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        if (Instance == this) Instance = null;
    }

    private void UpdateTargetScoreUI(int value)
    {
        if (txtTargetScore != null)
            txtTargetScore.text = value.ToString();
    }

    private void OnClientConnected(ulong clientId) => RefreshUIClientRpc();
    private void OnClientDisconnected(ulong clientId) => RefreshUIClientRpc();

    // ==== UI EVENTLERİ ====

    public void OnClickTeamRed() => NetworkPlayer.Local?.RequestChangeTeamServerRpc(Team.Red);
    public void OnClickTeamBlue() => NetworkPlayer.Local?.RequestChangeTeamServerRpc(Team.Blue);
    public void OnClickTeamNone() => NetworkPlayer.Local?.RequestChangeTeamServerRpc(Team.None);

    public void OnClickPlus()
    {
        if (!IsHost) return;
        ChangeTargetScoreServerRpc(+1);
    }

    public void OnClickMinus()
    {
        if (!IsHost) return;
        ChangeTargetScoreServerRpc(-1);
    }

    public void OnClickReady()
    {
        if (NetworkPlayer.Local == null) return;

        if (NetworkPlayer.Local.PlayerTeam.Value == Team.None)
        {
            Debug.Log("Takım seçmeden ready veremezsin.");
            return;
        }

        bool newReady = !NetworkPlayer.Local.IsReady.Value;
        NetworkPlayer.Local.RequestSetReadyServerRpc(newReady);
        RefreshUI(); // local UI hızlı güncellensin
    }

    public void OnClickStart()
    {
        if (!IsServer) return;

        var local = NetworkPlayer.Local;
        if (local == null || !local.IsHostPlayer.Value)
            return;

        if (!AreAllNonHostPlayersReady())
            return;

        // ✅ TargetScore’u taşımaya devam
        KempsSession.TargetScore = TargetScore.Value;

        // ✅ Seat assign için listeyi garanti yenile
        RebuildPlayerList();
        AssignSeatIndices();

        // ✅ TransitionHook üzerinden scene geçişi
        if (transitionHook == null)
        {
            if (TransitionUIController.Instance != null)
                TransitionUIController.Instance.Show();
            Debug.LogError("[LobbyManager] transitionHook NULL! Sahneye NetworkTransitionHook ekleyip inspector’dan bağla.");

            // fallback (istersen kalsın)
            NetworkManager.Singleton.SceneManager.LoadScene("Game", LoadSceneMode.Single);
            return;
        }

        transitionHook.HostStartGame_WithTransition("Game");
    }

    // ==== Yardımcılar ====

    private bool AreAllNonHostPlayersReady()
    {
        RebuildPlayerList();
        foreach (var p in players)
        {
            if (!p.IsHostPlayer.Value && !p.IsReady.Value)
                return false;
        }
        return true;
    }

    private void RebuildPlayerList()
    {
        players.Clear();
        var foundPlayers = Object.FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        players.AddRange(foundPlayers);
    }

    public void RefreshUI()
    {
        RebuildPlayerList();
        ClearAllSlots();

        foreach (var p in players)
            AddPlayerToSlots(p);

        UpdateButtons();
    }

    private void ClearAllSlots()
    {
        foreach (var slot in redColumn.GetComponentsInChildren<LobbySlotUI>()) slot.Clear();
        foreach (var slot in greyColumn.GetComponentsInChildren<LobbySlotUI>()) slot.Clear();
        foreach (var slot in blueColumn.GetComponentsInChildren<LobbySlotUI>()) slot.Clear();
    }

    private void AddPlayerToSlots(NetworkPlayer p)
    {
        Transform column = greyColumn;

        if (p.PlayerTeam.Value == Team.Red) column = redColumn;
        else if (p.PlayerTeam.Value == Team.Blue) column = blueColumn;

        foreach (var slot in column.GetComponentsInChildren<LobbySlotUI>())
        {
            if (slot.IsEmpty())
            {
                slot.SetPlayer(p);
                return;
            }
        }
    }

    private void UpdateButtons()
    {
        if (NetworkPlayer.Local == null) return;

        bool isHost = NetworkPlayer.Local.IsHostPlayer.Value;

        startButton.gameObject.SetActive(isHost);
        readyButton.gameObject.SetActive(!isHost);

        if (isHost)
        {
            startButton.interactable = AreAllNonHostPlayersReady();
            return;
        }

        bool isReady = NetworkPlayer.Local.IsReady.Value;
        readyButton.colors = isReady ? readyOnColors : readyOffColors;
    }

    private void AssignSeatIndices()
    {
        var redPlayers = new List<NetworkPlayer>();
        var bluePlayers = new List<NetworkPlayer>();

        foreach (var p in players)
        {
            if (p.PlayerTeam.Value == Team.Red) redPlayers.Add(p);
            else if (p.PlayerTeam.Value == Team.Blue) bluePlayers.Add(p);
        }

        // ✅ Red = seat 0 & 2
        if (redPlayers.Count >= 2)
        {
            redPlayers[0].SeatIndex.Value = 0;
            redPlayers[1].SeatIndex.Value = 2;
        }

        // ✅ Blue = seat 1 & 3
        if (bluePlayers.Count >= 2)
        {
            bluePlayers[0].SeatIndex.Value = 1;
            bluePlayers[1].SeatIndex.Value = 3;
        }
    }

    [ClientRpc]
    public void RefreshUIClientRpc() => RefreshUI();

    [ServerRpc(RequireOwnership = false)]
    private void ChangeTargetScoreServerRpc(int delta)
    {
        int next = Mathf.Clamp(TargetScore.Value + delta, minScore, maxScore);
        TargetScore.Value = next;
        KempsSession.TargetScore = next;
    }
}
