using UnityEngine;
using UnityEngine.UI; // ★ 使用舊版 UI Text
using System.Collections;

public class BeatJudge : MonoBehaviour
{
    [Header("判定範圍設定 (秒)")]
    [Tooltip("允許提早多少秒內仍算完美")]
    public float earlyRange = 0.03f;

    [Tooltip("允許延遲多少秒內仍算完美")]
    public float lateRange = 0.07f;

    [Header("判定時間補償 (秒)")]
    [Tooltip("正值會讓判定提前，建議 0.03~0.12 秒之間")]
    public float judgeOffset = 0.08f;

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

    public static BeatJudge Instance { get; private set; }
    private Coroutine scaleCoroutine;

    // ============================================================
    // ★ Combo 系統變數
    // ============================================================
    [Header("Combo 顯示設定")]
    public Text comboText;              // 舊版 UI Text
    public float comboResetTime = 3f;   // 超過多久沒打擊則歸零

    private int comboCount = 0;         // 當前 Combo 數
    private float lastHitTime = 0f;     // 上次 Perfect 時間
    private Coroutine comboTimerCoroutine; // 控制歸零倒數的 Coroutine

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    // ============================================================
    // 判定核心
    // ============================================================
    private int lastPerfectBeatIndex = -1; // ★ 新增：記錄上次成功拍點

    public bool IsOnBeat()
    {
        var bm = BeatManager.Instance;
        var mm = MusicManager.Instance;

        if (bm == null || mm == null)
            return false;

        AudioSource source = mm.GetComponent<AudioSource>();
        if (source == null || source.clip == null || !source.isPlaying)
            return false;

        float frequency = source.clip.frequency;
        float beatInterval = bm.GetInterval();
        double offsetSamples = frequency * bm.startDelay;

        double currentSamples = source.timeSamples - offsetSamples + (judgeOffset * frequency);
        if (currentSamples < 0)
            return false;

        double sampledBeat = currentSamples / (frequency * beatInterval);
        double nearestBeatIndex = System.Math.Round(sampledBeat);
        double nearestBeatTime = nearestBeatIndex * beatInterval;
        double actualTime = currentSamples / frequency;
        double delta = actualTime - nearestBeatTime;

        bool perfect = (delta >= -earlyRange && delta <= lateRange);

        // ★ 新增：同一拍防重判
        int beatIndexInt = (int)nearestBeatIndex;
        if (beatIndexInt == lastPerfectBeatIndex)
        {
            // 已經在這一拍判定過了，不再觸發
            return false;
        }

        PlayScaleAnim();

        if (perfect)
        {
            lastPerfectBeatIndex = beatIndexInt; // 記錄成功拍
            SpawnPerfectEffect();

            if (audioSource != null && snapClip != null)
            {
                double playTime = AudioSettings.dspTime + 0.05;
                audioSource.clip = snapClip;
                audioSource.PlayScheduled(playTime);
            }

            Debug.Log($"[Perfect] Δt = {delta:F4}s  Beat = {nearestBeatIndex}");
            RegisterBeatResult(true);
        }
        else
        {
            SpawnMissText();
            Debug.Log($"[Miss] Δt = {delta:F4}s");
            RegisterBeatResult(false);
        }

        return perfect;
    }


    // ============================================================
    // Combo 系統邏輯
    // ============================================================
    public void RegisterBeatResult(bool isPerfect)
    {
        if (isPerfect)
        {
            comboCount++;
            lastHitTime = Time.time;
            UpdateComboUI();

            // 每次 Perfect 都重啟倒數計時
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
        comboText.text = comboCount > 0 ? "x " + comboCount.ToString() : "";
    }

    // ============================================================
    // 特效顯示
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

    // ============================================================
    // UI 動畫
    // ============================================================
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
