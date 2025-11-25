using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using FMODUnity;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// FMOD 節奏判定系統 - 配合新架構
/// 核心改進：
/// 1. 基於 Listener 的虛擬拍點插值系統
/// 2. 動態查詢最近的可判定拍點
/// 3. 防止重複判定
/// </summary>
public class FMODBeatJudge : MonoBehaviour
{
    public static FMODBeatJudge Instance { get; private set; }

    // ========================================
    // Inspector 設定
    // ========================================
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

    [Header("Debug 模式")]
    [Tooltip("自動在每個拍點判定 Perfect（用於測試）")]
    public bool autoPerfectEveryBeat = false;

    [Header("Debug UI 顯示")]
    public Text debugInfoText;
    public bool showDetailedLog = true;

    // ========================================
    // 私有變數
    // ========================================
    private int comboCount = 0;
    private float lastHitTime;

    private int totalPerfectCount = 0;
    private int totalMissCount = 0;

    private Coroutine scaleRoutine;
    private Coroutine comboTimerRoutine;

    // 拍點消耗追蹤
    private HashSet<int> consumedBeats = new HashSet<int>();

    // ========================================
    // Unity 生命週期
    // ========================================
    private void Awake()
    {
        if (Instance != null)
            Destroy(this);
        Instance = this;
    }

    private void OnEnable()
    {
        FMODBeatListener.OnGlobalBeat += OnBeatUpdate;
    }

    private void OnDisable()
    {
        FMODBeatListener.OnGlobalBeat -= OnBeatUpdate;
    }

    private void OnBeatUpdate(int beatIndex)
    {
        // 清理舊的已消耗拍點（保留最近 5 個）
        consumedBeats.RemoveWhere(beat => beat < beatIndex - 5);
    }

    // ========================================
    // ★★★ 核心改進：玩家輸入判定
    // ========================================
    /// <summary>
    /// 判定當前輸入是否在拍點範圍內
    /// </summary>
    public bool IsOnBeat()
    {
        if (FMODBeatListener.Instance == null)
            return false;

        // 獲取當前時間
        float hitTime = FMODBeatListener.Instance.GetCurrentTime();

        // ★★★ 查詢最近的虛擬拍點
        int nearestBeat = FMODBeatListener.Instance.GetNearestVirtualBeat(hitTime);

        if (nearestBeat < 0)
        {
            UpdateDebugUI("<color=red>[系統未就緒]</color> 等待第一個 Callback");
            return false;
        }

        // ★★★ 檢查是否在判定窗口內
        if (!FMODBeatListener.Instance.IsInBeatWindow(nearestBeat, hitTime, out float delta))
        {
            // Miss：不在窗口內
            HandleMiss(hitTime, delta, nearestBeat);
            return false;
        }

        // ★★★ 檢查該拍是否已被判定
        if (consumedBeats.Contains(nearestBeat))
        {
            UpdateDebugUI($"<color=yellow>[重複判定]</color> 拍點 {nearestBeat} 已被判定過");
            if (showDetailedLog)
                Debug.LogWarning($"[BeatJudge] 拍點 {nearestBeat} 已被判定過，忽略");
            return false;
        }

        // ★★★ 防止判定未來的拍點（必須是當前或剛過去的拍）
        int currentBeat = FMODBeatListener.GlobalBeatIndex;
        if (nearestBeat > currentBeat + 1)
        {
            UpdateDebugUI($"<color=red>[判定過早]</color> 嘗試判定未來拍點 {nearestBeat}（當前 {currentBeat}）");
            if (showDetailedLog)
                Debug.LogWarning($"[BeatJudge] 嘗試判定未來拍點 {nearestBeat}（當前拍 {currentBeat}）");
            return false;
        }

        // ★ Perfect 判定
        HandlePerfect(nearestBeat, delta);
        return true;
    }

    // ========================================
    // Perfect 處理
    // ========================================
    private void HandlePerfect(int beatIndex, float delta)
    {
        // 標記為已消耗
        consumedBeats.Add(beatIndex);

        // 播放動畫
        PlayPressScale();

        // 播放特效與音效
        SpawnPerfectEffect();
        RuntimeManager.PlayOneShot(beatSFXEvent);

        // 震動回饋
        if (Gamepad.current != null)
            VibrationManager.Instance?.Vibrate("Perfect");

        // 通知其他系統
        FeverManager.Instance?.AddPerfect();
        RegisterBeatResult(true);

        // 更新統計
        totalPerfectCount++;

        // 計算顯示資訊
        float deltaMs = delta * 1000f;
        string timing = delta > 0 ? "晚" : "早";
        int beatInBar = FMODBeatListener.Instance.GetBeatInBar(beatIndex);
        bool isHeavy = FMODBeatListener.Instance.IsHeavyBeat(beatIndex);
        string beatType = isHeavy ? "<color=yellow>重拍</color>" : "輕拍";

        UpdateDebugUI($"<color=lime>[Perfect]</color> {beatType} | 拍點 {beatIndex} ({beatInBar}/4) | Δ = {deltaMs:+0.0;-0.0} ms ({timing})");

        if (showDetailedLog)
            Debug.Log($"[BeatJudge] ✅ Perfect | {beatType} | Δ = {deltaMs:+0.0;-0.0} ms | Beat {beatIndex}");
    }

    // ========================================
    // Miss 處理
    // ========================================
    private void HandleMiss(float hitTime, float delta, int nearestBeat)
    {
        // 播放動畫
        PlayPressScale();

        // 播放 Miss 特效
        SpawnMissText();

        // 通知其他系統
        FeverManager.Instance?.AddMiss();
        RegisterBeatResult(false);

        // 更新統計
        totalMissCount++;

        // 計算顯示資訊
        float deltaMs = delta * 1000f;
        string timing = delta > 0 ? "晚" : "早";
        float windowMs = FMODBeatListener.Instance.GetJudgementWindow() * 1000f;

        UpdateDebugUI($"<color=red>[Miss]</color> 目標拍點 {nearestBeat} | Δ = {deltaMs:+0.0;-0.0} ms ({timing}) | 超出窗口 ±{windowMs:0.0}ms");

        if (showDetailedLog)
            Debug.Log($"[BeatJudge] ❌ Miss | Δ = {deltaMs:+0.0;-0.0} ms | 時間 {hitTime:F3}s | 目標拍點 {nearestBeat}");
    }

    // ========================================
    // Debug：自動 Perfect
    // ========================================
    public void ForcePerfectFromListener(int beatIndex)
    {
        if (consumedBeats.Contains(beatIndex))
            return;

        consumedBeats.Add(beatIndex);

        SpawnPerfectEffect();
        RuntimeManager.PlayOneShot(beatSFXEvent);

        if (Gamepad.current != null)
            VibrationManager.Instance?.Vibrate("Perfect");

        FeverManager.Instance?.AddPerfect();
        RegisterBeatResult(true);

        totalPerfectCount++;

        int beatInBar = FMODBeatListener.Instance.GetBeatInBar(beatIndex);
        bool isHeavy = FMODBeatListener.Instance.IsHeavyBeat(beatIndex);
        string beatType = isHeavy ? "<color=yellow>重拍</color>" : "輕拍";

        UpdateDebugUI($"<color=lime>[Auto Perfect]</color> {beatType} | 拍點 {beatIndex} ({beatInBar}/4) | Δ = 0.0 ms");

        if (showDetailedLog)
            Debug.Log($"[Debug Perfect] Beat {beatIndex} 自動 Perfect");
    }

    // ========================================
    // Debug UI 更新
    // ========================================
    private void UpdateDebugUI(string message)
    {
        if (debugInfoText == null)
            return;

        float accuracy = GetAccuracy();

        debugInfoText.text = $"{message}\n" +
                            $"<color=white>統計：Perfect {totalPerfectCount} | Miss {totalMissCount} | 準確率 {accuracy:0.0}%</color>\n" +
                            $"<color=grey>當前拍點：{FMODBeatListener.GlobalBeatIndex} | BPM：{FMODBeatListener.Tempo:F1}</color>";
    }

    private float GetAccuracy()
    {
        int total = totalPerfectCount + totalMissCount;
        if (total == 0)
            return 100f;
        return (totalPerfectCount / (float)total) * 100f;
    }

    // ========================================
    // 公開方法：重置統計
    // ========================================
    public void ResetStatistics()
    {
        totalPerfectCount = 0;
        totalMissCount = 0;
        UpdateDebugUI("<color=yellow>[統計已重置]</color>");
    }

    // ========================================
    // Combo 系統
    // ========================================
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

    // ========================================
    // UI & 特效
    // ========================================
    private void PlayPressScale()
    {
        if (beatHitPointUI == null)
            return;

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
        if (perfectEffectPrefab == null)
            return;

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
    }

    private void SpawnMissText()
    {
        if (missTextPrefab == null || beatHitPointUI == null)
            return;

        GameObject obj = Instantiate(missTextPrefab, beatHitPointUI.parent);
        var rt = obj.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = beatHitPointUI.anchoredPosition + new Vector2(0, 60f);
        }
        else
        {
            obj.transform.localPosition = beatHitPointUI.localPosition + new Vector3(0, 60f, 0);
        }

        Destroy(obj, 0.3f);
    }
}