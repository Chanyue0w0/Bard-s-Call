using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using FMODUnity;
using System.Collections;

public class FMODBeatJudge : MonoBehaviour
{
    public static FMODBeatJudge Instance { get; private set; }

    [Header("判定範圍")]
    public float earlyRange = 0.03f;
    public float lateRange = 0.07f;
    public float judgeOffset = 0.0f;

    [Header("Perfect / Miss UI")]
    public GameObject perfectEffectPrefab;
    public GameObject missTextPrefab;
    public RectTransform beatHitPointUI;

    [Header("按下縮放動畫")]
    public float scaleUpSize = 2f;
    public float normalSize = 1.12f;
    public float animTime = 0.15f;

    [Header("FMOD Perfect 音效")]
    public EventReference beatSFXEvent;

    [Header("Combo UI")]
    public Text comboText;
    public float comboResetTime = 3f;
    private int comboCount = 0;
    private float lastHitTime;

    private Coroutine scaleRoutine;
    private Coroutine comboTimerRoutine;

    // 拍點（由 Listener 更新）
    private int latestBeatIndex = -1;
    private float lastBeatTime = 0f;
    private float secPerBeat = 0.5f;

    private int lastPerfectBeatIndex = -1;

    public int LastHitBeatIndex { get; private set; }
    public double LastHitDelta { get; private set; }

    private void Awake()
    {
        if (Instance != null) Destroy(this);
        Instance = this;
    }

    private void OnEnable()
    {
        FMODBeatListener.OnBeatInfo += OnBeatUpdate;
    }

    private void OnDisable()
    {
        FMODBeatListener.OnBeatInfo -= OnBeatUpdate;
    }

    private void OnBeatUpdate(FMODBeatListener.BeatInfo i)
    {
        latestBeatIndex = i.globalBeat;
        lastBeatTime = Time.unscaledTime;
        secPerBeat = 60f / i.tempo;
    }


    // ==========================================================
    // 判定（核心）
    // ==========================================================
    public bool IsOnBeat()
    {
        if (latestBeatIndex < 0 || secPerBeat <= 0)
            return false;

        double now = Time.unscaledTime + judgeOffset;
        double dtPrev = now - lastBeatTime;
        double dtNext = now - (lastBeatTime + secPerBeat);

        double absPrev = Mathf.Abs((float)dtPrev);
        double absNext = Mathf.Abs((float)dtNext);

        int candBeat = absPrev <= absNext ? latestBeatIndex : latestBeatIndex + 1;
        double delta = absPrev <= absNext ? dtPrev : dtNext;

        if (candBeat == lastPerfectBeatIndex)
            return false;

        PlayPressScale();

        bool perfect = delta >= -earlyRange && delta <= lateRange;
        LastHitBeatIndex = candBeat;
        LastHitDelta = delta;

        if (perfect)
        {
            lastPerfectBeatIndex = candBeat;

            // 粒子特效
            SpawnPerfectEffect();

            // FMOD 音效
            RuntimeManager.PlayOneShot(beatSFXEvent);

            // 震動
            if (Gamepad.current != null)
                VibrationManager.Instance?.Vibrate("Perfect");

            FeverManager.Instance?.AddPerfect();
            RegisterBeatResult(true);
        }
        else
        {
            SpawnMissText();
            FeverManager.Instance?.AddMiss();
            RegisterBeatResult(false);
        }

        return perfect;
    }

    // ==========================================================
    // Combo
    // ==========================================================
    private void RegisterBeatResult(bool perfect)
    {
        if (perfect)
        {
            comboCount++;
            lastHitTime = Time.time;
            UpdateComboUI();

            if (comboTimerRoutine != null)
                StopCoroutine(comboTimerRoutine);

            comboTimerRoutine = StartCoroutine(ComboTimeout());
        }
        else
            ResetCombo();
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
        if (comboText != null)
            comboText.text = comboCount > 0 ? comboCount.ToString() : "";
    }


    // ==========================================================
    // UI & 特效
    // ==========================================================
    private void PlayPressScale()
    {
        if (beatHitPointUI == null) return;

        if (scaleRoutine != null)
            StopCoroutine(scaleRoutine);

        scaleRoutine = StartCoroutine(PressScaleAnim());
    }

    private IEnumerator PressScaleAnim()
    {
        Vector3 s0 = Vector3.one * normalSize;
        Vector3 s1 = Vector3.one * scaleUpSize;

        float t = 0;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / animTime;
            beatHitPointUI.localScale = Vector3.Lerp(s0, s1, t);
            yield return null;
        }

        t = 0;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / animTime;
            beatHitPointUI.localScale = Vector3.Lerp(s1, s0, t);
            yield return null;
        }
        beatHitPointUI.localScale = s0;
    }

    private void SpawnPerfectEffect()
    {
        if (perfectEffectPrefab == null || beatHitPointUI == null)
            return;

        GameObject obj = Instantiate(perfectEffectPrefab, beatHitPointUI.parent);

        // 嘗試 RectTransform
        var rt = obj.GetComponent<RectTransform>();
        if (rt != null)
            rt.anchoredPosition = beatHitPointUI.anchoredPosition;
        else
            obj.transform.localPosition = beatHitPointUI.localPosition;

        Destroy(obj, 1.2f);
    }

    private void SpawnMissText()
    {
        if (missTextPrefab == null || beatHitPointUI == null)
            return;

        GameObject obj = Instantiate(missTextPrefab, beatHitPointUI.parent);

        var rt = obj.GetComponent<RectTransform>();
        if (rt != null)
            rt.anchoredPosition = beatHitPointUI.anchoredPosition + new Vector2(0, 60f);
        else
            obj.transform.localPosition = beatHitPointUI.localPosition + new Vector3(0, 60f, 0);

        Destroy(obj, 0.3f);
    }
}
