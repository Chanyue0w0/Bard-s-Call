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

    [Header("Perfect / Miss UI")]
    public GameObject perfectEffectPrefab;
    public GameObject missTextPrefab;
    public RectTransform beatHitPointUI;

    [Header("判定範圍設定")]
    [Tooltip("Perfect ±範圍(秒)")]
    public float perfectWindow = 0.10f;   // 100ms 推薦

    [Tooltip("Early / Late 可接受範圍(秒)")]
    public float maxJudgeWindow = 0.25f;  // 250ms 推薦

    [Tooltip("延遲補償(秒)")]
    public float audioLatencyOffset = 0f;

    // ========== 新動畫用 Beat 浮動系統 ==========
    // 基於 beatTimeline 和 tempo 估算的「全局浮動拍點位置」
    private float lastBeatPosition = 0f;
    private float currentBeatPosition = 0f;

    // 對動畫廣播（BeatSpriteAnimator 會訂閱）
    public static event Action<float> OnBeatDelta_Anim;


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

    private int lastTimelineMs = 0;


    // 供整個遊戲使用的全局拍點序號（從 0 開始）
    public int GlobalBeatIndex = -1;

    // ========== 與舊版 Listener API 對齊（給 UI 使用） ==========
    public int CurrentBeatInBar { get; private set; } = 1;
    public int CurrentBar { get; private set; } = 1;
    public int BeatsPerMeasure => timeSigUpper;

    // 用於 BeatUIAnimator 的 (float) 拍點時間
    public float CurrentBeatTime { get; private set; } = 0f;

    // 每拍秒數
    public float SecondsPerBeat => 60f / Mathf.Max(1f, currentTempo);


    public static event Action<int> OnGlobalBeat;          // 例如：斧頭哥布林每 8 拍攻擊
    public static event Action<int, int> OnBarBeat;        // 小節 + 拍
    public static event Action<BeatEventInfo> OnBeatInfo;  // 完整資訊


    private static readonly Queue<CallbackData> s_pendingCallbacks = new Queue<CallbackData>();

    private FMOD.Studio.EVENT_CALLBACK beatCallbackDelegate;

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

    // ========== 全局 Beat 事件（供角色、AI、技能、UI 使用） ==========
    public struct BeatEventInfo
    {
        public int bar;
        public int beat;
        public int globalBeat;
        public float tempo;
        public int timeSigUpper;
        public int timeSigLower;
    }
    

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
        DetectLoop();            // ★★★ 新增：偵測 Loop
        ProcessPendingCallbacks();
        AutoPerfectTick();
        UpdateAnimationBeat();   // ★ 新增：給動畫用
    }

    private void DetectLoop()
    {
        if (!musicInstance.isValid())
            return;

        musicInstance.getTimelinePosition(out int posMs);

        // ★★★ 判定是否 Loop（秒數從大跳到小）
        if (posMs < lastTimelineMs)
        {
            Debug.Log("<color=yellow>[FMODBeatListener2] Loop detected — resetting phase</color>");

            // ★ 重新對準浮動拍點系統
            firstBeatMusicTime = 0f;

            // ★ 重置動畫節奏推進
            lastBeatPosition = 0f;
            currentBeatPosition = 0f;

            // ★ 重置 UI/Animator 使用的 beatTime
            CurrentBeatTime = 0f;

            // 注意：不要重置 GlobalBeatIndex
            // 因為遊戲邏輯（敵人AI/技能）需要連續拍點，不是每輪都 0..
        }

        lastTimelineMs = posMs;
    }


    // ======================================================
    // ★★★ 新動畫節拍系統（基於 beatTimeline + tempo）
    // ======================================================
    private void UpdateAnimationBeat()
    {
        if (!beatTimelineReady)
            return;

        if (!musicInstance.isValid())
            return;

        // 取得 FMOD 音樂目前播放秒數
        musicInstance.getTimelinePosition(out int posMs);
        float musicSec = posMs / 1000f + audioLatencyOffset;

        // 取得此秒數的「第幾拍（float）」：起點 firstBeatMusicTime
        float beatInterval = 60f / currentTempo;

        currentBeatPosition = (musicSec - firstBeatMusicTime) / beatInterval;

        // 計算這一 frame 過了多少拍
        float beatDelta = currentBeatPosition - lastBeatPosition;

        if (beatDelta > 0f)
        {
            OnBeatDelta_Anim?.Invoke(beatDelta);
            lastBeatPosition = currentBeatPosition;
        }
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

            // 第一次拍點
            if (!firstBeatReceived)
            {
                firstBeatReceived = true;

                musicInstance.getTimelinePosition(out int posMs);
                firstBeatMusicTime = posMs / 1000f;

                firstBar = data.bar;
                firstBeatInBar = data.beat;
                currentTempo = data.tempo;
                timeSigUpper = data.tsUpper;
                timeSigLower = data.tsLower;

                BuildBeatTimelineFromFirstBeat();
            }
            else
            {
                if (Mathf.Abs(data.tempo - currentTempo) > 0.01f)
                {
                    currentTempo = data.tempo;
                }
            }

            // ★★★ 推進全局拍點（舊版 Listener 也有）
            GlobalBeatIndex++;
            // 更新目前所在小節資訊
            CurrentBar = data.bar;
            CurrentBeatInBar = data.beat;

            // 計算浮動 beat position（單純用 timeline）
            musicInstance.getTimelinePosition(out int posMsUpdate);
            CurrentBeatTime = (posMsUpdate / 1000f - firstBeatMusicTime) * (currentTempo / 60f);


            // ★★★ 廣播與舊版 Listener 相同的事件
            BeatEventInfo info = new BeatEventInfo
            {
                bar = data.bar,
                beat = data.beat,
                globalBeat = GlobalBeatIndex,
                tempo = currentTempo,
                timeSigUpper = timeSigUpper,
                timeSigLower = timeSigLower
            };

            OnGlobalBeat?.Invoke(GlobalBeatIndex);    // 玩家、怪物、UI、技能都會用到
            OnBarBeat?.Invoke(data.bar, data.beat);
            OnBeatInfo?.Invoke(info);
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

            // 播放特效與音效
            SpawnPerfectEffect();

            return true;
        }

        else
        {
            SpawnMissText();
            result = Judge.Miss;
            return false;
        }
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
