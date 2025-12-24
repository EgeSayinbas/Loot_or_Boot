using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using Steamworks;

public class SteamLobbyUI : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] private string lobbySceneName = "Lobby";

    [Header("UI")]
    [SerializeField] private TMP_Text statusText;                 // opsiyonel
    [SerializeField] private RectTransform listContent;           // ScrollView/Viewport/Content
    [SerializeField] private SteamLobbyRowUI rowPrefab;           // SteamLobbyRow prefab
    [SerializeField] private GameObject emptyHint;                // opsiyonel

    [Header("Filters")]
    [SerializeField] private string gameTagKey = "game_tag";
    [SerializeField] private string gameTagValue = "LootOrBoot";
    [SerializeField] private string hostIpKey = "host_ip";
    [SerializeField] private string hostPortKey = "host_port";
    [SerializeField] private int maxResults = 50;

    [Header("Ports (Netcode/UTP)")]
    [SerializeField] private ushort gamePort = 7777;

    private enum ListMode { Public, FriendsOnly }
    private ListMode _mode = ListMode.Public;

    // Public list
    private readonly List<CSteamID> _currentList = new();

    // Friends list (friendId -> lobbyId)
    private readonly Dictionary<CSteamID, CSteamID> _friendLobbyMap = new();
    private readonly HashSet<ulong> _friendLobbyUnique = new();

    private Callback<LobbyCreated_t> _cbLobbyCreated;
    private Callback<LobbyEnter_t> _cbLobbyEnter;
    private Callback<LobbyMatchList_t> _cbLobbyMatchList;
    private Callback<LobbyDataUpdate_t> _cbLobbyDataUpdate;

    private CSteamID _pendingJoinLobby;

    private void Awake()
    {
        _cbLobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        _cbLobbyEnter = Callback<LobbyEnter_t>.Create(OnLobbyEnter);
        _cbLobbyMatchList = Callback<LobbyMatchList_t>.Create(OnLobbyMatchList);
        _cbLobbyDataUpdate = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
    }

    public void AutoRefreshIfPossible()
    {
        if (!SteamBootstrapper.IsReady) return;
        OnClickRefreshLobbies();
    }

    // UI Button: "Create Lobby STEAM"
    public void OnClickCreateLobbySteam()
    {
        if (!SteamBootstrapper.IsReady)
        {
            SetStatus("Steam hazýr deðil. Steam açýk mý? (steam_appid.txt=480)");
            return;
        }

        SetStatus("Steam lobby oluþturuluyor...");
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 4);
    }

    // UI Button: "Refresh" (public list)
    public void OnClickRefreshLobbies()
    {
        _mode = ListMode.Public;
        RequestPublicLobbyList();
    }

    // UI Button: "Join Friend" (arkadaþ lobileri)
    public void OnClickJoinFriend()
    {
        _mode = ListMode.FriendsOnly;
        RequestFriendsLobbyList();
    }

    // -------------------------
    // PUBLIC LOBBY LIST
    // -------------------------
    private void RequestPublicLobbyList()
    {
        if (!SteamBootstrapper.IsReady)
        {
            SetStatus("Steam hazýr deðil.");
            return;
        }

        ClearRows();
        if (emptyHint != null) emptyHint.SetActive(false);

        SteamMatchmaking.AddRequestLobbyListResultCountFilter(maxResults);

        // Sadece bizim oyun lobileri (tag)
        SteamMatchmaking.AddRequestLobbyListStringFilter(
            gameTagKey, gameTagValue, ELobbyComparison.k_ELobbyComparisonEqual);

        SteamMatchmaking.AddRequestLobbyListDistanceFilter(
            ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);

        SetStatus("Public lobby listesi alýnýyor...");
        SteamMatchmaking.RequestLobbyList();
    }

    private void OnLobbyMatchList(LobbyMatchList_t cb)
    {
        if (_mode != ListMode.Public) return;

        int count = (int)cb.m_nLobbiesMatching;
        SetStatus($"Bulunan lobby: {count}");

        _currentList.Clear();

        if (count <= 0)
        {
            if (emptyHint != null) emptyHint.SetActive(true);
            return;
        }
        if (emptyHint != null) emptyHint.SetActive(false);

        EnsureListRefs();

        for (int i = 0; i < count; i++)
        {
            CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(i);
            _currentList.Add(lobbyId);

            CreateRowForLobby(lobbyId, preferFriendOwnerName: false);
        }
    }

    // -------------------------
    // FRIEND LOBBY LIST
    // (Steamworks.NET doðru yolu)
    // -------------------------
    private void RequestFriendsLobbyList()
    {
        if (!SteamBootstrapper.IsReady)
        {
            SetStatus("Steam hazýr deðil.");
            return;
        }

        ClearRows();
        if (emptyHint != null) emptyHint.SetActive(false);
        EnsureListRefs();

        _friendLobbyMap.Clear();
        _friendLobbyUnique.Clear();

        // Arkadaþlarým arasýnda “bu oyunu oynayan ve lobby’de olanlar”
        int friendCount = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);

        SetStatus("Arkadaþ lobileri taranýyor...");

        for (int i = 0; i < friendCount; i++)
        {
            CSteamID friendId = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);

            if (!SteamFriends.GetFriendGamePlayed(friendId, out FriendGameInfo_t gameInfo))
                continue;

            // Bu oyun mu? (appid 480 testte de çalýþýr)
            if (!IsSameGame(gameInfo))
                continue;

            // Lobby var mý?
            CSteamID lobbyId = gameInfo.m_steamIDLobby;
            if (lobbyId == CSteamID.Nil)
                continue;

            // Duplicate engelle
            if (!_friendLobbyUnique.Add(lobbyId.m_SteamID))
                continue;

            _friendLobbyMap[friendId] = lobbyId;

            // Lobby data (tag/ip/port) çekmek için
            SteamMatchmaking.RequestLobbyData(lobbyId);
        }

        // Eðer hiç aday yoksa hemen empty
        if (_friendLobbyMap.Count == 0)
        {
            SetStatus("Arkadaþ lobisi bulunamadý.");
            if (emptyHint != null) emptyHint.SetActive(true);
        }
    }

    private bool IsSameGame(FriendGameInfo_t info)
    {
        // Steamworks.NET’te m_gameID GameID struct.
        // AppId ile eþleþtirelim.
        uint myAppId = SteamUtils.GetAppID().m_AppId;

        // GameID -> AppID
        // (GameID.ToString vs farklý olabilir, en saðlamý: info.m_gameID.AppID())
        uint friendAppId = info.m_gameID.AppID().m_AppId;

        return friendAppId == myAppId;
    }

    private void OnLobbyDataUpdate(LobbyDataUpdate_t cb)
    {
        if (_mode != ListMode.FriendsOnly) return;

        CSteamID lobbyId = new CSteamID(cb.m_ulSteamIDLobby);

        // Bu lobby, bizim friend scan’de var mý?
        if (!_friendLobbyUnique.Contains(lobbyId.m_SteamID))
            return;

        // Sadece bizim oyun lobileri (tag filter)
        string tag = SteamMatchmaking.GetLobbyData(lobbyId, gameTagKey);
        if (tag != gameTagValue)
            return;

        // Satýr bas
        CreateRowForLobby(lobbyId, preferFriendOwnerName: true);

        // UI empty kapat
        if (emptyHint != null) emptyHint.SetActive(false);

        // Status
        SetStatus($"Friend lobbies: {listContent.childCount}");
    }

    // -------------------------
    // JOIN / HOST
    // -------------------------
    private void JoinLobby(CSteamID lobbyId)
    {
        if (!SteamBootstrapper.IsReady)
        {
            SetStatus("Steam hazýr deðil.");
            return;
        }

        SetStatus("Lobby'ye giriliyor...");
        _pendingJoinLobby = lobbyId;
        SteamMatchmaking.JoinLobby(lobbyId);
    }

    private void OnLobbyEnter(LobbyEnter_t cb)
    {
        var lobbyId = new CSteamID(cb.m_ulSteamIDLobby);

        if (lobbyId != _pendingJoinLobby)
            return;

        if (cb.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            SetStatus("Lobby'ye girilemedi.");
            return;
        }

        // Host connection info al
        string hostIp = SteamMatchmaking.GetLobbyData(lobbyId, hostIpKey);
        string hostPortStr = SteamMatchmaking.GetLobbyData(lobbyId, hostPortKey);

        if (string.IsNullOrWhiteSpace(hostIp) ||
            string.IsNullOrWhiteSpace(hostPortStr) ||
            !ushort.TryParse(hostPortStr, out ushort hostPort))
        {
            SetStatus("Lobby data eksik: host_ip/host_port yok.");
            return;
        }

        SetStatus($"Baðlanýyor: {hostIp}:{hostPort}");
        StartNgoClient(hostIp, hostPort);
    }

    private void OnLobbyCreated(LobbyCreated_t cb)
    {
        if (cb.m_eResult != EResult.k_EResultOK)
        {
            SetStatus("Lobby oluþturulamadý.");
            return;
        }

        CSteamID lobbyId = new CSteamID(cb.m_ulSteamIDLobby);

        // Lobby data set
        SteamMatchmaking.SetLobbyData(lobbyId, gameTagKey, gameTagValue);

        string myIp = LanIpUtil.GetLocalIPv4(); // mevcut yapýyý bozmadan
        SteamMatchmaking.SetLobbyData(lobbyId, hostIpKey, myIp);
        SteamMatchmaking.SetLobbyData(lobbyId, hostPortKey, gamePort.ToString());

        StartNgoHost();

        SetStatus($"Lobby oluþturuldu. Host: {myIp}:{gamePort}");
    }

    private void StartNgoHost()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[SteamLobbyUI] NetworkManager.Singleton yok.");
            return;
        }

        var utp = GetTransport();
        if (utp != null)
            utp.SetConnectionData("0.0.0.0", gamePort, "0.0.0.0");

        if (NetworkManager.Singleton.StartHost())
        {
            NetworkManager.Singleton.SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
        }
        else
        {
            SetStatus("Host baþlatýlamadý (NGO).");
        }
    }

    private void StartNgoClient(string ip, ushort port)
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[SteamLobbyUI] NetworkManager.Singleton yok.");
            return;
        }

        var utp = GetTransport();
        if (utp == null)
        {
            SetStatus("UnityTransport bulunamadý.");
            return;
        }

        utp.SetConnectionData(ip, port);

        if (!NetworkManager.Singleton.StartClient())
        {
            SetStatus("Client baþlatýlamadý (NGO).");
        }
    }

    private UnityTransport GetTransport()
    {
        if (NetworkManager.Singleton == null) return null;

        var utp = NetworkManager.Singleton.NetworkConfig.NetworkTransport as UnityTransport;
        if (utp != null) return utp;

        return NetworkManager.Singleton.GetComponent<UnityTransport>();
    }

    // -------------------------
    // UI HELPERS
    // -------------------------
    private void EnsureListRefs()
    {
        if (listContent == null || rowPrefab == null)
        {
            Debug.LogError("[SteamLobbyUI] listContent veya rowPrefab atanmadý!");
        }
    }

    private void CreateRowForLobby(CSteamID lobbyId, bool preferFriendOwnerName)
    {
        EnsureListRefs();
        if (listContent == null || rowPrefab == null) return;

        // Tag filtre: güvenlik (public list zaten filtreli)
        string tag = SteamMatchmaking.GetLobbyData(lobbyId, gameTagKey);
        if (tag != gameTagValue) return;

        string hostIp = SteamMatchmaking.GetLobbyData(lobbyId, hostIpKey);
        string hostPortStr = SteamMatchmaking.GetLobbyData(lobbyId, hostPortKey);

        var owner = SteamMatchmaking.GetLobbyOwner(lobbyId);
        string ownerName = SteamFriends.GetFriendPersonaName(owner);

        int members = SteamMatchmaking.GetNumLobbyMembers(lobbyId);

        var row = Instantiate(rowPrefab, listContent);
        row.Bind(ownerName, members, hostIp, hostPortStr, () => JoinLobby(lobbyId));
    }

    private void ClearRows()
    {
        if (listContent == null) return;
        for (int i = listContent.childCount - 1; i >= 0; i--)
            Destroy(listContent.GetChild(i).gameObject);
    }

    private void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
        Debug.Log("[SteamLobbyUI] " + msg);
    }
}
