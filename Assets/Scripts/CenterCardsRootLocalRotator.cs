using UnityEngine;

public class CenterCardsRootSeatRotator : MonoBehaviour
{
    [Header("Seat -> Y Rotation (degrees)")]
    [Tooltip("Seat0, Seat1, Seat2, Seat3 için Y rotasyonlarý")]
    [SerializeField] private float[] seatYaw = new float[4] { 0f, 90f, 180f, -90f };

    [Tooltip("SeatIndex hazýr olana kadar bekler, sonra bir kez uygular.")]
    [SerializeField] private bool applyOnce = true;

    private bool _applied;

    private void LateUpdate()
    {
        if (applyOnce && _applied) return;

        var local = NetworkPlayer.Local;
        if (local == null) return;

        int seat = local.SeatIndex.Value;
        if (seat < 0 || seat >= 4) return;

        float yaw = seatYaw[seat];
        transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

        _applied = true;
    }
}
