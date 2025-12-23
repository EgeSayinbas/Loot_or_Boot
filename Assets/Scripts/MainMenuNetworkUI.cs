using System.Collections;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuNetworkUI : MonoBehaviour
{
    [Header("Scenes")]
    public string lobbySceneName = "Lobby";

    [Header("Join UI (MainMenu)")]
    [SerializeField] private TMP_InputField roomIdInput;   // Lobby Code
    [SerializeField] private TMP_Text joinHintText;

    [Header("Ports")]
    [SerializeField] private ushort gamePort = 7777;

    [Header("Discovery Refs (DontDestroyOnLoad)")]
    [SerializeField] private LobbyCodeService codeService;
    [SerializeField] private LanDiscoveryHost discoveryHost;
    [SerializeField] private LanDiscoveryClient discoveryClient;

    [Header("Join Settings")]
    [SerializeField] private float joinResolveTimeout = 2.0f; // saniye
    [SerializeField] private int codeLength = 6;

    [Header("Debug / Safety")]
    [SerializeField] private float lobbySyncTimeout = 6f; // client bağlandıktan sonra lobby sync bekleme

    private bool _discoveryStarted;
    private bool _waitingLobbySync;

    private void Awake()
    {
        EnsureServices();

        // discovery client bir kez başlasın
        StartDiscoveryOnce();
    }

    private void OnEnable()
    {
        HookSceneEvents(true);
    }

    private void OnDisable()
    {
        HookSceneEvents(false);
    }

    public void OnClickHost()
    {
        if (NetworkManager.Singleton == null) return;

        EnsureServices();

        // 1) Host code üret
        if (codeService == null)
        {
            Debug.LogError("[MainMenuNetworkUI] LobbyCodeService yok!");
            return;
        }

        codeService.EnsureHostCode(codeLength);
        string code = codeService.CurrentHostCode;

        // 2) Transport: server bind
        var utp = GetTransport();
        if (utp != null)
        {
            utp.SetConnectionData("0.0.0.0", gamePort, "0.0.0.0");
        }

        // 3) Host başlat
        if (NetworkManager.Singleton.StartHost())
        {
            Debug.Log($"Host started. LobbyCode={code} port={gamePort}");

            // Lobby ekranında göstermek için
            KempsSession.LobbyId = code;

            // 4) Discovery host başlat
            if (discoveryHost != null)
                discoveryHost.StartHostDiscovery(code, gamePort);

            // 5) Lobby’ye geç
            NetworkManager.Singleton.SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
        }
        else
        {
            Debug.LogError("Failed to start host.");
            if (joinHintText != null) joinHintText.text = "Host başlatılamadı.";
        }
    }

    public void OnClickClient()
    {
        if (NetworkManager.Singleton == null) return;

        EnsureServices();

        string code = roomIdInput != null ? roomIdInput.text : "";
        code = LanLobbyCodeUtil.Normalize(code);

        if (!LanLobbyCodeUtil.IsValid(code, codeLength))
        {
            if (joinHintText != null) joinHintText.text = $"Lobby Code {codeLength} haneli olmalı.";
            return;
        }

        // ✅ Client ekranında da girilen kod görünsün (LobbyHostIpDisplay bunu okuyacak)
        KempsSession.LobbyId = code;

        StartCoroutine(Co_ResolveAndJoin(code));
    }

    private IEnumerator Co_ResolveAndJoin(string code)
    {
        if (joinHintText != null) joinHintText.text = $"Kod aranıyor: {code} ...";

        // Önce cache’de var mı?
        if (codeService != null && codeService.TryGetMapping(code, out string ipCached, out ushort portCached))
        {
            yield return StartCoroutine(Co_StartClientTo(code, ipCached, portCached));
            yield break;
        }

        // Yoksa query at + timeout içinde bekle
        float t = 0f;
        while (t < joinResolveTimeout)
        {
            t += Time.unscaledDeltaTime;

            if (discoveryClient != null)
                discoveryClient.QueryCode(code);

            // kısa bekle
            float step = 0.2f;
            float w = 0f;
            while (w < step)
            {
                w += Time.unscaledDeltaTime;
                yield return null;
            }

            if (codeService != null && codeService.TryGetMapping(code, out string ip, out ushort port))
            {
                yield return StartCoroutine(Co_StartClientTo(code, ip, port));
                yield break;
            }
        }

        if (joinHintText != null) joinHintText.text = $"Host bulunamadı. (Kod: {code})";
        Debug.LogWarning($"[MainMenuNetworkUI] Resolve timeout for code={code}. Firewall/UDP Broadcast engelli olabilir.");
    }

    private IEnumerator Co_StartClientTo(string code, string ip, ushort port)
    {
        var utp = GetTransport();
        if (utp == null)
        {
            if (joinHintText != null) joinHintText.text = "UnityTransport bulunamadı.";
            yield break;
        }

        utp.SetConnectionData(ip, port);

        // scene management açık mı? (client lobby’ye geçmiyorsa en kritik kontrol)
        if (NetworkManager.Singleton.SceneManager == null)
            Debug.LogWarning("[MainMenuNetworkUI] NetworkSceneManager NULL. Enable Scene Management açık mı?");

        if (NetworkManager.Singleton.StartClient())
        {
            Debug.Log($"Client started. Connecting to {ip}:{port} (code={code})");
            if (joinHintText != null) joinHintText.text = $"Bağlanıyor: {ip}:{port}";

            // ✅ Client bağlandıktan sonra lobby scene sync bekle (log için)
            _waitingLobbySync = true;
            StartCoroutine(Co_WaitLobbySceneSync());
        }
        else
        {
            Debug.LogError("Failed to start client.");
            if (joinHintText != null) joinHintText.text = "Client başlatılamadı.";
        }

        yield break;
    }

    private IEnumerator Co_WaitLobbySceneSync()
    {
        float t = 0f;
        while (t < lobbySyncTimeout && _waitingLobbySync)
        {
            t += Time.unscaledDeltaTime;

            // zaten lobby ise bırak
            if (SceneManager.GetActiveScene().name == lobbySceneName)
            {
                _waitingLobbySync = false;
                yield break;
            }

            yield return null;
        }

        if (_waitingLobbySync)
        {
            Debug.LogWarning("[MainMenuNetworkUI] Client bağlandı ama Lobby scene sync gelmedi. " +
                             "Build Scene list / sahne isimleri / Enable Scene Management / firewall kontrol et.");
            if (joinHintText != null) joinHintText.text = "Bağlandı ama Lobby yüklenmedi (Scene Sync yok).";
        }
    }

    // ---------------- Scene Events ----------------

    private void HookSceneEvents(bool hook)
    {
        if (NetworkManager.Singleton == null) return;
        if (NetworkManager.Singleton.SceneManager == null) return;

        if (hook)
            NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;
        else
            NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;
    }

    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        // Client tarafında sahne senkronunun geldiğini burada görürüz
        if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsClient) return;

        if (sceneEvent.SceneEventType == SceneEventType.SynchronizeComplete)
        {
            Debug.Log("[MainMenuNetworkUI] Client SynchronizeComplete (scene sync tamam). ActiveScene=" +
                      SceneManager.GetActiveScene().name);
        }

        if (sceneEvent.SceneEventType == SceneEventType.LoadComplete)
        {
            Debug.Log($"[MainMenuNetworkUI] LoadComplete: {sceneEvent.SceneName} (clientId={sceneEvent.ClientId})");

            if (sceneEvent.SceneName == lobbySceneName && sceneEvent.ClientId == NetworkManager.Singleton.LocalClientId)
            {
                _waitingLobbySync = false;
                if (joinHintText != null) joinHintText.text = "Lobby yüklendi.";
            }
        }
    }

    // ---------------- Helpers ----------------

    private void EnsureServices()
    {
        if (codeService == null) codeService = FindFirstObjectByType<LobbyCodeService>();
        if (discoveryHost == null) discoveryHost = FindFirstObjectByType<LanDiscoveryHost>();
        if (discoveryClient == null) discoveryClient = FindFirstObjectByType<LanDiscoveryClient>();
    }

    private void StartDiscoveryOnce()
    {
        if (_discoveryStarted) return;
        _discoveryStarted = true;

        if (discoveryClient != null)
            discoveryClient.StartClientDiscovery();
    }

    private UnityTransport GetTransport()
    {
        if (NetworkManager.Singleton == null) return null;

        var utp = NetworkManager.Singleton.NetworkConfig.NetworkTransport as UnityTransport;
        if (utp != null) return utp;

        return NetworkManager.Singleton.GetComponent<UnityTransport>();
    }
}
