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
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private RectTransform listContent;
    [SerializeField] private SteamLobbyRowUI rowPrefab;
    [SerializeField] private GameObject emptyHint;

    [Header("Filters")]
    [SerializeField] private string gameTagKey = "game_tag";
    [SerializeField] private string gameTagValue = "LootOrBoot";
    [SerializeField] private string hostIpKey = "host_ip";
    [SerializeField] private string hostPortKey = "host_port";
    [SerializeField] private string hostNameKey = "host_name";
    [SerializeField] private int maxResults = 50;

    [Header("Ports (Netcode/UTP)")]
    [SerializeField] private ushort gamePort = 7777;

    private enum ListMode { Public, FriendsOnly }
    private ListMode _mode = ListMode.Public;

    private readonly List<CSteamID> _currentList = new();

    private Callback<LobbyCreated_t> _cbLobbyCreated;
    private Callback<LobbyEnter_t> _cbLobbyEnter;
    private Callback<LobbyMatchList_t> _cbLobbyMatchList;

    private CSteamID _pendingJoinLobby;

    private void Awake()
    {
        _cbLobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        _cbLobbyEnter = Callback<LobbyEnter_t>.Create(OnLobbyEnter);
        _cbLobbyMatchList = Callback<LobbyMatchList_t>.Create(OnLobbyMatchList);
    }

    public void AutoRefreshIfPossible()
    {
        if (!SteamBootstrapper.IsReady) return;
        OnClickRefreshLobbies();
    }

    public void OnClickCreateLobbySteam()
    {
        if (!SteamBootstrapper.IsReady)
        {
            SetStatus("Steam hazır değil. Steam açık mı? (steam_appid.txt=480)");
            return;
        }

        SteamSession.SetSteamEnabled(true);
        SteamSession.SetLocalNameFromSteam();

        SetStatus("Steam lobby oluşturuluyor...");
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 4);
    }

    public void OnClickRefreshLobbies()
    {
        _mode = ListMode.Public;
        RequestLobbyList();
    }

    public void OnClickJoinFriend()
    {
        _mode = ListMode.FriendsOnly;
        RequestLobbyList();
    }

    private void RequestLobbyList()
    {
        if (!SteamBootstrapper.IsReady)
        {
            SetStatus("Steam hazır değil.");
            return;
        }

        ClearRows();
        if (emptyHint != null) emptyHint.SetActive(false);

        SteamMatchmaking.AddRequestLobbyListResultCountFilter(maxResults);

        // Only our game
        SteamMatchmaking.AddRequestLobbyListStringFilter(
            gameTagKey, gameTagValue,
            ELobbyComparison.k_ELobbyComparisonEqual
        );

        SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);

        SetStatus(_mode == ListMode.FriendsOnly ? "Arkadaş lobileri (süzme) aranıyor..." : "Public lobby listesi alınıyor...");
        SteamMatchmaking.RequestLobbyList();
    }

    private void OnLobbyMatchList(LobbyMatchList_t cb)
    {
        int count = (int)cb.m_nLobbiesMatching;
        SetStatus($"Bulunan lobby: {count}");

        _currentList.Clear();

        if (count <= 0)
        {
            if (emptyHint != null) emptyHint.SetActive(true);
            return;
        }
        if (emptyHint != null) emptyHint.SetActive(false);

        if (listContent == null || rowPrefab == null)
        {
            Debug.LogError("[SteamLobbyUI] listContent veya rowPrefab atanmadı!");
            return;
        }

        int shown = 0;

        for (int i = 0; i < count; i++)
        {
            CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(i);

            // FriendsOnly modunda: lobby owner friend değilse geç
            if (_mode == ListMode.FriendsOnly)
            {
                CSteamID owner = SteamMatchmaking.GetLobbyOwner(lobbyId);
                var rel = SteamFriends.GetFriendRelationship(owner);
                if (rel != EFriendRelationship.k_EFriendRelationshipFriend)
                    continue;
            }

            _currentList.Add(lobbyId);

            string hostIp = SteamMatchmaking.GetLobbyData(lobbyId, hostIpKey);
            string hostPortStr = SteamMatchmaking.GetLobbyData(lobbyId, hostPortKey);

            string hostName = SteamMatchmaking.GetLobbyData(lobbyId, hostNameKey);
            if (string.IsNullOrWhiteSpace(hostName))
            {
                string ownerName = SteamFriends.GetFriendPersonaName(SteamMatchmaking.GetLobbyOwner(lobbyId));
                hostName = ownerName;
            }

            int members = SteamMatchmaking.GetNumLobbyMembers(lobbyId);

            var row = Instantiate(rowPrefab, listContent);
            row.Bind(hostName, members, hostIp, hostPortStr, () => JoinLobby(lobbyId));
            shown++;
        }

        if (_mode == ListMode.FriendsOnly)
            SetStatus($"Arkadaş lobileri: {shown}");
    }

    private void JoinLobby(CSteamID lobbyId)
    {
        if (!SteamBootstrapper.IsReady)
        {
            SetStatus("Steam hazır değil.");
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

        SteamSession.SetSteamEnabled(true);
        SteamSession.SetLocalNameFromSteam();

        // Host info
        string hostIp = SteamMatchmaking.GetLobbyData(lobbyId, hostIpKey);
        string hostPortStr = SteamMatchmaking.GetLobbyData(lobbyId, hostPortKey);

        // Host name
        string hostName = SteamMatchmaking.GetLobbyData(lobbyId, hostNameKey);
        if (string.IsNullOrWhiteSpace(hostName))
            hostName = SteamFriends.GetFriendPersonaName(SteamMatchmaking.GetLobbyOwner(lobbyId));

        SteamSession.SetLobby(lobbyId, hostName);

        if (string.IsNullOrWhiteSpace(hostIp) ||
            string.IsNullOrWhiteSpace(hostPortStr) ||
            !ushort.TryParse(hostPortStr, out ushort hostPort))
        {
            SetStatus("Lobby data eksik: host_ip/host_port yok.");
            return;
        }

        SetStatus($"Bağlanıyor: {hostIp}:{hostPort}");
        StartNgoClient(hostIp, hostPort);
    }

    private void OnLobbyCreated(LobbyCreated_t cb)
    {
        if (cb.m_eResult != EResult.k_EResultOK)
        {
            SetStatus("Lobby oluşturulamadı.");
            return;
        }

        CSteamID lobbyId = new CSteamID(cb.m_ulSteamIDLobby);

        SteamSession.SetSteamEnabled(true);
        SteamSession.SetLocalNameFromSteam();

        string hostName = SteamSession.LocalPlayerName;
        if (string.IsNullOrWhiteSpace(hostName))
            hostName = SteamFriends.GetPersonaName();

        SteamSession.SetLobby(lobbyId, hostName);

        SteamMatchmaking.SetLobbyData(lobbyId, gameTagKey, gameTagValue);

        string myIp = LanIpUtil.GetLocalIPv4();
        SteamMatchmaking.SetLobbyData(lobbyId, hostIpKey, myIp);
        SteamMatchmaking.SetLobbyData(lobbyId, hostPortKey, gamePort.ToString());

        SteamMatchmaking.SetLobbyData(lobbyId, hostNameKey, hostName);

        StartNgoHost();

        SetStatus($"Lobby oluşturuldu. Host: {hostName} / {myIp}:{gamePort}");
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
            SetStatus("Host başlatılamadı (NGO).");
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
            SetStatus("UnityTransport bulunamadı.");
            return;
        }

        utp.SetConnectionData(ip, port);

        if (!NetworkManager.Singleton.StartClient())
        {
            SetStatus("Client başlatılamadı (NGO).");
        }
    }

    private UnityTransport GetTransport()
    {
        if (NetworkManager.Singleton == null) return null;

        var utp = NetworkManager.Singleton.NetworkConfig.NetworkTransport as UnityTransport;
        if (utp != null) return utp;

        return NetworkManager.Singleton.GetComponent<UnityTransport>();
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

    // Eğer bir yerde AppId_t -> uint lazımsa:
    private static uint GetAppIdUInt()
    {
        return SteamUtils.GetAppID().m_AppId;
    }
}
