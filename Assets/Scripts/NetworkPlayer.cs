using UnityEngine;
using Unity.Netcode;

// Ayrý Team.cs kullanmýyoruz; enum burada dursun
public enum Team
{
    None,
    Red,
    Blue
}

public class NetworkPlayer : NetworkBehaviour
{
    [Header("Camera / Head")]
    [SerializeField] private Camera playerCamera;          // PlayerCamera child
    [SerializeField] private AudioListener audioListener;  // PlayerCamera üstündeki
    [SerializeField] private Transform cameraPivot;        // CameraPivot
    [SerializeField] private Transform headAim;            // HeadAim (rig içinde)

    [Header("Hand Slots (Player Prefab)")]
    [SerializeField] private Transform handSlotRoot;       // HandR_CardSlotRoot
    [SerializeField] private Transform[] handSlots = new Transform[4]; // HandR_CardSlot_0..3

    public Transform CameraPivot => cameraPivot;
    public Transform HeadAim => headAim;

    public Transform HandSlotRoot => handSlotRoot;
    public Transform GetHandSlot(int index)
    {
        if (index < 0 || index >= handSlots.Length) return null;
        return handSlots[index];
    }

    public static NetworkPlayer Local { get; private set; }

    // ==== Lobby / Takým Bilgileri ====
    public NetworkVariable<Team> PlayerTeam = new NetworkVariable<Team>(
        Team.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> IsReady = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> IsHostPlayer = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> SeatIndex = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );


   
    // ===========================
    //  LIFECYCLE
    // ===========================
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Referanslarý inspector’da atamadýysan güvence olsun
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

        if (playerCamera != null && audioListener == null)
            audioListener = playerCamera.GetComponent<AudioListener>();

        if (cameraPivot == null && playerCamera != null)
            cameraPivot = playerCamera.transform.parent; // PlayerCamera parent = CameraPivot

        if (headAim == null)
            headAim = FindDeepChild(transform, "HeadAim");

        // Hand slot root + 0..3 bul
        if (handSlotRoot == null)
            handSlotRoot = FindDeepChild(transform, "HandR_CardSlotRoot");

        if (handSlotRoot != null)
        {
            for (int i = 0; i < 4; i++)
            {
                if (handSlots[i] == null)
                    handSlots[i] = FindDeepChild(handSlotRoot, $"HandR_CardSlot_{i}");
            }
        }

        if (IsOwner)
        {
            Local = this;
            EnableLocalCamera(true);
        }
        else
        {
            EnableLocalCamera(false);
        }

        // Ýlk host kendini host olarak iþaretlesin
        if (IsServer && OwnerClientId == NetworkManager.Singleton.LocalClientId)
        {
            IsHostPlayer.Value = true;

            if (LobbyManager.Instance != null)
                LobbyManager.Instance.RefreshUIClientRpc();
        }
    }

    protected new void OnDestroy()
    {
        if (Local == this)
            Local = null;
    }

    private void EnableLocalCamera(bool enable)
    {
        if (playerCamera != null)
            playerCamera.enabled = enable;

        if (audioListener != null)
            audioListener.enabled = enable;
    }

    // Derin child arama (name ile)
    private static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent == null) return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c.name == name) return c;

            var r = FindDeepChild(c, name);
            if (r != null) return r;
        }
        return null;
    }

    // ===========================
    //  UI'dan server'a gelen istekler (KORUNDU)
    // ===========================
    [ServerRpc(RequireOwnership = false)]
    public void RequestChangeTeamServerRpc(Team newTeam, ServerRpcParams rpcParams = default)
    {
        PlayerTeam.Value = newTeam;
        IsReady.Value = false; // takým deðiþince ready sýfýrlansýn

        if (LobbyManager.Instance != null)
            LobbyManager.Instance.RefreshUIClientRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestSetReadyServerRpc(bool ready, ServerRpcParams rpcParams = default)
    {
        IsReady.Value = ready;

        if (LobbyManager.Instance != null)
            LobbyManager.Instance.RefreshUIClientRpc();
    }

  
}
