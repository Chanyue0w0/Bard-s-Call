using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections;

public class BeatManager : MonoBehaviour
{
    public static BeatManager Instance { get; private set; }

    [Header("BPM 設定")]
    [Tooltip("每分鐘拍數")]
    public float bpm = 145f;

    [Tooltip("音樂播放後幾秒開始第一拍 (延遲)")]
    public float startDelay = 0f;

    [Header("節拍層級設定")]
    [Tooltip("可設定不同分拍層，例如 1=全拍、2=八分拍、4=十六分拍等")]
    [SerializeField] private IntervalFix[] intervals;

    [Header("UI 設定")]
    public GameObject beatPrefab;
    public RectTransform hitPoint;

    private AudioSource musicSource;
    private double offsetSamples;
    private BeatUI persistentBeat;
    private bool isReady = false;

    // ★ 全域主拍事件（供 BeatScale、BeatJudge 等使用）
    public static event Action OnBeat;

    [Header("Beat UI 移動設定")]
    public float beatTravelTime = 0.8f; // BeatUI 從邊緣飛到中心所需時間
    [Tooltip("畫面提早補償秒數，用來修正反拍現象 (建議 0.05~0.12)")]
    public float visualLeadTime = 0.08f;
    public RectTransform leftSpawnPoint;
    public RectTransform rightSpawnPoint;

    // ★ 新增：預捲所需的狀態
    private int lastSpawnBeatIndex = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private IEnumerator Start()
    {
        // 等待 MusicManager 準備完成
        yield return new WaitUntil(() => MusicManager.Instance != null);
        yield return new WaitUntil(() => MusicManager.Instance.GetComponent<AudioSource>()?.clip != null);

        musicSource = MusicManager.Instance.GetComponent<AudioSource>();
        offsetSamples = musicSource.clip.frequency * startDelay;

        // 生成中心的 Persistent BeatUI（只負責中心閃光）
        if (beatPrefab && hitPoint)
        {
            GameObject beatObj = Instantiate(beatPrefab, hitPoint.parent);
            beatObj.name = "BeatUI(Persistent)";
            RectTransform beatRect = beatObj.GetComponent<RectTransform>();
            RectTransform hitRect = hitPoint.GetComponent<RectTransform>();
            beatRect.anchoredPosition = hitRect.anchoredPosition;

            persistentBeat = beatObj.GetComponent<BeatUI>() ?? beatObj.AddComponent<BeatUI>();
            persistentBeat.Init();
        }

        isReady = true;
        Debug.Log("BeatManager Ready (Intervals: " + intervals.Length + ")");
    }

    private void Update()
    {
        if (!isReady || musicSource == null || !musicSource.isPlaying || musicSource.clip == null)
            return;

        if (musicSource.timeSamples < offsetSamples)
            return;

        // 1) 節拍偵測（維持你原先的完美對拍）
        foreach (IntervalFix interval in intervals)
        {
            double sampledTime = (musicSource.timeSamples - offsetSamples) /
                                 (musicSource.clip.frequency * interval.GetIntervalLength(bpm));
            interval.CheckForNewInterval(sampledTime);
        }

        // 2) ★ 預捲生成 BeatUI（在下一拍前 beatTravelTime 秒生成）
        PreRollSpawnForNextBeat();
    }

    // ★ 預捲：當距離「下一拍」的時間 <= beatTravelTime，就先生成一次飛行節拍球
    private void PreRollSpawnForNextBeat()
    {
        if (beatPrefab == null || hitPoint == null || (leftSpawnPoint == null && rightSpawnPoint == null))
            return;

        var clip = musicSource.clip;
        int freq = clip.frequency;

        // 每拍的樣本數
        double samplesPerBeat = freq * GetInterval();

        // 目前已經過的「拍數（含小數）」
        double beatFloat = (musicSource.timeSamples - offsetSamples) / samplesPerBeat;

        // 下一拍的 index 與樣本位置
        int nextBeatIndex = Mathf.FloorToInt((float)beatFloat) + 1;
        double nextBeatSample = offsetSamples + nextBeatIndex * samplesPerBeat;

        // 距下一拍的秒數
        double timeToNextBeat = (nextBeatSample - musicSource.timeSamples) / freq;

        // ★ 提前補償：讓生成比預期再早一點
        double adjustedTravelTime = Mathf.Max(0f, beatTravelTime - visualLeadTime);

        // 尚未為這個 nextBeatIndex 生成過，且時間已進入預捲窗
        if (timeToNextBeat <= adjustedTravelTime && nextBeatIndex != lastSpawnBeatIndex)
        {
            // 這裡你可改成只生一邊，或交替；預設兩邊各一顆
            if (leftSpawnPoint) SpawnBeatUI(leftSpawnPoint);
            if (rightSpawnPoint) SpawnBeatUI(rightSpawnPoint);

            lastSpawnBeatIndex = nextBeatIndex; // 記錄避免重複生成
        }
    }

    // 對外主拍觸發（給 UI / 判定 用）— 保持「當下正拍」立即觸發，不再生成飛行球
    public void MainBeatTrigger()
    {
        persistentBeat?.OnBeat(); // 中心閃光
        InvokeBeat();             // 其他系統（判定/數值）
    }

    // 安全觸發全域 OnBeat 事件
    public static void InvokeBeat()
    {
        OnBeat?.Invoke();
    }

    public float GetInterval() => 60f / bpm;

    // ★ 不再生成飛行球，避免同拍雙重生成
    public void TriggerBeat()
    {
        persistentBeat?.OnBeat();
        OnBeat?.Invoke();
    }

    private void SpawnBeatUI(RectTransform spawnPoint)
    {
        if (beatPrefab == null || hitPoint == null || spawnPoint == null)
            return;

        GameObject beatObj = Instantiate(beatPrefab, hitPoint.parent);
        beatObj.name = "BeatUI(Flying)";
        BeatUI beatUI = beatObj.GetComponent<BeatUI>() ?? beatObj.AddComponent<BeatUI>();
        beatUI.InitFly(spawnPoint, hitPoint, beatTravelTime);
    }
}

[System.Serializable]
public class IntervalFix
{
    [Tooltip("分拍，例如 1=全拍，2=八分拍，4=十六分拍")]
    [SerializeField] private float step = 1f;

    [Tooltip("該層節拍觸發的事件（可掛 UI 動畫、音效等）")]
    [SerializeField] private UnityEvent trigger;

    private int lastInterval = -1;

    public float GetIntervalLength(float bpm)
    {
        return 60f / bpm * step;
    }

    public void CheckForNewInterval(double sample)
    {
        int currentInterval = Mathf.FloorToInt((float)sample);
        if (currentInterval != lastInterval)
        {
            lastInterval = currentInterval;
            trigger.Invoke();
        }
    }
}
