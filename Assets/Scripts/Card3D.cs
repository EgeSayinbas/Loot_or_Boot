using UnityEngine;
using Unity.Netcode;

public class Card3D : NetworkBehaviour
{
    public int cardIndex;        // Merkezdeyse 0–3, eldeyse 0–3
    public bool isCenterCard;
    public int ownerSeat = -1;   // El kartýysa hangi seat

    private Renderer rend;

    private void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
    }

    private void OnMouseDown()
    {
        if (!IsOwner && NetworkManager.Singleton.IsClient)
            return;

        if (isCenterCard)
        {
            KempsGameManager.Instance
                .RequestReserveCenterCardServerRpc(cardIndex);
        }
        else
        {
            KempsGameManager.Instance
                .RequestSwapHandCardServerRpc(cardIndex);
        }
    }

    public void SetHighlight(bool active)
    {
        if (rend == null) return;

        rend.material.color = active ? Color.yellow : Color.white;
    }
}

