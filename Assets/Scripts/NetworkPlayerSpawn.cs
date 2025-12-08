using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class NetworkPlayerSpawn : NetworkBehaviour
{
    private Transform[] spawnPoints;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            StartCoroutine(PlaceAtSpawnPointWhenReady());
        }
    }

    private IEnumerator PlaceAtSpawnPointWhenReady()
    {
        // Önce NetworkPlayer ve SeatIndex hazýr olsun
        NetworkPlayer netPlayer = GetComponent<NetworkPlayer>();
        while (netPlayer == null || netPlayer.SeatIndex.Value < 0)
        {
            netPlayer = GetComponent<NetworkPlayer>();
            yield return null;
        }

        // SpawnPoints sahnede bulunana kadar bekle
        while (spawnPoints == null || spawnPoints.Length == 0)
        {
            RefreshSpawnPoints();

            if (spawnPoints == null || spawnPoints.Length == 0)
                yield return null;
        }

        int index = Mathf.Clamp(netPlayer.SeatIndex.Value, 0, spawnPoints.Length - 1);
        Transform sp = spawnPoints[index];

        Vector3 pos = sp.position;
        Quaternion rot = sp.rotation;

        // Server tarafýnda kendisini doðru yere koy
        transform.SetPositionAndRotation(pos, rot);

        // Tüm client'lere de ayný pozisyonu gönder
        SetPositionClientRpc(pos, rot);
    }

    private void RefreshSpawnPoints()
    {
        GameObject spRoot = GameObject.Find("SpawnPoints");
        if (spRoot != null)
        {
            int count = spRoot.transform.childCount;
            spawnPoints = new Transform[count];

            for (int i = 0; i < count; i++)
            {
                spawnPoints[i] = spRoot.transform.GetChild(i);
            }
        }
    }

    [ClientRpc]
    private void SetPositionClientRpc(Vector3 pos, Quaternion rot)
    {
        // Host dahil tüm client'ler kendi objesini ayný yere koyar
        transform.SetPositionAndRotation(pos, rot);
    }
}
