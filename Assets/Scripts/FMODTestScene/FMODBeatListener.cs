using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public class FMODBeatListener : MonoBehaviour
{
    public static FMODBeatListener Instance { get; private set; }

    [Header("FMOD 事件")]
    [Tooltip("主 BGM 事件")]
    public EventReference musicEvent;

    [Tooltip("每拍要播放的 SFX（可留空）")]
    public EventReference beatSFXEvent;

    private EventInstance musicInstance;
    private FMOD.Studio.EVENT_CALLBACK beatCallback;

    // ========================================
    // 內部狀態（由 FMOD callback 寫入、主執行緒讀取）
    // ========================================

    // 當前小節 / 小節內拍
    private static int s_currentBar = 0;
    private static int s_currentBeatInBar = 0;

    // 從開頭算的「全局第幾拍」，0 起算
    private static int s_globalBeatIndex = -1;

    // 節奏與拍號
    private static float s_tempo = 120f;  // 預設 120，FMOD 回呼後會更新
    private static int s_timeSigUpper = 4;
    private static int s_timeSigLower = 4;

    // 待處理的拍點資料（callback → Update）
    private struct BeatData
    {
        public int bar;
        public int beatInBar;
        public float tempo;
        public int tsUpper;
        public int tsLower;
        public int globalBeatIndex;
    }

    private static readonly Queue<BeatData> s_pendingBeats = new Queue<BeatData>();

    // ========================================
    // 對外公開的拍點資訊
    // ========================================

    // 完整的拍點資訊
    public struct BeatInfo
    {
        public int bar;            // 第幾小節（1 起算）
        public int beatInBar;      // 小節內第幾拍（1 起算）
        public int globalBeat;     // 全局第幾拍（0 起算）
        public float tempo;        // BPM
        public int timeSigUpper;   // 拍號分子
        public int timeSigLower;   // 拍號分母
    }

    // 每次拍點時觸發「全局第幾拍」
    public static event Action<int> OnGlobalBeat;

    // 每次拍點時觸發「第幾小節、第幾拍」
    public static event Action<int, int> OnBarBeat;

    // 每次拍點時觸發完整資訊
    public static event Action<BeatInfo> OnBeatInfo;

    // 方便外部查詢目前狀態
    public int CurrentBar => s_currentBar;
    public int CurrentBeatInBar => s_currentBeatInBar;
    public int GlobalBeatIndex => s_globalBeatIndex;
    public float Tempo => s_tempo;
    public int TimeSigUpper => s_timeSigUpper;
    public int TimeSigLower => s_timeSigLower;
    public float SecondsPerBeat => (s_tempo > 0f) ? 60f / s_tempo : 0f;

    // ========================================
    // 拍點排程（幾拍後做某事）
    // ========================================

    private class ScheduledAction
    {
        public int targetBeat;
        public Action action;
    }

    private static readonly List<ScheduledAction> s_scheduledActions = new List<ScheduledAction>();

    /// <summary>
    /// 在「指定全局拍」觸發 Action
    /// 例如：ScheduleAtBeat( GlobalBeatIndex + 4, () => Attack() );
    /// </summary>
    public void ScheduleAtBeat(int targetGlobalBeat, Action action)
    {
        if (action == null)
            return;

        var item = new ScheduledAction
        {
            targetBeat = targetGlobalBeat,
            action = action
        };
        s_scheduledActions.Add(item);
    }

    /// <summary>
    /// 從「現在」起算，幾拍後觸發 Action
    /// 例如：ScheduleAfterBeats(3, () => DoAttack());
    /// </summary>
    public void ScheduleAfterBeats(int beatsFromNow, Action action)
    {
        if (beatsFromNow < 0) beatsFromNow = 0;
        int target = GlobalBeatIndex + beatsFromNow;
        ScheduleAtBeat(target, action);
    }

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
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // 建立 BGM 實例
        musicInstance = RuntimeManager.CreateInstance(musicEvent);

        // 綁定 FMOD 的拍點回呼
        beatCallback = new FMOD.Studio.EVENT_CALLBACK(BeatEventCallback);
        musicInstance.setCallback(beatCallback, EVENT_CALLBACK_TYPE.TIMELINE_BEAT);

        // 開始播放 BGM
        musicInstance.start();

        // 可提前 release，FMOD 會在播放完後真正釋放
        musicInstance.release();
    }

    void Update()
    {
        // 處理從 callback 丟來的拍點事件（保證在主執行緒）
        ProcessPendingBeats();
    }

    private void OnDestroy()
    {
        // 場景切換時記得停掉 BGM（如果你要跨場景 loop，也可以不要停）
        if (musicInstance.isValid())
        {
            musicInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            // 不需要再 release，Start 時已經 release 過
        }

        if (Instance == this)
            Instance = null;
    }

    // ========================================
    // 將 callback 的資料丟到主執行緒處理
    // ========================================

    private void ProcessPendingBeats()
    {
        while (true)
        {
            BeatData data;

            lock (s_pendingBeats)
            {
                if (s_pendingBeats.Count == 0)
                    break;

                data = s_pendingBeats.Dequeue();
            }

            // 更新目前的全域狀態
            s_currentBar = data.bar;
            s_currentBeatInBar = data.beatInBar;
            s_tempo = data.tempo;
            s_timeSigUpper = data.tsUpper;
            s_timeSigLower = data.tsLower;
            s_globalBeatIndex = data.globalBeatIndex;

            // 每拍要播的 SFX（可選）
            if (!beatSFXEvent.IsNull)
            {
                RuntimeManager.PlayOneShot(beatSFXEvent);
            }

            // 建立 BeatInfo 給事件用
            BeatInfo info = new BeatInfo
            {
                bar = s_currentBar,
                beatInBar = s_currentBeatInBar,
                globalBeat = s_globalBeatIndex,
                tempo = s_tempo,
                timeSigUpper = s_timeSigUpper,
                timeSigLower = s_timeSigLower
            };

            // 依序觸發事件
            OnGlobalBeat?.Invoke(info.globalBeat);
            OnBarBeat?.Invoke(info.bar, info.beatInBar);
            OnBeatInfo?.Invoke(info);

            // 處理排程（例如：3 拍後攻擊）
            ProcessScheduledActions(info.globalBeat);
        }
    }

    private void ProcessScheduledActions(int currentGlobalBeat)
    {
        for (int i = s_scheduledActions.Count - 1; i >= 0; i--)
        {
            var item = s_scheduledActions[i];
            if (item.targetBeat <= currentGlobalBeat)
            {
                try
                {
                    item.action?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[FMODBeatListener] Scheduled action error: {e}");
                }
                s_scheduledActions.RemoveAt(i);
            }
        }
    }

    // ========================================
    // FMOD 拍點回呼（在 FMOD 內部 thread 執行）
    // ========================================

    [AOT.MonoPInvokeCallback(typeof(FMOD.Studio.EVENT_CALLBACK))]
    private static FMOD.RESULT BeatEventCallback(
        EVENT_CALLBACK_TYPE type,
        IntPtr eventInstance,
        IntPtr parameters)
    {
        if (type != EVENT_CALLBACK_TYPE.TIMELINE_BEAT)
            return FMOD.RESULT.OK;

        // 取出 FMOD 傳來的拍點資訊
        var beatProps = (TIMELINE_BEAT_PROPERTIES)
            Marshal.PtrToStructure(parameters, typeof(TIMELINE_BEAT_PROPERTIES));

        // 計數「全局第幾拍」
        int newGlobalBeatIndex = s_globalBeatIndex + 1;

        BeatData data = new BeatData
        {
            bar = beatProps.bar,
            beatInBar = beatProps.beat,
            tempo = beatProps.tempo,
            tsUpper = beatProps.timesignatureupper,
            tsLower = beatProps.timesignaturelower,
            globalBeatIndex = newGlobalBeatIndex
        };

        // 丟進佇列，留給主執行緒的 Update 使用
        lock (s_pendingBeats)
        {
            s_pendingBeats.Enqueue(data);
        }

        return FMOD.RESULT.OK;
    }

    // ========================================
    // 一些小工具（之後技能、蓄力會很常用到）
    // ========================================

    /// <summary>
    /// 回傳：從 startBeat 到現在經過了幾拍（可用來判斷技能是否已經持續 N 拍）
    /// </summary>
    public int BeatsSince(int startGlobalBeat)
    {
        return GlobalBeatIndex - startGlobalBeat;
    }

    /// <summary>
    /// 回傳：從現在起算，offset 拍之後的全局拍 index
    /// </summary>
    public int GetBeatAfter(int offsetBeats)
    {
        return GlobalBeatIndex + offsetBeats;
    }
}
