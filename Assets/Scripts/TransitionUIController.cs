using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TransitionUIController : MonoBehaviour
{
    public static TransitionUIController Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private Slider loadingBar;
    [SerializeField] private GameObject panelRoot;

    [Header("Fake Fill")]
    [SerializeField] private float fakeFillDuration = 10f; // Lobby’de 10sn hissi
    [SerializeField] private float minFill = 0.05f;
    [SerializeField] private float maxFillBeforeReady = 0.9f;

    [Header("Auto Close On Game Scene")]
    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private float holdAfterGameSceneLoaded = 5f;

    private Coroutine _fakeRoutine;
    private Coroutine _fillAndHideRoutine;
    private float _value;
    private bool _isShowing;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (rootCanvas == null) rootCanvas = GetComponentInChildren<Canvas>(true);

        SceneManager.sceneLoaded += OnSceneLoaded;

        HideImmediate();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Hook ölse bile: Game sahnesine girince otomatik finalize + 5sn bekle + kapa
        if (!_isShowing) return;

        if (scene.name == gameSceneName)
        {
            MarkSceneLoadedAndHold(holdAfterGameSceneLoaded);
        }
    }

    public void Show()
    {
        if (_isShowing) return;
        _isShowing = true;

        if (panelRoot != null) panelRoot.SetActive(true);
        if (rootCanvas != null) rootCanvas.enabled = true;

        SetProgress(minFill);

        StopFake();
        StopFillAndHide();

        // 10 sn boyunca 0.9’a doðru “fake” dolsun
        _fakeRoutine = StartCoroutine(Co_FakeFillTo(maxFillBeforeReady, fakeFillDuration));
    }

    public void MarkSceneLoadedAndHold(float extraSecondsAfterLoad = 5f)
    {
        if (!_isShowing) return;

        StopFake();
        StopFillAndHide();
        _fillAndHideRoutine = StartCoroutine(Co_FillAndHide(extraSecondsAfterLoad));
    }

    public void HideImmediate()
    {
        StopFake();
        StopFillAndHide();

        _isShowing = false;

        if (panelRoot != null) panelRoot.SetActive(false);
        if (rootCanvas != null) rootCanvas.enabled = false;

        SetProgress(0f);
    }

    private void StopFake()
    {
        if (_fakeRoutine != null)
        {
            StopCoroutine(_fakeRoutine);
            _fakeRoutine = null;
        }
    }

    private void StopFillAndHide()
    {
        if (_fillAndHideRoutine != null)
        {
            StopCoroutine(_fillAndHideRoutine);
            _fillAndHideRoutine = null;
        }
    }

    private IEnumerator Co_FakeFillTo(float target, float duration)
    {
        float start = _value;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / Mathf.Max(0.0001f, duration));
            float v = Mathf.Lerp(start, target, u);
            SetProgress(v);
            yield return null;
        }

        SetProgress(target);
    }

    private IEnumerator Co_FillAndHide(float holdSeconds)
    {
        // hýzlý 1.0'a tamamla
        float start = _value;
        float t = 0f;
        float dur = 0.35f;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            SetProgress(Mathf.Lerp(start, 1f, u));
            yield return null;
        }

        SetProgress(1f);

        // Game scene açýldýktan sonra 5sn daha kalsýn
        float wait = Mathf.Max(0f, holdSeconds);
        float w = 0f;
        while (w < wait)
        {
            w += Time.unscaledDeltaTime;
            yield return null;
        }

        HideImmediate();
    }

    private void SetProgress(float v)
    {
        _value = Mathf.Clamp01(v);
        if (loadingBar != null) loadingBar.value = _value;
    }
}
