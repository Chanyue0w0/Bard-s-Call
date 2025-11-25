using UnityEngine;
using UnityEngine.UI;
using FMODUnity;
using FMOD.Studio;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

/// <summary>
/// FMOD 拍點監聽器 - 全新架構
/// 核心策略：
/// 1. 每個 Callback 作為錨點，記錄精確時間
/// 2. 在兩個 Callback 之間插值計算虛擬拍點
/// 3. 玩家判定基於虛擬拍點 + 窗口
/// 4. 新 Callback 到達時重新校準
/// </summary>
public class FMODBeatListener : MonoBehaviour
{
    public static FMODBeatListener Instance { get; private set; }

    // ========================================
    // FMOD 設定
    // ========================================
    [Header("FMOD 事件設定")]
    public EventReference musicEvent;
    public EventReference beatSFXEvent;
    private EventInstance musicInstance;
    private FMOD.Studio.EVENT_CALLBACK beatCallback;

    // ========================================
    // ★ 保留原有的靜態變數
    // ========================================
    private static int s_currentBar;
    private static int s_currentBeatInBar;
    private static int s_globalBeatIndex = -1;
    private static int s_timeSigUpper = 4;
    private static int s_timeSigLower = 4;
    private static float s_tempo = 120f;

    public static int CurrentBeatInBar => s_currentBeatInBar;
    public static int CurrentBar => s_currentBar;
    public static int BeatsPerMeasure => s_timeSigUpper;
    public static int GlobalBeatIndex => s_globalBeatIndex;
    public static float Tempo => s_tempo;

    // ========================================
    // ★★★ 新架構：Callback 錨點系統
    // ========================================
    [Header("判定系統設定")]
    [Tooltip("判定窗口（秒），例如 0.1 表示 ±100ms")]
    public float judgementWindow = 0.1f;

    // Callback 錨點記錄
    private class BeatAnchor
    {
        public int globalBeatIndex;     // 拍點索引
        public float callbackTime;      // Callback 到達的精確時間（Time.time）
        public float tempo;             // 當時的 BPM
        public int beatInBar;           // 小節內拍點
        public int bar;                 // 小節數
    }

    private List<BeatAnchor> beatAnchors = new List<BeatAnchor>(); // 記錄所有 Callback 錨點
    private BeatAnchor lastAnchor = null;  // 最近的錨點
    private float beatInterval = 0.5f;     // 每拍間隔（秒）

    // ========================================
    // Float Beat System（保留）
    // ========================================
    private float lastBeatTime = 0f;
    private float currentBeatTime = 0f;
    public static event Action<float> OnBeatDelta;

    // ========================================
    // UI 動畫（保留）
    // ========================================
    [Header("Beat UI（節奏呼吸）")]
    public Image beatPulseImage;
    public float pulseScaleUp = 1.35f;
    public float pulseScaleTime = 0.08f;
    public float pulseRecoverTime = 0.12f;
    private Coroutine pulseRoutine;

    [Header("Heavy Beat Notify UI")]
    public Image heavyBeatNotifyUI;
    public Sprite normalBeatSprite;
    public Sprite preHeavyBeatSprite;
    public Sprite heavyBeatSprite;

    [Header("Debug｜拍點顯示")]
    public Text beatDebugText;

    [Tooltip("重拍間隔，例如 4 表示每 4 拍是重拍")]
    public int heavyBeatInterval = 4;

    [Tooltip("是否啟用重拍提示 UI")]
    public bool enableHeavyBeatNotify = true;

    // ========================================
    // 保留原有事件
    // ========================================
    public struct BeatInfo
    {
        public int bar;
        public int beatInBar;
        public int globalBeat;
        public float tempo;
        public int timeSigUpper;
        public int timeSigLower;
    }

    public static event Action<int> OnGlobalBeat;
    public static event Action<int, int> OnBarBeat;
    public static event Action<BeatInfo> OnBeatInfo;

    // ========================================
    // 排程系統（保留）
    // ========================================
    private class ScheduledAction
    {
        public int targetBeat;
        public Action action;
    }
    private static readonly List<ScheduledAction> s_scheduledActions = new();

    public void ScheduleAtBeat(int targetBeat, Action action)
    {
        if (action == null) return;
        s_scheduledActions.Add(new ScheduledAction { targetBeat = targetBeat, action = action });
    }

    public void ScheduleAfterBeats(int offset, Action action)
    {
        ScheduleAtBeat(s_globalBeatIndex + Mathf.Max(0, offset), action);
    }

    // ========================================
    // Callback 佇列
    // ========================================
    private struct CallbackData
    {
        public int bar;
        public int beat;
        public float tempo;
        public int timeSigUpper;
        public int timeSigLower;
        public float callbackTime;
    }
    private static readonly Queue<CallbackData> s_pendingCallbacks = new();

    // ========================================
    // Unity 生命週期
    // ========================================
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
        musicInstance = RuntimeManager.CreateInstance(musicEvent);
        beatCallback = new FMOD.Studio.EVENT_CALLBACK(BeatEventCallback);
        musicInstance.setCallback(beatCallback, EVENT_CALLBACK_TYPE.TIMELINE_BEAT);
        musicInstance.start();
    }

    private void Update()
    {
        ProcessPendingCallbacks();
        UpdateFloatBeat(); // 保留
    }

    private void OnDestroy()
    {
        if (musicInstance.isValid())
            musicInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        if (Instance == this)
            Instance = null;
    }

    // ========================================
    // FMOD Callback
    // ========================================
    [AOT.MonoPInvokeCallback(typeof(FMOD.Studio.EVENT_CALLBACK))]
    private static FMOD.RESULT BeatEventCallback(EVENT_CALLBACK_TYPE type, IntPtr inst, IntPtr param)
    {
        if (type != EVENT_CALLBACK_TYPE.TIMELINE_BEAT)
            return FMOD.RESULT.OK;

        var p = (TIMELINE_BEAT_PROPERTIES)Marshal.PtrToStructure(param, typeof(TIMELINE_BEAT_PROPERTIES));

        Debug.Log($"[FMOD Callback] Bar={p.bar}, Beat={p.beat}, Tempo={p.tempo}, TS={p.timesignatureupper}/{p.timesignaturelower}");

        CallbackData data = new CallbackData
        {
            bar = p.bar,
            beat = p.beat,
            tempo = p.tempo,
            timeSigUpper = p.timesignatureupper,
            timeSigLower = p.timesignaturelower,
            callbackTime = Time.time
        };

        lock (s_pendingCallbacks)
        {
            s_pendingCallbacks.Enqueue(data);
        }

        return FMOD.RESULT.OK;
    }

    // ========================================
    // ★★★ 核心改進：處理 Callback 錨點
    // ========================================
    private void ProcessPendingCallbacks()
    {
        while (true)
        {
            CallbackData d;
            lock (s_pendingCallbacks)
            {
                if (s_pendingCallbacks.Count == 0)
                    break;
                d = s_pendingCallbacks.Dequeue();
            }

            // 計算全局拍點索引
            int calculatedGlobalBeat = (d.bar * d.timeSigUpper) + (d.beat - 1);

            // 防止重複處理
            if (calculatedGlobalBeat <= s_globalBeatIndex)
            {
                Debug.LogWarning($"[FMODBeatListener] 重複拍點 {calculatedGlobalBeat}，忽略");
                continue;
            }

            // ★★★ 更新靜態變數
            s_globalBeatIndex = calculatedGlobalBeat;
            s_currentBar = d.bar;
            s_currentBeatInBar = d.beat;
            s_tempo = d.tempo;
            s_timeSigUpper = d.timeSigUpper;
            s_timeSigLower = d.timeSigLower;
            beatInterval = 60f / s_tempo;

            // ★★★ 記錄 Callback 錨點
            BeatAnchor anchor = new BeatAnchor
            {
                globalBeatIndex = calculatedGlobalBeat,
                callbackTime = d.callbackTime,
                tempo = d.tempo,
                beatInBar = d.beat,
                bar = d.bar
            };

            beatAnchors.Add(anchor);
            lastAnchor = anchor;

            // 清理舊錨點（保留最近 10 個）
            if (beatAnchors.Count > 10)
                beatAnchors.RemoveAt(0);

            Debug.Log($"<color=cyan>[錨點記錄]</color> 拍點={calculatedGlobalBeat}, 時間={d.callbackTime:F3}s, BPM={d.tempo}");

            // ★ 觸發原有事件
            BeatInfo info = new BeatInfo
            {
                bar = s_currentBar,
                beatInBar = s_currentBeatInBar,
                globalBeat = s_globalBeatIndex,
                tempo = s_tempo,
                timeSigUpper = s_timeSigUpper,
                timeSigLower = s_timeSigLower
            };

            PlayPulseAnimation();

            // 保留：自動 Perfect 功能
            if (FMODBeatJudge.Instance != null && FMODBeatJudge.Instance.autoPerfectEveryBeat)
            {
                FMODBeatJudge.Instance.ForcePerfectFromListener(s_globalBeatIndex);
            }

            OnGlobalBeat?.Invoke(s_globalBeatIndex);
            OnBarBeat?.Invoke(s_currentBar, s_currentBeatInBar);
            OnBeatInfo?.Invoke(info);

            // 更新 UI
            if (beatDebugText != null)
            {
                beatDebugText.text = $"Bar: {s_currentBar} | Beat: {s_currentBeatInBar} | Global: {s_globalBeatIndex}";
            }

            ProcessScheduledActions(s_globalBeatIndex);
        }
    }

    // ========================================
    // 保留：排程系統
    // ========================================
    private void ProcessScheduledActions(int beat)
    {
        for (int i = s_scheduledActions.Count - 1; i >= 0; i--)
        {
            if (s_scheduledActions[i].targetBeat <= beat)
            {
                try
                {
                    s_scheduledActions[i].action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
                s_scheduledActions.RemoveAt(i);
            }
        }
    }

    // ========================================
    // 保留：Float Beat System
    // ========================================
    private void UpdateFloatBeat()
    {
        if (musicInstance.isValid() == false)
            return;

        musicInstance.getTimelinePosition(out int ms);
        float sec = ms / 1000f;
        currentBeatTime = sec * (s_tempo / 60f);

        float deltaBeat = currentBeatTime - lastBeatTime;
        if (deltaBeat > 0)
        {
            OnBeatDelta?.Invoke(deltaBeat);
            lastBeatTime = currentBeatTime;
        }
    }

    public float GetCurrentBeatTime()
    {
        return currentBeatTime;
    }

    public float SecondsPerBeat => 60f / Mathf.Max(1f, s_tempo);

    // ========================================
    // ★★★ 核心改進：虛擬拍點計算（基於錨點插值）
    // ========================================
    /// <summary>
    /// 計算指定拍點的預測時間（基於最近的錨點）
    /// </summary>
    public float GetPredictedBeatTime(int targetBeatIndex)
    {
        if (lastAnchor == null)
            return 0f;

        // 基於最近的錨點計算
        int beatOffset = targetBeatIndex - lastAnchor.globalBeatIndex;
        float predictedTime = lastAnchor.callbackTime + (beatOffset * beatInterval);

        return predictedTime;
    }

    /// <summary>
    /// 檢查當前時間是否在指定拍點的判定窗口內
    /// </summary>
    public bool IsInBeatWindow(int targetBeatIndex, float currentTime, out float delta)
    {
        float predictedTime = GetPredictedBeatTime(targetBeatIndex);
        delta = currentTime - predictedTime;
        return Mathf.Abs(delta) <= judgementWindow;
    }

    /// <summary>
    /// 獲取當前最接近的虛擬拍點
    /// </summary>
    public int GetNearestVirtualBeat(float currentTime)
    {
        if (lastAnchor == null)
            return -1;

        // 基於最近的錨點計算當前應該在第幾拍
        float timeSinceAnchor = currentTime - lastAnchor.callbackTime;
        float beatsSinceAnchor = timeSinceAnchor / beatInterval;
        int nearestBeat = lastAnchor.globalBeatIndex + Mathf.RoundToInt(beatsSinceAnchor);

        return nearestBeat;
    }

    /// <summary>
    /// 獲取指定拍點在小節中的位置（1-4）
    /// </summary>
    public int GetBeatInBar(int globalBeatIndex)
    {
        if (lastAnchor == null)
            return 1;

        int offset = globalBeatIndex - lastAnchor.globalBeatIndex;
        int beatInBar = ((lastAnchor.beatInBar - 1 + offset) % s_timeSigUpper) + 1;
        return beatInBar;
    }

    /// <summary>
    /// 檢查指定拍點是否為重拍
    /// </summary>
    public bool IsHeavyBeat(int globalBeatIndex)
    {
        return GetBeatInBar(globalBeatIndex) == heavyBeatInterval;
    }

    // ========================================
    // UI 更新
    // ========================================

    private void PlayPulseAnimation()
    {
        if (beatPulseImage == null)
            return;

        if (pulseRoutine != null)
            StopCoroutine(pulseRoutine);

        pulseRoutine = StartCoroutine(PulseAnim());
    }

    private IEnumerator PulseAnim()
    {
        RectTransform rt = beatPulseImage.rectTransform;
        Vector3 s0 = Vector3.one;
        Vector3 s1 = Vector3.one * pulseScaleUp;

        float t = 0;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / pulseScaleTime;
            rt.localScale = Vector3.Lerp(s0, s1, t);
            yield return null;
        }

        t = 0;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / pulseRecoverTime;
            rt.localScale = Vector3.Lerp(s1, s0, t);
            yield return null;
        }

        rt.localScale = s0;
    }

    // ========================================
    // 公開方法
    // ========================================
    public float GetCurrentTime()
    {
        return Time.time;
    }

    public float GetJudgementWindow()
    {
        return judgementWindow;
    }
}