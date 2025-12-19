using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class HeadProjectileSetup : MonoBehaviour
{
    [Header("Spin (optional)")]
    [SerializeField] private float spinTorque = 2f;

    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        if (_rb != null)
        {
            _rb.AddTorque(Random.onUnitSphere * spinTorque, ForceMode.VelocityChange);
        }
    }
}
