using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkTransitionHook : NetworkBehaviour
{
    [Header("Timings")]
    [SerializeField] private float lobbyVisibleSeconds = 10f; // Start'a basýnca en az bu kadar açýk
    [SerializeField] private float holdAfterGameSceneLoaded = 5f;

    private bool _transitionRequested;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Netcode scene eventlerini dinle
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;
        }
    }

    /// <summary>
    /// Lobby'de Start butonuna basýnca bunu çaðýr (Host/Server).
    /// </summary>
    public void HostStartGame_WithTransition(string gameSceneName)
    {
        if (!IsServer) return;
        if (_transitionRequested) return;

        _transitionRequested = true;

        // 1) Herkeste paneli aç
        ShowTransitionClientRpc();

        // 2) Scene load baþlat
        NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);

        // Not: lobbyVisibleSeconds kadar bekleme zorunlu deðil:
        // UI zaten fakeFillDuration=10sn set.
        // Ama istersen ekstra garanti için burada bir þey yapmaya gerek yok.
    }

    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        // Bu event hem server hem client tarafýnda gelir.
        // Bize: "Game scene load complete oldu" aný lazým.
        // SceneEventType.LoadComplete: bir client için yükleme tamamlandý
        // SceneEventType.LoadEventCompleted: tüm clientlar tamamladý (server tarafýnda)
        if (!_transitionRequested) return;

        if (sceneEvent.SceneEventType == SceneEventType.LoadComplete)
        {
            // Her client kendi LoadComplete'inde "loaded" sayýlýr,
            // ama paneli kapatma kararýný server versin istiyoruz.
            return;
        }

        if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted)
        {
            // Server: herkes yükledi -> þimdi "barý tamamla ve 5sn sonra kapa" de
            if (IsServer)
            {
                MarkLoadedClientRpc(holdAfterGameSceneLoaded);
                _transitionRequested = false;
            }
        }
    }

    [ClientRpc]
    private void ShowTransitionClientRpc()
    {
        if (TransitionUIController.Instance != null)
            TransitionUIController.Instance.Show();
    }

    [ClientRpc]
    private void MarkLoadedClientRpc(float holdSeconds)
    {
        if (TransitionUIController.Instance != null)
            TransitionUIController.Instance.MarkSceneLoadedAndHold(holdSeconds);
    }
}
