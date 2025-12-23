using UnityEngine;

public class GameplayOverlayBlocker : MonoBehaviour
{
    [Header("Panels that block gameplay (any active -> block)")]
    [SerializeField] private GameObject endLosePanel;
    [SerializeField] private GameObject endWinPanel;
    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject losePanel;

    [Header("Gameplay HUD roots to toggle")]
    [SerializeField] private GameObject bottomLeft_Pass;
    [SerializeField] private GameObject bottomRight_Actions;   // Btn_Kemps + Btn_Unkemps
    [SerializeField] private GameObject topCenter_Info;        // scoreboard / round text vb


    [Header("Input Controllers to disable (optional)")]
    [SerializeField] private Behaviour[] inputBehavioursToDisable; // KempsInputController, click scripts, vs.

    private bool _lastBlocked;

    private void Awake()
    {
        Apply(IsAnyBlockingPanelOpen());
    }

    private void Update()
    {
        bool blocked = IsAnyBlockingPanelOpen();
        if (blocked == _lastBlocked) return;

        Apply(blocked);
    }

    private bool IsAnyBlockingPanelOpen()
    {
        return (endLosePanel != null && endLosePanel.activeInHierarchy)
            || (endWinPanel != null && endWinPanel.activeInHierarchy)
            || (winPanel != null && winPanel.activeInHierarchy)
            || (losePanel != null && losePanel.activeInHierarchy);
    }

    private void Apply(bool blocked)
    {
        _lastBlocked = blocked;

        // HUD toggle
        SetActiveSafe(bottomLeft_Pass, !blocked);
        SetActiveSafe(bottomRight_Actions, !blocked);
        SetActiveSafe(topCenter_Info, !blocked);


        // Input disable (playerlar arkada oynamasýn)
        if (inputBehavioursToDisable != null)
        {
            for (int i = 0; i < inputBehavioursToDisable.Length; i++)
            {
                if (inputBehavioursToDisable[i] == null) continue;
                inputBehavioursToDisable[i].enabled = !blocked;
            }
        }

        // Ýstersen mouse unlock vs burada da yaparsýn:
        // if (blocked) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        // else { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    }

    private void SetActiveSafe(GameObject go, bool active)
    {
        if (go == null) return;
        if (go.activeSelf == active) return;
        go.SetActive(active);
    }
}
