using UnityEngine;

public class Card3D : MonoBehaviour
{
    public CardView View { get; private set; }

    private void Awake()
    {
        View = GetComponent<CardView>();
    }

    private void OnMouseDown()
    {
        // Multiplayer Play Mode’da her pencerede ayrý týk çalýþýr.
        if (KempsInputController.Instance == null)
        {
            Debug.LogWarning("[Card3D] KempsInputController.Instance yok, týklama iþlenemedi.");
            return;
        }

        KempsInputController.Instance.OnCardClicked(this);
    }
}
