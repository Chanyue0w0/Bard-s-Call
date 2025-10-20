using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System;
using System.Collections;

public class BeatManager : MonoBehaviour
{
    public static BeatManager Instance { get; private set; }

    [Header("BPM 設定")]
    public float bpm = 145f;
    public float startDelay = 0f;

    [Header("節拍層級設定")]
    [SerializeField] private IntervalFix[] intervals;

    [Header("UI 設定")]
    public GameObject beatPrefabLeft;
    public GameObject beatPrefabRight;
    public GameObject beatCenterPrefab;
    public RectTransform hitPoint;

    private AudioSource musicSource;
    private double offsetSamples;
    private BeatUI persistentBeat;
    private bool isReady = false;

    public static event Action OnBeat;

    [Header("Beat UI 移動設定")]
    public float beatTravelTime = 0.8f;
    public float visualLeadTime = 0.08f;
    public RectTransform leftSpawnPoint;
    public RectTransform rightSpawnPoint;

    private int lastSpawnBeatIndex = -1;

    [Header("拍數顯示")]
    public int beatsPerMeasure = 4;
    public int currentBeatIndex = 0;
    public int currentMeasure = 1;

    [Header("區段顯示設定")]
    public Text sectionText; // ★ 指定 Text (Legacy) 物件
    private int[] sectionBeats = { 4, 4, 4, 4, 4, 4, 4, 4 }; // 每個區段的拍數 (共32拍)
    private string[] sectionNames = {
        "玩家回合 - 普通攻擊1",
        "玩家回合 - 普通攻擊2",
        "玩家回合 - 普通攻擊3",
        "玩家回合 - Combo時間",
        "玩家回合 - Combo技能動畫",
        "玩家回合 - 休息轉換",
        "敵人回合 - 攻擊A",
        "敵人回合 - 攻擊B"
    };
    private int totalBeatsInCycle; // 32
    private int currentSectionIndex = 0;

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

        // 計算整個循環總拍數
        foreach (int b in sectionBeats)
            totalBeatsInCycle += b;

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

    // ★ 每拍觸發
    public void MainBeatTrigger()
    {
        persistentBeat?.OnBeat();
        InvokeBeat();

        // 更新全域拍數
        currentBeatIndex++;

        if ((currentBeatIndex - 1) % beatsPerMeasure == 0 && currentBeatIndex > 1)
            currentMeasure++;

        // ★ 取得當前循環拍數（1~32）
        int beatInCycle = ((currentBeatIndex - 1) % totalBeatsInCycle) + 1;

        // ★ 找出目前在哪個區段
        int sum = 0;
        for (int i = 0; i < sectionBeats.Length; i++)
        {
            sum += sectionBeats[i];
            if (beatInCycle <= sum)
            {
                currentSectionIndex = i;
                break;
            }
        }

        // ★ 計算該區段內第幾拍
        int sectionStartBeat = 0;
        for (int j = 0; j < currentSectionIndex; j++)
            sectionStartBeat += sectionBeats[j];
        int beatInSection = beatInCycle - sectionStartBeat;

        // ★ 更新 Text (Legacy)
        if (sectionText)
        {
            sectionText.text = $"{sectionNames[currentSectionIndex]} | 拍 {beatInSection} / {sectionBeats[currentSectionIndex]}";
        }

        // Console Debug (方便測試)
        Debug.Log($"{sectionNames[currentSectionIndex]} | 拍 {beatInSection} / {sectionBeats[currentSectionIndex]}");
    }

    public static void InvokeBeat() => OnBeat?.Invoke();
    public float GetInterval() => 60f / bpm;

    public void TriggerBeat()
    {
        persistentBeat?.OnBeat();
        OnBeat?.Invoke();
    }

    private void SpawnBeatUI(GameObject prefab, RectTransform spawnPoint)
    {
        if (prefab == null || hitPoint == null || spawnPoint == null)
            return;

        GameObject beatObj = Instantiate(prefab);
        beatObj.name = "BeatUI(Flying_" + prefab.name + ")";
        beatObj.transform.SetParent(hitPoint.parent, false);

        RectTransform rect = beatObj.GetComponent<RectTransform>();
        rect.localScale = Vector3.one;
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
