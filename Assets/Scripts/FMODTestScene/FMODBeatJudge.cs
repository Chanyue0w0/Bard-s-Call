using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using FMODUnity;
using System.Collections;
using System;

public class FMODBeatJudge : MonoBehaviour
{
    public static FMODBeatJudge Instance { get; private set; }

    [Header("判定範圍設定（建議 0.10f）")]
    public float earlyRange = 0.10f;   // 建議改成 100ms，輕度節奏 RPG

    [Header("System Offset（若覺得拍子晚，可設為 -0.03f）")]
    public float judgeOffset = 0.0f;

    [Header("Auto System Offset（自動學習Offset功能）")]
    public bool autoCalibrateOffset = false;

    [Header("Perfect / Miss UI")]
    public GameObject perfectEffectPrefab;
    public GameObject missTextPrefab;
    public RectTransform beatHitPointUI;

    [Header("按下縮放動畫")]
    public float scaleUpSize = 2f;
    public float normalSize = 1.12f;
    public float animTime = 0.15f;

    [Header("Perfect 音效（FMOD）")]
    public EventReference beatSFXEvent;

    [Header("Combo UI")]
    public Text comboText;
    public float comboResetTime = 3f;
    private int comboCount = 0;
    private float lastHitTime;

    // Debug：每拍自動判 Perfect
    [Header("Debug 模式")]
    public bool autoPerfectEveryBeat = false;

    private Coroutine scaleRoutine;
    private Coroutine comboTimerRoutine;

    // 拍點（由 Listener 推送）
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

    private void OnBeatUpdate(FMODBeatListener.BeatInfo info)
    {
        latestBeatIndex = info.globalBeat;
        lastBeatTime = Time.unscaledTime;
        secPerBeat = 60f / info.tempo;
    }

    // ==========================================================
    // Debug：Listener 直接叫 Judge 自動 Perfect
    // ==========================================================
    public void ForcePerfectFromListener(int beatIndex)
    {
        lastPerfectBeatIndex = beatIndex;
        LastHitBeatIndex = beatIndex;
        LastHitDelta = 0;

        SpawnPerfectEffect();
        RuntimeManager.PlayOneShot(beatSFXEvent);

        if (Gamepad.current != null)
            VibrationManager.Instance?.Vibrate("Perfect");

        FeverManager.Instance?.AddPerfect();
        RegisterBeatResult(true);

        Debug.Log($"[Debug Perfect] Beat {beatIndex} 自動 Perfect");
    }

    // ==========================================================
    // 玩家輸入判定（核心）
    // ==========================================================
    // ==========================================================
    // 玩家輸入判定（核心）
    // ==========================================================
    public bool IsOnBeat()
    {
        if (latestBeatIndex < 0 || secPerBeat <= 0)
            return false;

        double now = Time.unscaledTime + judgeOffset;

        // ================================
        // 1. 推估 nearest beat index
        // ================================
        double beatOffset = (now - lastBeatTime) / secPerBeat;
        int nearestBeatIndex = latestBeatIndex + Mathf.RoundToInt((float)beatOffset);
        double nearestBeatTime = lastBeatTime + Mathf.RoundToInt((float)beatOffset) * secPerBeat;

        // ================================
        // 2. 計算落差（毫秒）
        // ================================
        double delta = now - nearestBeatTime;
        Debug.Log($"[BeatJudge] Δ = {delta * 1000:0.0} ms");

        bool perfect = Math.Abs(delta) <= earlyRange;

        LastHitDelta = delta;
        LastHitBeatIndex = nearestBeatIndex;

        PlayPressScale();

        if (perfect)
        {
            if (nearestBeatIndex == lastPerfectBeatIndex)
                return false;

            lastPerfectBeatIndex = nearestBeatIndex;

            SpawnPerfectEffect();
            RuntimeManager.PlayOneShot(beatSFXEvent);

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

        // ==========================================================
        // 3. ★ 自動校準 Offset（新增開關）
        // ==========================================================
        if (autoCalibrateOffset && perfect)
        {
            // 讓 Offset 漸漸逼近玩家習慣的節奏輸入時間
            judgeOffset = Mathf.Lerp((float)judgeOffset, (float)(judgeOffset - delta), 0.15f);

            Debug.Log($"[Offset Calibration] offset = {judgeOffset:0.000}");
        }

        return perfect;
    }


    // ==========================================================
    // Combo 系統
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
        if (perfectEffectPrefab == null) return;

        GameObject obj = Instantiate(perfectEffectPrefab, beatHitPointUI);

        var rt = obj.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = beatHitPointUI.anchorMin;
            rt.anchorMax = beatHitPointUI.anchorMax;
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
        }
        else
        {
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = Vector3.one;
        }

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
