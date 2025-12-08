using UnityEngine;

public class KempsInputController : MonoBehaviour
{
    public void OnClickPass()
    {
        Debug.Log("[Input] PASS týklandý");

        if (KempsGameManager.Instance != null)
            KempsGameManager.Instance.RequestPassServerRpc();
    }

    public void OnClickKemps()
    {
        Debug.Log("[Input] KEMPS týklandý");

        if (KempsGameManager.Instance != null)
            KempsGameManager.Instance.RequestKempsServerRpc();
    }

    public void OnClickUnkemps()
    {
        Debug.Log("[Input] UNKEMPS týklandý");

        if (KempsGameManager.Instance != null)
            KempsGameManager.Instance.RequestUnkempsServerRpc();
    }

    // Ortadaki kart / el kartýna týklama için ileride þunlarý kullanacaðýz:
    public void OnClickCenterCard(int index)
    {
        Debug.Log($"[INPUT] Center card {index} týklandý.");

        if (KempsGameManager.Instance != null)
        {
            KempsGameManager.Instance.RequestReserveCenterCardServerRpc(index);
        }
    }

    public void OnClickHandCard(int index)
    {
        Debug.Log($"[INPUT] Hand card {index} týklandý.");

        if (KempsGameManager.Instance != null)
        {
            KempsGameManager.Instance.RequestSwapHandCardServerRpc(index);
        }
    }
}
