using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class BeatJudge : MonoBehaviour
{
    [Header("�P�w�d��]�w (��)")]
    public float earlyRange = 0.03f;
    public float lateRange = 0.07f;

    [Header("�P�w�ɶ����v (��)")]
    public float judgeOffset = 0.08f;

    [Header("�S�ĻP UI")]
    public GameObject beatHitLightUIPrefab;
    public GameObject missTextPrefab;
    public RectTransform beatHitPointUI;

    [Header("�Y��ʵe�]�w")]
    public float scaleUpSize = 2f;
    public float normalSize = 1.12f;
    public float animTime = 0.15f;

    [Header("���ĳ]�w")]
    public AudioClip snapClip;
    private AudioSource audioSource;

    public static BeatJudge Instance { get; private set; }
    private Coroutine scaleCoroutine;

    [Header("Combo ��ܳ]�w")]
    public Text comboText;
    public float comboResetTime = 3f;

    private int comboCount = 0;
    private float lastHitTime = 0f;
    private Coroutine comboTimerCoroutine;

    // ============================================================
    // �� �s�W�ܼ�
    // ============================================================
    private int lastPerfectBeatIndex = -1;
    public int LastHitBeatIndex { get; private set; } = -1;  // �� ���}���a�̫�R����
    public double LastHitDelta { get; private set; } = 0.0;  // �� �R���P���I�~�t�]��K�ոա^

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
    // �֤ߧP�w
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

        double currentSamples = source.timeSamples - offsetSamples + (judgeOffset * frequency);
        if (currentSamples < 0)
            return false;

        // ---------------------------------------------------------
        // �� �p���ګ��U�ɶ��ҹ������z�׸`�����
        // ---------------------------------------------------------
        double sampledBeat = currentSamples / (frequency * beatInterval);
        double nearestBeatIndex = System.Math.Round(sampledBeat);
        double nearestBeatTime = nearestBeatIndex * beatInterval;
        double actualTime = currentSamples / frequency;
        double delta = actualTime - nearestBeatTime;

        bool perfect = (delta >= -earlyRange && delta <= lateRange);

        // ---------------------------------------------------------
        // �� �P�稾���P
        // ---------------------------------------------------------
        int beatIndexInt = (int)nearestBeatIndex;
        if (beatIndexInt == lastPerfectBeatIndex)
            return false;

        PlayScaleAnim();

        if (perfect)
        {
            // ======================================================
            // �� ��کR����]���a���U�h���@��^
            // ======================================================
            lastPerfectBeatIndex = beatIndexInt;
            LastHitBeatIndex = beatIndexInt;   // �� ���}�X�h
            LastHitDelta = delta;

            SpawnPerfectEffect();
            if (audioSource != null && snapClip != null)
            {
                double playTime = AudioSettings.dspTime + 0.05;
                audioSource.clip = snapClip;
                audioSource.PlayScheduled(playTime);
            }

            Debug.Log($"[Perfect] ������ = {LastHitBeatIndex}  �Gt = {delta:F4}s");
            RegisterBeatResult(true);
        }
        else
        {
            SpawnMissText();
            Debug.Log($"[Miss] �Gt = {delta:F4}s");
            RegisterBeatResult(false);
        }

        return perfect;
    }

    // ============================================================
    // Combo �t��
    // ============================================================
    public void RegisterBeatResult(bool isPerfect)
    {
        if (isPerfect)
        {
            comboCount++;
            lastHitTime = Time.time;
            UpdateComboUI();

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
    // �S�ĻP�ʵe
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
