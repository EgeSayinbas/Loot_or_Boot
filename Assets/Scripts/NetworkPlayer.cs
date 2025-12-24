using UnityEngine;
using Unity.Netcode;
using Unity.Collections;

// Ayrı Team.cs kullanmıyoruz; enum burada dursun
public enum Team
{
    None,
    Red,
    Blue
}

public class NetworkPlayer : NetworkBehaviour
{
    [Header("Camera / Head")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private AudioListener audioListener;
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private Transform headAim;

    [Header("Hand Slots (Player Prefab)")]
    [SerializeField] private Transform handSlotRoot;
    [SerializeField] private Transform[] handSlots = new Transform[4];

    public Transform CameraPivot => cameraPivot;
    public Transform HeadAim => headAim;

    public Transform HandSlotRoot => handSlotRoot;
    public Transform GetHandSlot(int index)
    {
        if (index < 0 || index >= handSlots.Length) return null;
        return handSlots[index];
    }

    public static NetworkPlayer Local { get; private set; }

    // ==== Steam Display Name (SYNC) ====
    public NetworkVariable<FixedString64Bytes> PlayerSteamName = new NetworkVariable<FixedString64Bytes>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public string GetDisplayName()
    {
        var s = PlayerSteamName.Value.ToString();
        return string.IsNullOrWhiteSpace(s) ? "" : s;
    }

    // ==== Lobby / Takım Bilgileri ====
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

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

        if (playerCamera != null && audioListener == null)
            audioListener = playerCamera.GetComponent<AudioListener>();

        if (cameraPivot == null && playerCamera != null)
            cameraPivot = playerCamera.transform.parent;

        if (headAim == null)
            headAim = FindDeepChild(transform, "HeadAim");

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

            // Owner kendi ismini server'a gönderir (Steam moddaysa)
            TrySendMyNameToServer();
        }
        else
        {
            EnableLocalCamera(false);
        }

        if (IsServer && OwnerClientId == NetworkManager.Singleton.LocalClientId)
        {
            IsHostPlayer.Value = true;

            if (LobbyManager.Instance != null)
                LobbyManager.Instance.RefreshUIClientRpc();
        }

        // İsim değişince UI yenilemek istersen:
        PlayerSteamName.OnValueChanged += (_, __) =>
        {
            if (LobbyManager.Instance != null)
                LobbyManager.Instance.RefreshUIClientRpc();
        };
    }

    protected new void OnDestroy()
    {
        if (Local == this)
            Local = null;
    }

    private void TrySendMyNameToServer()
    {
        // SteamSession Local name hazırla
        if (SteamSession.IsSteam)
            SteamSession.SetLocalNameFromSteam();

        string name = SteamSession.IsSteam ? SteamSession.LocalPlayerName : "";

        if (!string.IsNullOrWhiteSpace(name))
            SubmitMyDisplayNameServerRpc(name);
    }

    private void EnableLocalCamera(bool enable)
    {
        if (playerCamera != null)
            playerCamera.enabled = enable;

        if (audioListener != null)
            audioListener.enabled = enable;
    }

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
        IsReady.Value = false;

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

    // ===========================
    //  Avoid extra dependencies: name submit
    // ===========================
    [ServerRpc]
    private void SubmitMyDisplayNameServerRpc(string name, ServerRpcParams rpcParams = default)
    {
        // Basit sanitize
        if (string.IsNullOrWhiteSpace(name)) return;
        if (name.Length > 32) name = name.Substring(0, 32);

        PlayerSteamName.Value = new FixedString64Bytes(name);

        if (LobbyManager.Instance != null)
            LobbyManager.Instance.RefreshUIClientRpc();
    }
}
