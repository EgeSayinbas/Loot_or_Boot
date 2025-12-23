using UnityEngine;

public class TutorialMenuController : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject tutorialMenuRoot; // TutorialMenu (parent)

    [Header("Panels")]
    [SerializeField] private GameObject panel0; // Tutorial_Panel0
    [SerializeField] private GameObject panel1; // Tutorial_Panel1
    [SerializeField] private GameObject panel2; // Tutorial_Panel2

    private int _currentIndex = -1;

    private void Awake()
    {
        // Güvenli baþlangýç: kapalý kalsýn
        HideAll();
        if (tutorialMenuRoot != null) tutorialMenuRoot.SetActive(false);
    }

    // ====== MAIN MENU BUTTON ======
    // Btn_Tutorial OnClick -> ToggleTutorial()
    public void ToggleTutorial()
    {
        if (tutorialMenuRoot == null) return;

        bool willShow = !tutorialMenuRoot.activeSelf;
        tutorialMenuRoot.SetActive(willShow);

        if (willShow)
        {
            ShowPanel(0); // her açýlýþta panel0
        }
        else
        {
            // kapanýrken panel fark etmez
            HideAll();
            _currentIndex = -1;
        }
    }

    // ====== PANEL BUTTONS ======
    // Panel0 Next OnClick -> NextFrom0()
    public void NextFrom0() => ShowPanel(1);

    // Panel1 Prev/Next OnClick -> PrevFrom1(), NextFrom1()
    public void PrevFrom1() => ShowPanel(0);
    public void NextFrom1() => ShowPanel(2);

    // Panel2 Prev OnClick -> PrevFrom2()
    public void PrevFrom2() => ShowPanel(1);

    // ====== INTERNAL ======
    private void ShowPanel(int index)
    {
        if (tutorialMenuRoot == null) return;

        // Menü kapalýysa yanlýþlýkla panel açýlmasýn
        if (!tutorialMenuRoot.activeSelf)
            tutorialMenuRoot.SetActive(true);

        HideAll();

        switch (index)
        {
            case 0:
                if (panel0 != null) panel0.SetActive(true);
                _currentIndex = 0;
                break;
            case 1:
                if (panel1 != null) panel1.SetActive(true);
                _currentIndex = 1;
                break;
            case 2:
                if (panel2 != null) panel2.SetActive(true);
                _currentIndex = 2;
                break;
        }
    }

    private void HideAll()
    {
        if (panel0 != null) panel0.SetActive(false);
        if (panel1 != null) panel1.SetActive(false);
        if (panel2 != null) panel2.SetActive(false);
    }
}
