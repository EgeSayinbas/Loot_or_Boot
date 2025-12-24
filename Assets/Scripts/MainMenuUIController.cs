using UnityEngine;
using UnityEngine.UI;

public class MainMenuUIController : MonoBehaviour
{
    [Header("Mode Toggle (OFF=LAN, ON=STEAM)")]
    [SerializeField] private Toggle modeToggle;

    [Header("Panels")]
    [SerializeField] private GameObject lanPanel;
    [SerializeField] private GameObject steamPanel;

    [Header("Defaults")]
    [SerializeField] private bool defaultSteamMode = false;

    private void Awake()
    {
        if (modeToggle != null)
        {
            modeToggle.onValueChanged.RemoveListener(SetSteamMode);
            modeToggle.onValueChanged.AddListener(SetSteamMode);
            modeToggle.isOn = defaultSteamMode;
        }

        SetSteamMode(modeToggle != null ? modeToggle.isOn : defaultSteamMode);
    }

    public void SetSteamMode(bool steamOn)
    {
        if (lanPanel != null) lanPanel.SetActive(!steamOn);
        if (steamPanel != null) steamPanel.SetActive(steamOn);

        // Steam panel açýlýnca auto refresh
        if (steamOn && steamPanel != null)
        {
            var steamUi = steamPanel.GetComponentInChildren<SteamLobbyUI>(true);
            if (steamUi != null)
                steamUi.AutoRefreshIfPossible();
        }
    }
}
