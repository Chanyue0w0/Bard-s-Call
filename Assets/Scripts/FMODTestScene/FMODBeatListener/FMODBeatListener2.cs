using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public class FMODBeatListener2 : MonoBehaviour
{
    public static FMODBeatListener2 Instance { get; private set; }

    [Header("FMOD Event")]
    public EventReference musicEvent;
    [Header("Perfect 音效")]
    public EventReference perfectSFX;

    [Header("判定範圍設定")]
    [Tooltip("Perfect ±範圍(秒)")]
    public float perfectWindow = 0.10f;   // 100ms 推薦

    [Tooltip("Early / Late 可接受範圍(秒)")]
    public float maxJudgeWindow = 0.25f;  // 250ms 推薦

    [Tooltip("延遲補償(秒)")]
    public float audioLatencyOffset = 0f;

    // ================================
    // Beat Timeline（整首歌）
    // ================================
    public class BeatInfo
    {
        public int index;
        public float time;   // 秒（FMOD timeline position）
        public int bar;
        public int beat;
        public float tempo;
    }

    private readonly List<BeatInfo> beatTimeline = new List<BeatInfo>();
    private EventInstance musicInstance;

    // 拍點基礎資訊（從第一次 callback 拿）
    private bool firstBeatReceived = false;
    private int firstBar = 0;
    private int firstBeatInBar = 0;
    private float firstBeatMusicTime = 0f;   // 秒
    private float currentTempo = 120f;
    private int timeSigUpper = 4;
    private int timeSigLower = 4;
    private float eventLengthSec = 0f;

    private bool beatTimelineReady = false;

    [Header("播放延遲設定")]
    [Tooltip("開始遊戲後，延遲幾秒再播放 BGM（秒）")]
    public float musicStartDelay = 0f;

    [Header("Auto Perfect 設定")]
    [Tooltip("自動在每個拍點觸發 Perfect（用於測試拍點正確性）")]
    public bool autoPerfect = false;

    private int lastAutoBeatIndex = -1; // 避免同一拍重複觸發
    private int lastJudgedBeatIndex = -1;

    // ================================
    // callback 佇列（避免在 audio thread 動到 Unity）
    // ================================
    private struct CallbackData
    {
        public int bar;
        public int beat;
        public float tempo;
        public int tsUpper;
        public int tsLower;
    }

    private static readonly Queue<CallbackData> s_pendingCallbacks = new Queue<CallbackData>();

    private FMOD.Studio.EVENT_CALLBACK beatCallbackDelegate;

    // ================================
    // Life Cycle
    // ================================
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
        InitializeFMOD();

        if (musicStartDelay > 0f)
            Invoke(nameof(StartMusic), musicStartDelay);
        else
            StartMusic();
    }

    private void StartMusic()
    {
        if (!musicInstance.isValid())
        {
            Debug.LogError("[FMODBeatListener2] musicInstance 無效，無法播放");
            return;
        }

        musicInstance.start();
        Debug.Log($"[FMODBeatListener2] 音樂已播放（延遲 {musicStartDelay:F2} 秒後啟動）");
    }


    private void Update()
    {
        ProcessPendingCallbacks();
        AutoPerfectTick();
    }

    // ===============================================
    // Auto Perfect：自動在每拍觸發 Perfect 事件
    // ===============================================
    private void AutoPerfectTick()
    {
        if (!autoPerfect) return;
        if (!beatTimelineReady) return;
        if (!musicInstance.isValid()) return;

        // 取得現在音樂時間
        musicInstance.getTimelinePosition(out int posMs);
        float currentMusicTime = posMs / 1000f + audioLatencyOffset;

        // 找最近拍點
        float minAbsDelta = float.MaxValue;
        float bestDelta = 0f;
        int bestIndex = -1;

        for (int i = 0; i < beatTimeline.Count; i++)
        {
            float d = currentMusicTime - beatTimeline[i].time;
            float abs = Mathf.Abs(d);

            if (abs < minAbsDelta)
            {
                minAbsDelta = abs;
                bestDelta = d;
                bestIndex = i;
            }
        }

        if (bestIndex == -1)
            return;

        // 避免同一拍點多次觸發
        if (bestIndex == lastAutoBeatIndex)
            return;

        // 若離拍點太遠，不要判定
        if (minAbsDelta > perfectWindow)
            return;

        lastAutoBeatIndex = bestIndex;

        // 播音效
        if (!perfectSFX.IsNull)
            RuntimeManager.PlayOneShot(perfectSFX);

        var info = beatTimeline[bestIndex];

        Debug.Log(
            $"<color=cyan>[AUTO PERFECT]</color> " +
            $"拍 {bestIndex} | bar={info.bar}, beat={info.beat} | Δ = {bestDelta * 1000f:+0.0;-0.0} ms");
    }


    private void OnDestroy()
    {
        if (musicInstance.isValid())
        {
            musicInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            musicInstance.release();
        }

        if (Instance == this)
            Instance = null;
    }

    // ================================
    // 初始化
    // ================================
    private void InitializeFMOD()
    {
        if (musicEvent.IsNull)
        {
            Debug.LogError("[FMODBeatListener2] musicEvent 未設定");
            return;
        }

        musicInstance = RuntimeManager.CreateInstance(musicEvent);

        if (!musicInstance.isValid())
        {
            Debug.LogError("[FMODBeatListener2] 建立 musicInstance 失敗");
            return;
        }

        // 取得整個 event 長度（毫秒）
        musicInstance.getDescription(out EventDescription desc);
        desc.getLength(out int lengthMs);
        eventLengthSec = lengthMs / 1000f;

        // 註冊 TIMELINE_BEAT callback
        beatCallbackDelegate = new FMOD.Studio.EVENT_CALLBACK(BeatCallback);
        var result = musicInstance.setCallback(beatCallbackDelegate, EVENT_CALLBACK_TYPE.TIMELINE_BEAT);
        if (result != FMOD.RESULT.OK)
        {
            Debug.LogError($"[FMODBeatListener2] setCallback 失敗: {result}");
        }

        // 啟動音樂
        //musicInstance.start();
        //Debug.Log("[FMODBeatListener2] 音樂已啟動，等待第一個拍點 callback...");
    }

    // ================================
    // FMOD Callback（在 audio thread 上）
    // ================================
    [AOT.MonoPInvokeCallback(typeof(FMOD.Studio.EVENT_CALLBACK))]
    private static FMOD.RESULT BeatCallback(EVENT_CALLBACK_TYPE type, IntPtr instancePtr, IntPtr parameterPtr)
    {
        if (type != EVENT_CALLBACK_TYPE.TIMELINE_BEAT)
            return FMOD.RESULT.OK;

        try
        {
            var beatProps =
                (TIMELINE_BEAT_PROPERTIES)Marshal.PtrToStructure(parameterPtr, typeof(TIMELINE_BEAT_PROPERTIES));

            var data = new CallbackData
            {
                bar = beatProps.bar,
                beat = beatProps.beat,
                tempo = beatProps.tempo,
                tsUpper = beatProps.timesignatureupper,
                tsLower = beatProps.timesignaturelower
            };

            lock (s_pendingCallbacks)
            {
                s_pendingCallbacks.Enqueue(data);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[FMODBeatListener2] BeatCallback 例外: {e.Message}");
            return FMOD.RESULT.ERR_INTERNAL;
        }

        return FMOD.RESULT.OK;
    }

    // ================================
    // 主線程處理 callback 結果
    // ================================
    private void ProcessPendingCallbacks()
    {
        while (true)
        {
            CallbackData data;
            lock (s_pendingCallbacks)
            {
                if (s_pendingCallbacks.Count == 0)
                    break;

                data = s_pendingCallbacks.Dequeue();
            }

            // 第一次收到拍點 → 當作整首拍點的起點
            if (!firstBeatReceived)
            {
                firstBeatReceived = true;

                musicInstance.getTimelinePosition(out int posMs);
                firstBeatMusicTime = posMs / 1000f;  // 秒

                firstBar = data.bar;
                firstBeatInBar = data.beat;
                currentTempo = data.tempo;
                timeSigUpper = data.tsUpper;
                timeSigLower = data.tsLower;

                Debug.Log(
                    $"[FMODBeatListener2] 第一個拍點: bar={firstBar}, beat={firstBeatInBar}, tempo={currentTempo}, musicTime={firstBeatMusicTime:F3}s");

                BuildBeatTimelineFromFirstBeat();
            }
            else
            {
                // 如果中途 tempo 變化，這邊可以再做進階處理（目前先忽略）
                if (Mathf.Abs(data.tempo - currentTempo) > 0.01f)
                {
                    Debug.Log(
                        $"[FMODBeatListener2] BPM 變化：{currentTempo:F1} → {data.tempo:F1}（目前範例先不重建 timeline）");
                    currentTempo = data.tempo;
                }
            }
        }
    }

    // ================================
    // 用「第一個拍點」＋ tempo ＋ event 長度 → 預算整首拍點
    // ================================
    private void BuildBeatTimelineFromFirstBeat()
    {
        beatTimeline.Clear();
        beatTimelineReady = false;

        if (eventLengthSec <= 0f)
        {
            Debug.LogError("[FMODBeatListener2] eventLengthSec 無效，無法建立 beat timeline");
            return;
        }

        if (currentTempo <= 0f)
        {
            Debug.LogError("[FMODBeatListener2] tempo 無效，無法建立 beat timeline");
            return;
        }

        float beatInterval = 60f / currentTempo; // 每拍秒數
        float startTime = firstBeatMusicTime;

        // 估算最大拍數，保守抓長一點
        int maxBeats = Mathf.CeilToInt((eventLengthSec - startTime) / beatInterval) + 8;
        if (maxBeats < 0) maxBeats = 0;

        int currentBar = firstBar;
        int currentBeatInBar = firstBeatInBar;

        for (int i = 0; i < maxBeats; i++)
        {
            float t = startTime + i * beatInterval;
            if (t > eventLengthSec + 0.5f)  // 留 0.5 秒緩衝
                break;

            var info = new BeatInfo
            {
                index = i,
                time = t,
                bar = currentBar,
                beat = currentBeatInBar,
                tempo = currentTempo
            };

            beatTimeline.Add(info);

            // 更新小節/拍
            currentBeatInBar++;
            if (currentBeatInBar > timeSigUpper)
            {
                currentBeatInBar = 1;
                currentBar++;
            }
        }

        beatTimelineReady = true;
        Debug.Log(
            $"[FMODBeatListener2] 整首 beat timeline 已建立：共 {beatTimeline.Count} 拍，長度約 {eventLengthSec:F3}s");
    }

    // ================================
    // 公開 API：判定
    // ================================
    public enum Judge
    {
        Miss,
        Late,
        Early,
        Perfect
    }

    public bool IsOnBeat(out Judge result, out int nearestBeatIndex, out float deltaSec)
    {
        result = Judge.Miss;
        nearestBeatIndex = -1;
        deltaSec = 0f;

        if (!beatTimelineReady)
        {
            Debug.LogWarning("[FMODBeatListener2] beatTimeline 尚未建立（可能還沒收到第一個拍點 callback）");
            return false;
        }

        if (!musicInstance.isValid())
        {
            Debug.LogWarning("[FMODBeatListener2] musicInstance 無效");
            return false;
        }

        // 取得現在 FMOD 播放時間
        musicInstance.getTimelinePosition(out int posMs);
        float currentMusicTime = posMs / 1000f + audioLatencyOffset;

        // 搜尋最近的拍點
        float minAbsDelta = float.MaxValue;
        float bestDelta = 0f;
        int bestIndex = -1;

        // 這裡簡單用線性搜尋，你之後想優化可以改二分搜尋
        for (int i = 0; i < beatTimeline.Count; i++)
        {
            float d = currentMusicTime - beatTimeline[i].time;
            float abs = Mathf.Abs(d);

            if (abs > maxJudgeWindow)
                continue;

            if (abs < minAbsDelta)
            {
                minAbsDelta = abs;
                bestDelta = d;
                bestIndex = i;
            }
        }

        if (bestIndex == -1)
        {
            // 找不到落在判定窗內的拍點
            return false;
        }

        nearestBeatIndex = bestIndex;
        deltaSec = bestDelta;

        float absDelta = Mathf.Abs(bestDelta);

        if (absDelta <= perfectWindow)
        {
            if (bestIndex == lastJudgedBeatIndex)
            {
                // 同一拍點不可重複判定
                result = Judge.Miss;
                return false;
            }

            result = Judge.Perfect;

            lastJudgedBeatIndex = bestIndex;

            RuntimeManager.PlayOneShot(perfectSFX);

            return true;
        }

        else
        {
            result = Judge.Miss;
            return false;
        }
    }

    // 簡化版：只關心是不是 Perfect
    public bool IsPerfect()
    {
        return IsOnBeat(out Judge r, out _, out _) && r == Judge.Perfect;
    }

    // 方便你在 Inspector 右鍵測試
    [ContextMenu("Debug 顯示前 8 拍")]
    private void DebugPrintFirstBeats()
    {
        if (!beatTimelineReady)
        {
            Debug.LogWarning("beatTimeline 尚未 ready");
            return;
        }

        int count = Mathf.Min(8, beatTimeline.Count);
        for (int i = 0; i < count; i++)
        {
            var b = beatTimeline[i];
            Debug.Log($"Beat[{i}] t={b.time:F3}s, bar={b.bar}, beat={b.beat}");
        }
    }
}
