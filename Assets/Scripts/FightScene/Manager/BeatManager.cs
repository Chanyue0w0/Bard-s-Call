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
    [Tooltip("左邊節拍 Prefab")]
    public GameObject beatPrefabLeft;    // ★ 新增
    [Tooltip("右邊節拍 Prefab")]
    public GameObject beatPrefabRight;   // ★ 新增
    [Tooltip("中心閃光用（Persistent Beat）Prefab")]
    public GameObject beatCenterPrefab;  // ★ 原 beatPrefab 改名
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
        yield return new WaitUntil(() => MusicManager.Instance != null);
        yield return new WaitUntil(() => MusicManager.Instance.GetComponent<AudioSource>()?.clip != null);

        musicSource = MusicManager.Instance.GetComponent<AudioSource>();
        offsetSamples = musicSource.clip.frequency * startDelay;

        // 生成中心閃光 Persistent BeatUI
        if (beatCenterPrefab && hitPoint)
        {
            GameObject beatObj = Instantiate(beatCenterPrefab, hitPoint.parent);
            beatObj.name = "BeatUI(Persistent)";
            RectTransform beatRect = beatObj.GetComponent<RectTransform>();
            beatRect.anchoredPosition = hitPoint.anchoredPosition;

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

        foreach (IntervalFix interval in intervals)
        {
            double sampledTime = (musicSource.timeSamples - offsetSamples) /
                                 (musicSource.clip.frequency * interval.GetIntervalLength(bpm));
            interval.CheckForNewInterval(sampledTime);
        }

        PreRollSpawnForNextBeat();
    }

    private void PreRollSpawnForNextBeat()
    {
        if ((beatPrefabLeft == null && beatPrefabRight == null) || hitPoint == null ||
            (leftSpawnPoint == null && rightSpawnPoint == null))
            return;

        var clip = musicSource.clip;
        int freq = clip.frequency;
        double samplesPerBeat = freq * GetInterval();
        double beatFloat = (musicSource.timeSamples - offsetSamples) / samplesPerBeat;
        int nextBeatIndex = Mathf.FloorToInt((float)beatFloat) + 1;
        double nextBeatSample = offsetSamples + nextBeatIndex * samplesPerBeat;
        double timeToNextBeat = (nextBeatSample - musicSource.timeSamples) / freq;

        double adjustedTravelTime = Mathf.Max(0f, beatTravelTime - visualLeadTime);

        if (timeToNextBeat <= adjustedTravelTime && nextBeatIndex != lastSpawnBeatIndex)
        {
            if (leftSpawnPoint && beatPrefabLeft)
                SpawnBeatUI(beatPrefabLeft, leftSpawnPoint);

            if (rightSpawnPoint && beatPrefabRight)
                SpawnBeatUI(beatPrefabRight, rightSpawnPoint);

            lastSpawnBeatIndex = nextBeatIndex;
        }
    }

    public void MainBeatTrigger()
    {
        persistentBeat?.OnBeat();
        InvokeBeat();
    }

    public static void InvokeBeat()
    {
        OnBeat?.Invoke();
    }

    public float GetInterval() => 60f / bpm;

    public void TriggerBeat()
    {
        persistentBeat?.OnBeat();
        OnBeat?.Invoke();
    }

    // ★ 修改：傳入 prefab 參數
    private void SpawnBeatUI(GameObject prefab, RectTransform spawnPoint)
    {
        if (prefab == null || hitPoint == null || spawnPoint == null)
            return;

        // 先生成，不指定父物件
        GameObject beatObj = Instantiate(prefab);
        beatObj.name = "BeatUI(Flying_" + prefab.name + ")";

        // ★ 關鍵：重新設為子物件時，不保留世界座標
        beatObj.transform.SetParent(hitPoint.parent, false);

        // ★ 強制重設縮放與位置
        RectTransform rect = beatObj.GetComponent<RectTransform>();
        rect.localScale = Vector3.one; // 這樣就不會被放大
        rect.anchoredPosition = spawnPoint.anchoredPosition;

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
