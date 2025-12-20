using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 1'e basınca kafayı fırlatır:
/// - HeadBoneScale scale=0 (tüm clientlarda)
/// - Server head prefab spawn eder, velocity+angularVelocity verir (herkes aynı görür)
/// - SADECE Owner camera head'i takip eder (parent yok, follow var) -> No cameras rendering fix
/// - 2sn sonra kafa "uçarak" geri döner, despawn + kafa geri görünür
/// - Geri dönmeden tekrar 1'e basılamaz (server kilidi)
/// </summary>
public class HeadThrowController : NetworkBehaviour
{
    [Header("Refs (Player Prefab)")]
    [SerializeField] private Transform cameraPivot;          // CameraPivot
    [SerializeField] private Transform headSocket;           // kafanın normalde durduğu yer (bone/empty)
    [SerializeField] private Transform headBoneScale;        // scale=0 yapılacak obje (Head_M vs)

    [Header("Head Prefab (Networked)")]
    [SerializeField] private NetworkObject headProjectilePrefab;

    [Header("Throw Settings")]
    [SerializeField] private float throwSpeed = 7.5f;
    [SerializeField] private float upwardBoost = 0.35f;
    [SerializeField] private float returnDelay = 2.0f;

    [Header("Return Flight")]
    [SerializeField] private float returnDuration = 0.35f;
    [SerializeField] private AnimationCurve returnCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Owner Camera Follow")]
    [SerializeField] private bool followHeadWithRotation = true;  // rotation da takip etsin
    [SerializeField] private Vector3 followOffsetLocal = Vector3.zero; // istersen kafa üzerinden offset

    // Server yazar, herkes okur
    private NetworkVariable<bool> isHeadThrown = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // Spawn edilen head objesinin NetId'si (owner follow için)
    private NetworkVariable<ulong> thrownHeadNetId = new NetworkVariable<ulong>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // Owner follow runtime
    private Transform _followTarget;
    private bool _isFollowing;

    // Owner camera restore
    private Transform _cameraOriginalParent;
    private Vector3 _cameraOriginalLocalPos;
    private Quaternion _cameraOriginalLocalRot;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // güvence
        if (cameraPivot == null)
        {
            var np = GetComponent<NetworkPlayer>();
            if (np != null) cameraPivot = np.CameraPivot;
        }

        thrownHeadNetId.OnValueChanged += OnThrownHeadNetIdChanged;
    }

    private void OnDestroyc()
    {
        thrownHeadNetId.OnValueChanged -= OnThrownHeadNetIdChanged;
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            if (isHeadThrown.Value) return;

            if (cameraPivot == null || headSocket == null || headProjectilePrefab == null || headBoneScale == null)
            {
                Debug.LogError("[HeadThrow] Missing refs! (cameraPivot/headSocket/headProjectilePrefab/headBoneScale)");
                return;
            }

            Vector3 forward = cameraPivot.forward;
            Vector3 dir = (forward + Vector3.up * upwardBoost).normalized;

            RequestThrowHeadServerRpc(dir);
        }
    }

    private void LateUpdate()
    {
        // ✅ parent yok: sadece owner follow
        if (!IsOwner) return;
        if (!_isFollowing) return;
        if (cameraPivot == null || _followTarget == null) return;

        // pozisyon
        Vector3 targetPos = _followTarget.TransformPoint(followOffsetLocal);
        cameraPivot.position = targetPos;

        // rotation
        if (followHeadWithRotation)
            cameraPivot.rotation = _followTarget.rotation;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestThrowHeadServerRpc(Vector3 dir, ServerRpcParams rpcParams = default)
    {
        // sadece kendi owner’ı tetiklesin
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

        if (isHeadThrown.Value) return;
        isHeadThrown.Value = true;

        // Orijinal kafayı gizle (herkeste)
        SetHeadVisibleClientRpc(false);

        // Spawn
        var headObj = Instantiate(headProjectilePrefab, headSocket.position, headSocket.rotation);
        headObj.Spawn(true);

        thrownHeadNetId.Value = headObj.NetworkObjectId;

        // Physics
        var rb = headObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = dir.normalized * throwSpeed;

            // ✅ rotation: dönsün
            rb.angularVelocity = Random.onUnitSphere * 6f;
        }

        StartCoroutine(Co_ReturnHead_Server(headObj));
    }

    private IEnumerator Co_ReturnHead_Server(NetworkObject headObj)
    {
        yield return new WaitForSeconds(returnDelay);

        if (headObj == null || !headObj.IsSpawned)
        {
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

            Vector3 targetPos = headSocket.position;
            Quaternion targetRot = headSocket.rotation;

            headObj.transform.SetPositionAndRotation(
                Vector3.Lerp(startPos, targetPos, cu),
                Quaternion.Slerp(startRot, targetRot, cu)
            );

            yield return null;
        }

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
        headBoneScale.localScale = visible ? Vector3.one : Vector3.zero;
    }

    private void OnThrownHeadNetIdChanged(ulong oldId, ulong newId)
    {
        if (!IsOwner) return;

        if (newId == 0)
        {
            StopFollowAndRestoreCamera();
            return;
        }

        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(newId, out var netObj))
        {
            StartFollowHead(netObj.transform);
        }
    }

    private void StartFollowHead(Transform headTransform)
    {
        if (cameraPivot == null || headTransform == null) return;

        // save
        _cameraOriginalParent = cameraPivot.parent;
        _cameraOriginalLocalPos = cameraPivot.localPosition;
        _cameraOriginalLocalRot = cameraPivot.localRotation;

        _followTarget = headTransform;
        _isFollowing = true;
    }

    private void StopFollowAndRestoreCamera()
    {
        if (!_isFollowing) return;
        if (cameraPivot == null) return;

        _isFollowing = false;
        _followTarget = null;

        // restore
        cameraPivot.SetParent(_cameraOriginalParent, worldPositionStays: false);
        cameraPivot.localPosition = _cameraOriginalLocalPos;
        cameraPivot.localRotation = _cameraOriginalLocalRot;
    }
}
