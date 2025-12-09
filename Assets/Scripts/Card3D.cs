using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Card3D : MonoBehaviour
{
    public CardView View { get; private set; }

    private void Awake()
    {
        View = GetComponent<CardView>();
        if (View == null)
        {
            Debug.LogWarning("[Card3D] CardView bulunamadý.");
        }
    }

    private void OnMouseDown()
    {
        if (!Application.isPlaying)
            return;

        if (KempsInputController.Instance == null)
        {
            Debug.LogWarning("[Card3D] KempsInputController.Instance yok, týklama iþlenemedi.");
            return;
        }

        KempsInputController.Instance.OnCardClicked(this);
    }
}
