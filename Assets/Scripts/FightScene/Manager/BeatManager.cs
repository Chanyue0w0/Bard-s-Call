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
    public RectTransform leftSpawnPoint;
    public RectTransform rightSpawnPoint;


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

        // 生成 BeatUI
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

        // 用樣本為基準的節拍偵測
        foreach (IntervalFix interval in intervals)
        {
            double sampledTime = (musicSource.timeSamples - offsetSamples) /
                                 (musicSource.clip.frequency * interval.GetIntervalLength(bpm));
            interval.CheckForNewInterval(sampledTime);
        }
    }

    // 對外主拍觸發（給 UI 用）
    public void MainBeatTrigger()
    {
        persistentBeat?.OnBeat();
        InvokeBeat();
    }

    // ★ 新增這個方法供外部呼叫，安全觸發全域 OnBeat 事件
    public static void InvokeBeat()
    {
        OnBeat?.Invoke();
    }

    public float GetInterval() => 60f / bpm;


    public void TriggerBeat()
    {
        // 中心閃光（原本的 persistentBeat）
        persistentBeat?.OnBeat();

        // 從左右兩邊各生成一個飛行中的 BeatUI
        SpawnBeatUI(leftSpawnPoint);
        SpawnBeatUI(rightSpawnPoint);

        OnBeat?.Invoke();
    }

    private void SpawnBeatUI(RectTransform spawnPoint)
    {
        if (beatPrefab == null || hitPoint == null || spawnPoint == null)
            return;

        // 生成在同一 Canvas 下
        GameObject beatObj = Instantiate(beatPrefab, hitPoint.parent);
        beatObj.name = "BeatUI(Flying)";
        RectTransform rect = beatObj.GetComponent<RectTransform>();

        // 初始化飛行設定
        BeatUI beatUI = beatObj.GetComponent<BeatUI>() ?? beatObj.AddComponent<BeatUI>();
        beatUI.InitFly(spawnPoint, hitPoint, beatTravelTime);
    }

    private Coroutine scheduleCoroutine;  // ★ 新增：紀錄正在運作的協程

    public void ScheduleNextBeat()
    {
        // ★ 若已有一個預排節拍，則略過（防止重複）
        if (scheduleCoroutine != null)
            return;

        scheduleCoroutine = StartCoroutine(ScheduleBeatCoroutine());
    }

    private IEnumerator ScheduleBeatCoroutine()
    {
        float interval = GetInterval(); // 一拍長度（秒）
        float delay = interval - beatTravelTime;
        delay = Mathf.Max(0f, delay);

        yield return new WaitForSecondsRealtime(delay);

        // 提前生成 BeatUI
        SpawnBeatUI(leftSpawnPoint);
        SpawnBeatUI(rightSpawnPoint);

        yield return new WaitForSecondsRealtime(beatTravelTime);

        // 到正拍時觸發 OnBeat
        TriggerBeat();

        // ★ 拍結束，允許下一個節拍重新排程
        scheduleCoroutine = null;
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

            // ★ 若是主拍（step == 1）則透過 BeatManager 的 InvokeBeat() 通知全域事件
            if (Mathf.Approximately(step, 1f))
            {
                BeatManager.Instance?.TriggerBeat();   // ← 改這裡！
            }
            //if (Mathf.Approximately(step, 1f))
            //{
            //    BeatManager.Instance?.ScheduleNextBeat();  // 改這裡！
            //}


        }
    }

}
