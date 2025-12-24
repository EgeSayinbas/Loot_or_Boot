using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuModeUI : MonoBehaviour
{
    [SerializeField] private Toggle modeToggle;
    [SerializeField] private TMP_Text label; // "LAN / STEAM"

    private void Awake()
    {
        if (modeToggle != null)
        {
            modeToggle.onValueChanged.RemoveListener(OnChanged);
            modeToggle.onValueChanged.AddListener(OnChanged);
            OnChanged(modeToggle.isOn);
        }
    }

    private void OnChanged(bool steamOn)
    {
        if (label == null) return;
        label.text = steamOn ? "LAN / STEAM  (STEAM)" : "LAN / STEAM  (LAN)";
    }
}
