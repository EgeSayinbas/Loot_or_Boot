using System.Collections;
using TMPro;
using UnityEngine;

public class KempsHUD : MonoBehaviour
{
    public static KempsHUD Instance { get; private set; }

    [Header("Top Center Info")]
    [SerializeField] private TMP_Text txtRoundInfo;

    [Header("Round End Overlay")]
    [SerializeField] private GameObject centerOverlay;
    [SerializeField] private TMP_Text txtCountDown;

    [Header("Round Result Panels (per round)")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject losePanel;

    [Header("GAME OVER Panels (match end)")]
    [SerializeField] private GameObject endWinPanel;
    [SerializeField] private GameObject endLosePanel;

    [SerializeField] private TMP_Text txtEndScore_Win;
    [SerializeField] private TMP_Text txtEndScore_Lose;

    [SerializeField] private TMP_Text txtEndWin;
    [SerializeField] private TMP_Text txtEndLose;

    private Coroutine _roundEndCo;
    private bool _gameOverShown;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        HideAllPanelsImmediate();
        RefreshTopInfo();
        StartCoroutine(BindToGameManager());
    }

    private IEnumerator BindToGameManager()
    {
        while (KempsGameManager.Instance == null)
            yield return null;

        var gm = KempsGameManager.Instance;

        gm.Team0Score.OnValueChanged += (_, __) => RefreshTopInfo();
        gm.Team1Score.OnValueChanged += (_, __) => RefreshTopInfo();
        gm.RoundIndex.OnValueChanged += (_, __) => RefreshTopInfo();

        RefreshTopInfo();
    }

    public void RefreshTopInfo()
    {
        var gm = KempsGameManager.Instance;
        if (gm == null || txtRoundInfo == null) return;

        // team0=Red, team1=Blue
        int round = gm.RoundIndex.Value;
        int red = gm.Team0Score.Value;
        int blue = gm.Team1Score.Value;

        // İstersen Target'ı da yazdırabilirsin:
        int target = gm.TargetScore.Value;

        txtRoundInfo.text = $"Loot {round}\nCaptains {red} : Crew {blue}\n(Throphy {target})";
    }

    // ===== ROUND END (3..2..1) =====
    public void PlayRoundEndSequence(int winnerTeam)
    {
        if (_gameOverShown) return;

        if (_roundEndCo != null) StopCoroutine(_roundEndCo);
        _roundEndCo = StartCoroutine(RoundEndRoutine(winnerTeam));
    }

    private IEnumerator RoundEndRoutine(int winnerTeam)
    {
        HideRoundPanelsImmediate();

        int myTeam = GetMyTeamSafe();
        bool iWon = (myTeam != -1 && myTeam == winnerTeam);

        if (winPanel != null) winPanel.SetActive(iWon);
        if (losePanel != null) losePanel.SetActive(!iWon);

        if (centerOverlay != null) centerOverlay.SetActive(true);

        for (int t = 3; t >= 1; t--)
        {
            if (txtCountDown != null) txtCountDown.text = t.ToString();
            yield return new WaitForSeconds(1f);
        }

        HideRoundPanelsImmediate();
        RefreshTopInfo();
        _roundEndCo = null;
    }

    private void HideRoundPanelsImmediate()
    {
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
        if (centerOverlay != null) centerOverlay.SetActive(false);
        if (txtCountDown != null) txtCountDown.text = "";
    }

    // ===== GAME OVER (End panels) =====
    public void ShowGameOver(int winnerTeam, int team0Score, int team1Score)
    {
        _gameOverShown = true;

        if (_roundEndCo != null)
        {
            StopCoroutine(_roundEndCo);
            _roundEndCo = null;
        }

        HideAllPanelsImmediate();
        

        int myTeam = GetMyTeamSafe();
        bool iWon = (myTeam != -1 && myTeam == winnerTeam);

        if (endWinPanel != null) endWinPanel.SetActive(iWon);
        if (endLosePanel != null) endLosePanel.SetActive(!iWon);

        string scoreLine = $"Captains {team0Score} : Crew {team1Score} ";
        if (txtEndScore_Win != null) txtEndScore_Win.text = scoreLine;
        if (txtEndScore_Lose != null) txtEndScore_Lose.text = scoreLine;

        if (txtEndWin != null && iWon) txtEndWin.text = "YOU WIN!";
        if (txtEndLose != null && !iWon) txtEndLose.text = "YOU LOSE!";
    }

    public void ResetForPlayAgain()
    {
        _gameOverShown = false;
        HideAllPanelsImmediate();
        RefreshTopInfo();
    }

    private void HideAllPanelsImmediate()
    {
        HideRoundPanelsImmediate();
        if (endWinPanel != null) endWinPanel.SetActive(false);
        if (endLosePanel != null) endLosePanel.SetActive(false);
    }

    private int GetMyTeamSafe()
    {
        var local = NetworkPlayer.Local;
        if (local == null) return -1;

        int seat = local.SeatIndex.Value;
        if (seat < 0) return -1;

        return seat % 2; // team0 red, team1 blue
    }
}
