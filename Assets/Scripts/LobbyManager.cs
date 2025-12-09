using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using UnityEngine.SceneManagement;   // LoadSceneMode buradan geliyor
using TMPro;

public class LobbyManager : NetworkBehaviour
{
    public static LobbyManager Instance { get; private set; }

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

    private readonly List<NetworkPlayer> players = new List<NetworkPlayer>();

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Ready button color presetlerini otomatik al
        if (readyButton != null)
        {
            readyOffColors = readyButton.colors;

            // READY basılı hali = disabled rengi her yerde
            readyOnColors = readyOffColors;
            readyOnColors.normalColor = readyOffColors.disabledColor;
            readyOnColors.highlightedColor = readyOffColors.disabledColor;
            readyOnColors.pressedColor = readyOffColors.disabledColor;
        }


        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        RefreshUI();
    }

    private void OnDestroycustom()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        if (Instance == this)
            Instance = null;
    }

    private void OnClientConnected(ulong clientId)
    {
        RefreshUIClientRpc();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        RefreshUIClientRpc();
    }

    // ==== UI EVENTLERİ ====

    public void OnClickTeamRed()
    {
        NetworkPlayer.Local?.RequestChangeTeamServerRpc(Team.Red);
    }

    public void OnClickTeamBlue()
    {
        NetworkPlayer.Local?.RequestChangeTeamServerRpc(Team.Blue);
    }

    public void OnClickTeamNone()
    {
        NetworkPlayer.Local?.RequestChangeTeamServerRpc(Team.None);
    }

    public void OnClickReady()
    {
        if (NetworkPlayer.Local == null) return;

        // TAKIMSIZKEN READY VERME!
        if (NetworkPlayer.Local.PlayerTeam.Value == Team.None)
        {
            Debug.Log("Takım seçmeden ready veremezsin.");
            return;
        }

        bool newReady = !NetworkPlayer.Local.IsReady.Value;
        NetworkPlayer.Local.RequestSetReadyServerRpc(newReady);
    }

    public void OnClickStart()
    {
        if (!IsServer) return;

        var local = NetworkPlayer.Local;
        if (local == null || !local.IsHostPlayer.Value)
            return;

        if (!AreAllNonHostPlayersReady())
            return;

        AssignSeatIndices();
        NetworkManager.Singleton.SceneManager.LoadScene("Game", LoadSceneMode.Single);
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

        // Yeni API
        var foundPlayers = Object.FindObjectsByType<NetworkPlayer>(
            FindObjectsSortMode.None
        );

        players.AddRange(foundPlayers);
    }

    [ClientRpc]
    public void RefreshUIClientRpc()
    {
        RefreshUI();
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
        foreach (var slot in redColumn.GetComponentsInChildren<LobbySlotUI>())
            slot.Clear();
        foreach (var slot in greyColumn.GetComponentsInChildren<LobbySlotUI>())
            slot.Clear();
        foreach (var slot in blueColumn.GetComponentsInChildren<LobbySlotUI>())
            slot.Clear();
    }

    private void AddPlayerToSlots(NetworkPlayer p)
    {
        Transform column = greyColumn;

        if (p.PlayerTeam.Value == Team.Red)
            column = redColumn;
        else if (p.PlayerTeam.Value == Team.Blue)
            column = blueColumn;

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
        if (NetworkPlayer.Local == null)
            return;

        bool isHost = NetworkPlayer.Local.IsHostPlayer.Value;

        // HOST
        startButton.gameObject.SetActive(isHost);
        readyButton.gameObject.SetActive(!isHost);

        if (isHost)
        {
            startButton.interactable = AreAllNonHostPlayersReady();
            return;
        }

        // CLIENT
        bool isReady = NetworkPlayer.Local.IsReady.Value;

        readyButton.colors = isReady ? readyOnColors : readyOffColors;

        var text = readyButton.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
            text.text = "READY";
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

        if (redPlayers.Count >= 2)
        {
            redPlayers[0].SeatIndex.Value = 0;
            redPlayers[1].SeatIndex.Value = 2;
        }

        if (bluePlayers.Count >= 2)
        {
            bluePlayers[0].SeatIndex.Value = 1;
            bluePlayers[1].SeatIndex.Value = 3;
        }
    }
}
