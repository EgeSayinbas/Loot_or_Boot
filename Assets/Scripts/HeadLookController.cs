using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Sağ mouse basılıyken:
/// - Owner kendi kamerayı döndürür (yaw=Y, pitch=X)
/// - HeadAim'i senin rig eksenine göre döndürür:
///     yaw  -> -X
///     pitch-> +Z  (ama kamera ile aynı yön için pitch işareti terslenir)
/// Gövde SABİT.
/// Diğer client'lar sadece HeadAim rotasyonunu görür (kamera zaten kapalı).
/// </summary>
public class HeadLookController : NetworkBehaviour
{
    [Header("Refs (Hierarchy'ne uygun)")]
    [SerializeField] private Transform cameraPivot; // CameraPivot
    [SerializeField] private Transform headAim;     // HeadAim

    [Header("Settings")]
    [SerializeField] private float sensitivity = 120f;
    [SerializeField] private float minPitch = -50f;
    [SerializeField] private float maxPitch = 50f;

    // Camera angles
    private float yaw;   // around Y
    private float pitch; // around X

    // Rig default'unu korumak için
    private Quaternion headAimBaseLocalRot;

    // Network (Owner yazar, herkes okur)
    private NetworkVariable<float> netYaw = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private NetworkVariable<float> netPitch = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Referansları NetworkPlayer'dan çek (senin düzenine uygun)
        if (cameraPivot == null || headAim == null)
        {
            var np = GetComponent<NetworkPlayer>();
            if (np != null)
            {
                if (cameraPivot == null) cameraPivot = np.CameraPivot;
                if (headAim == null) headAim = np.HeadAim;
            }
        }

        // Son çare: isimle bul
        if (cameraPivot == null)
            cameraPivot = transform.Find("CameraPivot");

        if (headAim == null)
            headAim = FindDeepChild(transform, "HeadAim");

        if (cameraPivot == null)
            Debug.LogError("[HeadLookController] CameraPivot bulunamadı! Inspector’da atayın.");

        if (headAim == null)
            Debug.LogError("[HeadLookController] HeadAim bulunamadı! Inspector’da atayın.");

        if (headAim != null)
            headAimBaseLocalRot = headAim.localRotation;

        // Başlangıç açılarını cameraPivot'tan al (LOCAL üzerinden)
        if (cameraPivot != null)
        {
            yaw = cameraPivot.localEulerAngles.y;

            float x = cameraPivot.localEulerAngles.x;
            if (x > 180f) x -= 360f;
            pitch = Mathf.Clamp(x, minPitch, maxPitch);
        }

        if (IsOwner)
        {
            netYaw.Value = yaw;
            netPitch.Value = pitch;

            ApplyCameraLocal(yaw, pitch);
            ApplyHeadAimLocal(yaw, pitch);
        }
        else
        {
            // Remote: sadece headAim uygula (ilk spawn)
            ApplyHeadAimLocal(netYaw.Value, netPitch.Value);
        }

        // IMPORTANT FIX: Remote tarafta apply ederken diğer ekseni DAİMA NetworkVariable'dan oku
        netYaw.OnValueChanged += (_, v) =>
        {
            if (IsOwner) return;
            yaw = v;
            ApplyHeadAimLocal(yaw, netPitch.Value);
        };

        netPitch.OnValueChanged += (_, v) =>
        {
            if (IsOwner) return;
            pitch = v;
            ApplyHeadAimLocal(netYaw.Value, pitch);
        };
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (cameraPivot == null || headAim == null) return;

        // SADECE sağ mouse basılıyken
        if (!Input.GetMouseButton(1))
            return;

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        yaw += mouseX * sensitivity * Time.deltaTime;
        pitch -= mouseY * sensitivity * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // Local uygula
        ApplyCameraLocal(yaw, pitch);
        ApplyHeadAimLocal(yaw, pitch);

        // Network'e yaz
        netYaw.Value = yaw;
        netPitch.Value = pitch;
    }

    private void ApplyCameraLocal(float y, float p)
    {
        // FIX: Dünya rotation yerine LOCAL rotation (pivot child ise en doğru davranış)
        cameraPivot.localRotation = Quaternion.Euler(p, y, 0f);
    }

    private void ApplyHeadAimLocal(float y, float p)
    {
        if (headAim == null) return;

        // Senin rig mapping'in:
        //  - left/right -> HeadAim -X (yaw)
        //  - up/down    -> HeadAim +Z (pitch)
        //
        // "kamera aşağı dönerken kafa yukarı" problemi => pitch işareti ters.
        //      pitchZ = -p
        Quaternion yawRot = Quaternion.AngleAxis(-y, Vector3.right);       // -X
        Quaternion pitchRot = Quaternion.AngleAxis(-p, Vector3.forward);   // +Z ekseninde ama işaret ters (FIX)

        // Sıra: önce yaw sonra pitch
        headAim.localRotation = headAimBaseLocalRot * yawRot * pitchRot;
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
}
