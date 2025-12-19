using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerEmotes : NetworkBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator animator;

    [Header("Trigger Names (Animator)")]
    [SerializeField] private string trigHead360 = "Trig_Head360";
    [SerializeField] private string trigChew = "Trig_Chew";
    [SerializeField] private string trigSlay = "Trig_Slay";
    [SerializeField] private string trigThumbsup = "Trig_Thumbsup";

    private int _hashHead360;
    private int _hashChew;
    private int _hashSlay;
    private int _hashThumbsup;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        _hashHead360 = Animator.StringToHash(trigHead360);
        _hashChew = Animator.StringToHash(trigChew);
        _hashSlay = Animator.StringToHash(trigSlay);
        _hashThumbsup = Animator.StringToHash(trigThumbsup);
    }

    private void Update()
    {
        // Input sadece owner’da okunur -> double tetik kaynaðýný keser
        if (!IsOwner) return;
        if (!IsSpawned) return;

        if (Input.GetKeyDown(KeyCode.Alpha2))
            RequestEmoteServerRpc(EmoteType.Head360);

        if (Input.GetKeyDown(KeyCode.Alpha3))
            RequestEmoteServerRpc(EmoteType.Chew);

        if (Input.GetKeyDown(KeyCode.Alpha4))
            RequestEmoteServerRpc(EmoteType.Slay);

        if (Input.GetKeyDown(KeyCode.Alpha5))
            RequestEmoteServerRpc(EmoteType.Thumbsup);

    }

    private enum EmoteType : byte
    {
        Head360 = 0,
        Chew = 1,
        Slay=2,
        Thumbsup=3,
    }

    [ServerRpc]
    private void RequestEmoteServerRpc(EmoteType type, ServerRpcParams rpcParams = default)
    {
        // Server herkese tek sefer yayýnlar
        PlayEmoteClientRpc(type);
    }

    [ClientRpc]
    private void PlayEmoteClientRpc(EmoteType type)
    {
        if (animator == null) return;

        // Güvenli: trigger resetleyip sonra setle -> “takýlý kalma” / üst üste tetik problemlerini azaltýr
        switch (type)
        {
            case EmoteType.Head360:
                animator.ResetTrigger(_hashHead360);
                animator.SetTrigger(_hashHead360);
                break;

            case EmoteType.Chew:
                animator.ResetTrigger(_hashChew);
                animator.SetTrigger(_hashChew);
                break;

            case EmoteType.Slay:
                animator.ResetTrigger(_hashSlay);
                animator.SetTrigger(_hashSlay);
                break;

            case EmoteType.Thumbsup:
                animator.ResetTrigger(_hashThumbsup);
                animator.SetTrigger(_hashThumbsup);
                break;
        }
    }
}
