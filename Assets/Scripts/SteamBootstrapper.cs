using UnityEngine;
using Steamworks;

public class SteamBootstrapper : MonoBehaviour
{
    [SerializeField] private bool dontDestroyOnLoad = true;
    public static bool IsReady { get; private set; }

    private static SteamBootstrapper _instance;

    private void Awake()
    {
        if (_instance != null)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        try
        {
            IsReady = SteamAPI.Init();
            if (!IsReady)
                Debug.LogWarning("[SteamBootstrapper] SteamAPI.Init failed. Steam açýk mý? steam_appid.txt=480 var mý?");
            else
                Debug.Log("[SteamBootstrapper] Steam initialized.");
        }
        catch (System.DllNotFoundException e)
        {
            IsReady = false;
            Debug.LogError("[SteamBootstrapper] Steamworks dll bulunamadý. Steamworks.NET import doðru mu?\n" + e);
        }
    }

    private void Update()
    {
        if (!IsReady) return;
        SteamAPI.RunCallbacks();
    }

    private void OnApplicationQuit()
    {
        if (!IsReady) return;
        SteamAPI.Shutdown();
        IsReady = false;
    }
}
