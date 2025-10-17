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
    public Transform hitPoint;

    private AudioSource musicSource;
    private double offsetSamples;
    private BeatUI persistentBeat;
    private bool isReady = false;

    // ★ 全域主拍事件（供 BeatScale、BeatJudge 等使用）
    public static event Action OnBeat;

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
                BeatManager.InvokeBeat();
            }
        }
    }
}
