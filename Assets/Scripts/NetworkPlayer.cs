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
    [SerializeField] private Transform headPivot;          // HeadPivot objesi

    public Transform HeadPivot => headPivot;

    public static NetworkPlayer Local { get; private set; }

    // ==== Lobby / Takým Bilgileri ====

    // Takým bilgisi
    public NetworkVariable<Team> PlayerTeam = new NetworkVariable<Team>(
        Team.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Ready durumu
    public NetworkVariable<bool> IsReady = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Bu player host mu? (NetworkBehaviour.IsHost ile karýþmasýn diye isim farklý)
    public NetworkVariable<bool> IsHostPlayer = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Game sahnesinde hangi koltuða oturacaðý (0–3)
    public NetworkVariable<int> SeatIndex = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ===========================
    //  LÝFECYCLE
    // ===========================

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Referanslarý inspector’da atamadýysan güvence olsun
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);
        if (playerCamera != null && audioListener == null)
            audioListener = playerCamera.GetComponent<AudioListener>();

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
            {
                LobbyManager.Instance.RefreshUIClientRpc();
            }
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

    // ===========================
    //  UI'dan server'a gelen istekler
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
