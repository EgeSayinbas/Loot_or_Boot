using Unity.Netcode;
using Unity.Collections;
using UnityEngine;
using Steamworks;

public class SteamIdentityNet : NetworkBehaviour
{
    // UI için güvenli: kýsa string
    public NetworkVariable<FixedString32Bytes> DisplayName =
        new(writePerm: NetworkVariableWritePermission.Server);

    // Ýstersen SteamID de yayýnlayalým (ileride lazým olur)
    public NetworkVariable<ulong> SteamId =
        new(writePerm: NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        // Steam hazýr deðilse LAN modda fallback
        string name = "Player";
        ulong sid = 0;

        if (SteamBootstrapper.IsReady)
        {
            name = SteamFriends.GetPersonaName();
            sid = SteamUser.GetSteamID().m_SteamID;
        }

        SubmitIdentityServerRpc(name, sid);
    }

    [ServerRpc(RequireOwnership = true)]
    private void SubmitIdentityServerRpc(string name, ulong steamId)
    {
        // Çok uzun adlar UI’yý bozmasýn
        if (string.IsNullOrWhiteSpace(name)) name = "Player";
        if (name.Length > 28) name = name.Substring(0, 28);

        DisplayName.Value = name;
        SteamId.Value = steamId;
    }
}
