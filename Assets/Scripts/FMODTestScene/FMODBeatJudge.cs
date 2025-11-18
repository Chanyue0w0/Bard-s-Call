using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;

public class FMODBeatJudge : MonoBehaviour
{
    [Header("判定範圍設定 (秒)")]
    public float earlyRange = 0.03f;   // 提前可接受範圍
    public float lateRange = 0.07f;    // 延遲可接受範圍

    [Header("判定時間補償 (秒)")]
    [Tooltip("正值代表實際判定時間會往後平移，例如補償人類反應 / 延遲")]
    public float judgeOffset = 0.0f;

    [Header("特效與 UI")]
    public GameObject beatHitLightUIPrefab;
    public GameObject missTextPrefab;
    public RectTransform beatHitPointUI;

    [Header("縮放動畫設定")]
    public float scaleUpSize = 2f;
    public float normalSize = 1.12f;
    public float animTime = 0.15f;

    [Header("音效設定")]
    public AudioClip snapClip;
    private AudioSource audioSource;

    public static FMODBeatJudge Instance { get; private set; }
    private Coroutine scaleCoroutine;

    [Header("Combo 顯示設定")]
    public Text comboText;
    public float comboResetTime = 3f;

    private int comboCount = 0;
    private float lastHitTime = 0f;
    private Coroutine comboTimerCoroutine;

    // ============================================================
    // FMOD 對拍相關狀態
    // ============================================================

    // 由 FMODBeatListener 推進
    private int latestGlobalBeatIndex = -1;
    private float lastBeatUnityTime = 0f;     // 最近一次拍點在 Unity 的時間（Time.unscaledTime）
    private float secondsPerBeat = 0.5f;      // 每拍秒數 (60 / BPM)

    // 防止同一拍被判多次 Perfect
    private int lastPerfectBeatIndex = -1;

    // 對外方便除錯用
    public int LastHitBeatIndex { get; private set; } = -1;
    public double LastHitDelta { get; private set; } = 0.0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        // 監聽 FMODBeatListener 的拍點事件
        if (FMODBeatListener.Instance != null)
        {
            FMODBeatListener.OnBeatInfo += OnBeatInfoReceived;
        }
    }

    private void OnDisable()
    {
        if (FMODBeatListener.Instance != null)
        {
            FMODBeatListener.OnBeatInfo -= OnBeatInfoReceived;
        }
    }

    private void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    // ============================================================
    // 從 FMODBeatListener 收拍點
    // ============================================================

    private void OnBeatInfoReceived(FMODBeatListener.BeatInfo info)
    {
        // 記錄全局拍 index
        latestGlobalBeatIndex = info.globalBeat;
        // 記錄該拍發生時的 Unity 時間（用 unscaledTime 避免 Time.timeScale 影響）
        lastBeatUnityTime = Time.unscaledTime;
        // 每拍秒數（由 FMOD 回報 BPM）
        secondsPerBeat = (info.tempo > 0f) ? 60f / info.tempo : 0f;
    }

    // ============================================================
    // 核心判定：是否對拍
    // ============================================================
    public bool IsOnBeat()
    {
        var listener = FMODBeatListener.Instance;
        if (listener == null)
            return false;

        if (secondsPerBeat <= 0f || latestGlobalBeatIndex < 0)
            return false;

        // 以 unscaledTime 為主，避免遊戲暫停影響判定
        double now = Time.unscaledTime + judgeOffset;

        // 現在距「最近一拍」過了多久
        double dtSinceLastBeat = now - lastBeatUnityTime;

        // 預測下一拍的時間
        double nextBeatTime = lastBeatUnityTime + secondsPerBeat;
        double dtToNextBeat = now - nextBeatTime;

        // 找出「距離最近的那一拍」
        double absLast = System.Math.Abs(dtSinceLastBeat);
        double absNext = System.Math.Abs(dtToNextBeat);

        int candidateBeatIndex;
        double delta;

        if (absLast <= absNext)
        {
            // 靠近上一拍
            candidateBeatIndex = latestGlobalBeatIndex;
            delta = dtSinceLastBeat;
        }
        else
        {
            // 靠近下一拍（提前敲擊）
            candidateBeatIndex = latestGlobalBeatIndex + 1;
            delta = dtToNextBeat;
        }

        // 若這一拍已經判過 Perfect，就不再判
        if (candidateBeatIndex == lastPerfectBeatIndex)
            return false;

        // 播 UI 縮放（不管有沒有判到 Perfect）
        PlayScaleAnim();

        bool perfect = (delta >= -earlyRange && delta <= lateRange);

        if (perfect)
        {
            lastPerfectBeatIndex = candidateBeatIndex;
            LastHitBeatIndex = candidateBeatIndex;
            LastHitDelta = delta;

            SpawnPerfectEffect();

            if (audioSource != null && snapClip != null)
            {
                // 稍微往後排程一點點，避免爆音
                double playTime = AudioSettings.dspTime + 0.05;
                audioSource.clip = snapClip;
                audioSource.PlayScheduled(playTime);
            }

            // 搖桿震動
            if (Gamepad.current != null && VibrationManager.Instance != null)
            {
                VibrationManager.Instance.Vibrate("Perfect");
            }

            // Fever 累積
            if (FeverManager.Instance != null)
            {
                FeverManager.Instance.AddPerfect();
            }

            Debug.Log($"[FMOD Perfect] 打擊拍 = {LastHitBeatIndex}  Δt = {delta:F4}s");
            RegisterBeatResult(true);
        }
        else
        {
            SpawnMissText();

            if (FeverManager.Instance != null)
            {
                FeverManager.Instance.AddMiss();
            }

            Debug.Log($"[FMOD Miss] Δt = {delta:F4}s");
            RegisterBeatResult(false);
        }

        return perfect;
    }

    // ============================================================
    // Combo 系統（沿用舊 BeatJudge 邏輯）
    // ============================================================
    public void RegisterBeatResult(bool isPerfect)
    {
        if (isPerfect)
        {
            comboCount++;
            lastHitTime = Time.time;
            UpdateComboUI();

            // 紀錄最大連擊
            if (comboCount > GlobalIndex.MaxCombo)
                GlobalIndex.MaxCombo = comboCount;

            if (comboTimerCoroutine != null)
                StopCoroutine(comboTimerCoroutine);
            comboTimerCoroutine = StartCoroutine(ComboTimeout());
        }
        else
        {
            ResetCombo();
        }
    }

    private IEnumerator ComboTimeout()
    {
        yield return new WaitForSeconds(comboResetTime);
        if (Time.time - lastHitTime >= comboResetTime)
            ResetCombo();
    }

    private void ResetCombo()
    {
        comboCount = 0;
        UpdateComboUI();
    }

    private void UpdateComboUI()
    {
        if (comboText == null) return;
        comboText.text = comboCount > 0 ? comboCount.ToString() : "";
    }

    // ============================================================
    // 特效與動畫（沿用舊 BeatJudge）
    // ============================================================
    private void SpawnPerfectEffect()
    {
        if (beatHitLightUIPrefab == null || beatHitPointUI == null) return;

        GameObject effect = Instantiate(beatHitLightUIPrefab, beatHitPointUI.parent);
        RectTransform rect = effect.GetComponent<RectTransform>();
        if (rect != null)
            rect.anchoredPosition = beatHitPointUI.anchoredPosition;

        Destroy(effect, 0.5f);
    }

    private void SpawnMissText()
    {
        if (missTextPrefab == null || beatHitPointUI == null) return;

        GameObject missObj = Instantiate(missTextPrefab, beatHitPointUI.parent);
        RectTransform rect = missObj.GetComponent<RectTransform>();
        if (rect != null)
            rect.anchoredPosition = beatHitPointUI.anchoredPosition + new Vector2(0, 50f);

        Destroy(missObj, 0.3f);
    }

    private void PlayScaleAnim()
    {
        if (beatHitPointUI == null) return;

        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);

        scaleCoroutine = StartCoroutine(ScaleAnim());
    }

    private IEnumerator ScaleAnim()
    {
        Vector3 start = Vector3.one * normalSize;
        Vector3 up = Vector3.one * scaleUpSize;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / animTime;
            beatHitPointUI.localScale = Vector3.Lerp(start, up, t);
            yield return null;
        }

        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / animTime;
            beatHitPointUI.localScale = Vector3.Lerp(up, start, t);
            yield return null;
        }

        beatHitPointUI.localScale = start;
        scaleCoroutine = null;
    }
}
