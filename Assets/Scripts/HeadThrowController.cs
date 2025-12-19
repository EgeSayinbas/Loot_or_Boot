using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 1'e basınca kafayı fırlatır:
/// - Orijinal headBoneScale (veya head mesh root) scale=0 olur (tüm clientlarda)
/// - Server, head prefabı spawn eder ve velocity verir (herkes aynı görür)
/// - Owner kamera kafayı takip eder
/// - 2sn sonra kafa "uçarak" geri döner, sonra despawn + kafa geri görünür
/// - Geri dönmeden tekrar 1'e basılamaz (server kilidi)
/// </summary>
public class HeadThrowController : NetworkBehaviour
{
    [Header("Refs (Player Prefab)")]
    [SerializeField] private Transform cameraPivot;          // senin CameraPivot
    [SerializeField] private Transform headSocket;           // kafanın normalde durduğu yer (bone/empty)
    [SerializeField] private Transform headBoneScale;        // scale=0 yapılacak obje (HeadBoneScale)

    [Header("Head Prefab (Networked)")]
    [SerializeField] private NetworkObject headProjectilePrefab;

    [Header("Throw Settings")]
    [SerializeField] private float throwSpeed = 7.5f;
    [SerializeField] private float upwardBoost = 0.35f;      // biraz yukarı da kalksın
    [SerializeField] private float returnDelay = 2.0f;

    [Header("Return Flight")]
    [SerializeField] private float returnDuration = 0.35f;   // teleport değil, kısa bir "uçuş"
    [SerializeField] private AnimationCurve returnCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // Server yazar, herkes okur
    private NetworkVariable<bool> isHeadThrown = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // Spawn edilen head objesinin NetId'si (camera follow için)
    private NetworkVariable<ulong> thrownHeadNetId = new NetworkVariable<ulong>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // Owner camera restore
    private Transform _cameraOriginalParent;
    private Vector3 _cameraOriginalLocalPos;
    private Quaternion _cameraOriginalLocalRot;
    private bool _cameraAttached;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // güvence (istersen inspector’dan zaten atarsın)
        if (cameraPivot == null)
        {
            var np = GetComponent<NetworkPlayer>();
            if (np != null) cameraPivot = np.CameraPivot;
        }

        thrownHeadNetId.OnValueChanged += OnThrownHeadNetIdChanged;
        isHeadThrown.OnValueChanged += (_, __) =>
        {
            // istersen debug
            // Debug.Log($"[HeadThrow] isHeadThrown => {isHeadThrown.Value}");
        };
    }

    private void OnDestroyc()
    {
        thrownHeadNetId.OnValueChanged -= OnThrownHeadNetIdChanged;
    }

    private void Update()
    {
        if (!IsOwner) return;

        // tekrar basmayı localde de engelle (asıl kilit server)
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            if (isHeadThrown.Value) return;

            if (cameraPivot == null || headSocket == null || headProjectilePrefab == null || headBoneScale == null)
            {
                Debug.LogError("[HeadThrow] Missing refs! (cameraPivot/headSocket/headProjectilePrefab/headBoneScale)");
                return;
            }

            Vector3 forward = cameraPivot.forward;
            // Çok aşağı bakıyorsa “drop” hissi vermesin diye biraz yukarı ekle:
            Vector3 dir = (forward + Vector3.up * upwardBoost).normalized;

            RequestThrowHeadServerRpc(dir);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestThrowHeadServerRpc(Vector3 dir, ServerRpcParams rpcParams = default)
    {
        // sadece kendi owner’ı triggerlasın (başkası adına atmasın)
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

        if (isHeadThrown.Value) return;
        isHeadThrown.Value = true;

        // Orijinal kafayı gizle (herkeste)
        SetHeadVisibleClientRpc(false);

        // Head prefab spawn
        var headObj = Instantiate(headProjectilePrefab, headSocket.position, headSocket.rotation);
        headObj.Spawn(true);

        thrownHeadNetId.Value = headObj.NetworkObjectId;

        // Velocity server’da ver (diğer clientlarda "sadece düşüyor" sorunu buradan geliyordu)
        var rb = headObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = dir.normalized * throwSpeed;
            rb.angularVelocity = Random.onUnitSphere * 6f; // biraz dönsün (istersen kaldır)
        }

        StartCoroutine(Co_ReturnHead_Server(headObj));
    }

    private IEnumerator Co_ReturnHead_Server(NetworkObject headObj)
    {
        yield return new WaitForSeconds(returnDelay);

        if (headObj == null || !headObj.IsSpawned)
        {
            // güvenlik
            thrownHeadNetId.Value = 0;
            isHeadThrown.Value = false;
            SetHeadVisibleClientRpc(true);
            yield break;
        }

        // return uçuşu: physics kapatıp slot'a doğru lerp
        var rb = headObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        Vector3 startPos = headObj.transform.position;
        Quaternion startRot = headObj.transform.rotation;

        float t = 0f;
        while (t < returnDuration)
        {
            if (headObj == null || !headObj.IsSpawned) break;

            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / Mathf.Max(0.0001f, returnDuration));
            float cu = returnCurve != null ? returnCurve.Evaluate(u) : u;

            // headSocket server’da doğru konumda (player animasyonu vs.)
            Vector3 targetPos = headSocket.position;
            Quaternion targetRot = headSocket.rotation;

            headObj.transform.SetPositionAndRotation(
                Vector3.Lerp(startPos, targetPos, cu),
                Quaternion.Slerp(startRot, targetRot, cu)
            );

            yield return null;
        }

        // bitiş: despawn + kafa geri göster
        if (headObj != null && headObj.IsSpawned)
            headObj.Despawn(true);

        thrownHeadNetId.Value = 0;

        SetHeadVisibleClientRpc(true);
        isHeadThrown.Value = false;
    }

    [ClientRpc]
    private void SetHeadVisibleClientRpc(bool visible)
    {
        if (headBoneScale == null) return;

        // “HeadBoneScale” üzerinde scale ile gizle/göster
        headBoneScale.localScale = visible ? Vector3.one : Vector3.zero;
    }

    private void OnThrownHeadNetIdChanged(ulong oldId, ulong newId)
    {
        // sadece owner camera follow
        if (!IsOwner) return;

        if (newId == 0)
        {
            DetachCameraFromHead();
            return;
        }

        // spawn manager’dan objeyi bul
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(newId, out var netObj))
        {
            AttachCameraToHead(netObj.transform);
        }
    }

    private void AttachCameraToHead(Transform headTransform)
    {
        if (cameraPivot == null || headTransform == null) return;
        if (_cameraAttached) return;

        _cameraOriginalParent = cameraPivot.parent;
        _cameraOriginalLocalPos = cameraPivot.localPosition;
        _cameraOriginalLocalRot = cameraPivot.localRotation;

        cameraPivot.SetParent(headTransform, worldPositionStays: true);
        _cameraAttached = true;
    }

    private void DetachCameraFromHead()
    {
        if (!_cameraAttached) return;
        if (cameraPivot == null) return;

        cameraPivot.SetParent(_cameraOriginalParent, worldPositionStays: false);
        cameraPivot.localPosition = _cameraOriginalLocalPos;
        cameraPivot.localRotation = _cameraOriginalLocalRot;

        _cameraAttached = false;
    }
}
