using UnityEngine;
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
    // 判定核心（使用 MusicManager 的 AudioSource）
    // ============================================================
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

        // 當前取樣時間（含判定補償）
        double currentSamples = source.timeSamples - offsetSamples - (judgeOffset * frequency);
        if (currentSamples < 0)
            return false;

        // 計算目前應在第幾拍
        double sampledBeat = currentSamples / (frequency * beatInterval);
        double nearestBeatIndex = System.Math.Round(sampledBeat);
        double nearestBeatTime = nearestBeatIndex * beatInterval;

        // 計算與拍點的時間差（秒）
        double actualTime = currentSamples / frequency;
        double delta = actualTime - nearestBeatTime; // 正：延遲，負：提前

        bool perfect = (delta >= -earlyRange && delta <= lateRange);

        // UI 動畫
        PlayScaleAnim();

        if (perfect)
        {
            SpawnPerfectEffect();

            if (audioSource != null && snapClip != null)
            {
                double playTime = AudioSettings.dspTime + 0.05; // 提前排程 50ms
                audioSource.clip = snapClip;
                audioSource.PlayScheduled(playTime);
            }

            Debug.Log($"[Perfect] Δt = {delta:F4}s  Beat = {nearestBeatIndex}");
        }
        else
        {
            SpawnMissText();
            Debug.Log($"[Miss] Δt = {delta:F4}s");
        }

        return perfect;
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
