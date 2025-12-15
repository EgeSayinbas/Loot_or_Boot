using UnityEngine;

public class Deck3D : MonoBehaviour
{
    private void OnMouseDown()
    {
        if (KempsInputController.Instance == null)
        {
            Debug.LogWarning("[Deck3D] KempsInputController.Instance yok.");
            return;
        }

        KempsInputController.Instance.OnDeckClicked();
    }
}
