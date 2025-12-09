using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Sað mouse tuþuna basýlýyken kamerayý ve karakterin kafasýný çevirir.
/// Yalnýzca local owner input alýr. Yaw ve pitch deðerleri NetworkVariable ile
/// diðer client'lara senkronlanýr.
/// </summary>
public class HeadLookController : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform bodyRoot;   // Y ekseninde dönecek olan (genelde Player root)
    [SerializeField] private Transform headPivot;  // Kamera/kafa pivotu (HeadPivot)

    [Header("Ayarlar")]
    [SerializeField] private float sensitivity = 120f;
    [SerializeField] private float minPitch = -50f;
    [SerializeField] private float maxPitch = 50f;

    private float yaw;
    private float pitch;

    // Owner yazar, herkes okur
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

        if (bodyRoot == null)
            bodyRoot = transform;

        if (headPivot == null)
        {
            var np = GetComponent<NetworkPlayer>();
            if (np != null)
                headPivot = np.HeadPivot;
        }

        // Baþlangýç yaw/pitch deðerleri
        if (bodyRoot != null)
            yaw = bodyRoot.eulerAngles.y;

        if (headPivot != null)
        {
            float x = headPivot.localEulerAngles.x;
            if (x > 180f) x -= 360f;
            pitch = x;
        }

        // Net deðerleri baþlangýçla eþitle
        if (IsOwner)
        {
            netYaw.Value = yaw;
            netPitch.Value = pitch;
        }

        netYaw.OnValueChanged += OnNetYawChanged;
        netPitch.OnValueChanged += OnNetPitchChanged;
    }

    private void OnDestroyCstm()
    {
        netYaw.OnValueChanged -= OnNetYawChanged;
        netPitch.OnValueChanged -= OnNetPitchChanged;
    }

    private void Update()
    {
        // Sadece kendi oyuncumuz input almalý
        if (!IsOwner)
            return;

        // Sað mouse basýlýyken bakýþ ve kafa çevir
        if (Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            yaw += mouseX * sensitivity * Time.deltaTime;
            pitch -= mouseY * sensitivity * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            // Localde uygula
            ApplyYawPitchLocal(yaw, pitch);

            // Net deðiþkenlere yaz -> server üzerinden diðer client'lara gider
            netYaw.Value = yaw;
            netPitch.Value = pitch;
        }
    }

    // ---- Network callback'leri ----

    private void OnNetYawChanged(float oldValue, float newValue)
    {
        // Owner zaten kendisi ayarlýyor; sadece diðerlerinde uygula
        if (IsOwner) return;

        yaw = newValue;
        ApplyYawPitchLocal(yaw, pitch);
    }

    private void OnNetPitchChanged(float oldValue, float newValue)
    {
        if (IsOwner) return;

        pitch = newValue;
        ApplyYawPitchLocal(yaw, pitch);
    }

    // ---- Ortak uygulama fonksiyonu ----

    private void ApplyYawPitchLocal(float y, float p)
    {
        if (bodyRoot != null)
            bodyRoot.rotation = Quaternion.Euler(0f, y, 0f);

        if (headPivot != null)
        {
            Vector3 e = headPivot.localEulerAngles;
            e.x = p;
            headPivot.localEulerAngles = e;
        }
    }
}
