using UnityEngine;
using UnityEngine.UI;
using FMODUnity;
using FMOD.Studio;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public class FMODBeatListener : MonoBehaviour
{
    public static FMODBeatListener Instance { get; private set; }

    [Header("FMOD 事件設定")]
    public EventReference musicEvent;
    public EventReference beatSFXEvent;  // ★ 不再在 Listener 播放，由 Judge 控制 Perfect 時播放

    private EventInstance musicInstance;
    private FMOD.Studio.EVENT_CALLBACK beatCallback;

    // ========================================
    // UI 動畫
    // ========================================
    [Header("Beat UI（節奏呼吸）")]
    public Image beatPulseImage;
    public float pulseScaleUp = 1.35f;
    public float pulseScaleTime = 0.08f;
    public float pulseRecoverTime = 0.12f;
    private Coroutine pulseRoutine;

    // ========================================
    // 拍點狀態
    // ========================================
    private static int s_currentBar;
    private static int s_currentBeatInBar;
    private static int s_globalBeatIndex = -1;

    private static float s_tempo = 120f;
    private static int s_timeSigUpper = 4;
    private static int s_timeSigLower = 4;

    private struct BeatData
    {
        public int bar;
        public int beatInBar;
        public float tempo;
        public int tsUpper;
        public int tsLower;
        public int globalBeatIndex;
    }
    private static readonly Queue<BeatData> s_pendingBeats = new();

    // ========================================
    // 對外事件
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

    public float SecondsPerBeat => 60f / Mathf.Max(1f, s_tempo);
    public int GlobalBeatIndex => s_globalBeatIndex;


    // ========================================
    // 排程
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
    // Unity Life Cycle
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
        ProcessPendingBeats();
    }

    private void OnDestroy()
    {
        if (musicInstance.isValid())
            musicInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);

        if (Instance == this)
            Instance = null;
    }

    // ========================================
    // 主線程處理拍點
    // ========================================
    private void ProcessPendingBeats()
    {
        while (s_pendingBeats.Count > 0)
        {
            BeatData d;
            lock (s_pendingBeats) d = s_pendingBeats.Dequeue();

            s_currentBar = d.bar;
            s_currentBeatInBar = d.beatInBar;
            s_tempo = d.tempo;
            s_timeSigUpper = d.tsUpper;
            s_timeSigLower = d.tsLower;
            s_globalBeatIndex = d.globalBeatIndex;

            BeatInfo info = new BeatInfo
            {
                bar = s_currentBar,
                beatInBar = s_currentBeatInBar,
                globalBeat = s_globalBeatIndex,
                tempo = s_tempo,
                timeSigUpper = s_timeSigUpper,
                timeSigLower = s_timeSigLower
            };

            // UI 節奏脈衝
            PlayPulseAnimation();

            // ==========================================================
            // ★ Debug：每拍自動 Perfect（你需要的功能）
            // ==========================================================
            if (FMODBeatJudge.Instance != null && FMODBeatJudge.Instance.autoPerfectEveryBeat)
            {
                FMODBeatJudge.Instance.ForcePerfectFromListener(s_globalBeatIndex);
            }

            // 廣播事件（角色 AI / UI 都會聽到）
            OnGlobalBeat?.Invoke(s_globalBeatIndex);
            OnBarBeat?.Invoke(s_currentBar, s_currentBeatInBar);
            OnBeatInfo?.Invoke(info);

            // ==========================================================
            // ★ 修正重點：使排程與下一拍行為能正確執行！！
            // ==========================================================
            ProcessScheduledActions(s_globalBeatIndex);
        }
    }


    private void ProcessScheduledActions(int beat)
    {
        for (int i = s_scheduledActions.Count - 1; i >= 0; i--)
        {
            if (s_scheduledActions[i].targetBeat <= beat)
            {
                try { s_scheduledActions[i].action?.Invoke(); }
                catch (Exception ex) { Debug.LogError(ex); }
                s_scheduledActions.RemoveAt(i);
            }
        }
    }

    // ========================================
    // UI Pulse
    // ========================================
    private void PlayPulseAnimation()
    {
        if (beatPulseImage == null) return;

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
    // FMOD Callback
    // ========================================
    [AOT.MonoPInvokeCallback(typeof(FMOD.Studio.EVENT_CALLBACK))]
    private static FMOD.RESULT BeatEventCallback(EVENT_CALLBACK_TYPE type, IntPtr inst, IntPtr param)
    {
        if (type != EVENT_CALLBACK_TYPE.TIMELINE_BEAT)
            return FMOD.RESULT.OK;

        var p = (TIMELINE_BEAT_PROPERTIES)Marshal.PtrToStructure(param, typeof(TIMELINE_BEAT_PROPERTIES));

        BeatData d = new BeatData
        {
            bar = p.bar,
            beatInBar = p.beat,
            tempo = p.tempo,
            tsUpper = p.timesignatureupper,
            tsLower = p.timesignaturelower,
            globalBeatIndex = s_globalBeatIndex + 1
        };

        lock (s_pendingBeats)
        {
            s_pendingBeats.Enqueue(d);
        }

        return FMOD.RESULT.OK;
    }
}
