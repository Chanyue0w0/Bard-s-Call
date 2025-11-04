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

    // ★ 全域拍事件（BattleManager、BeatJudge等可訂閱）
    public static event Action OnBeat;

    [Header("Beat UI 移動設定")]
    public float beatTravelTime = 0.8f;
    public float visualLeadTime = 0.08f;
    public RectTransform leftSpawnPoint;
    public RectTransform rightSpawnPoint;

    [Header("Beat UI 顏色設定")]
    public Color normalBeatColor = Color.white;
    // 金黃色（RGB 255,215,0）
    public Color heavyBeatColor = new Color32(255, 215, 0, 255);


    private int lastSpawnBeatIndex = -1;

    [Header("拍數設定")]
    public int beatsPerMeasure = 4;      // 每小節4拍
    public int currentBeatIndex = 0;     // 總拍計數
    public int currentBeatInCycle = 0;   // 當前小節拍（1~4）

    [Header("拍數顯示")]
    public Text beatText; // 顯示「拍 X / 4」

    // ★ 新增：預測下一拍（供BattleManager使用）
    [HideInInspector] public int predictedNextBeat = 1;

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
        // 等待音樂來源就緒
        yield return new WaitUntil(() => MusicManager.Instance != null);
        yield return new WaitUntil(() => MusicManager.Instance.GetComponent<AudioSource>()?.clip != null);

        musicSource = MusicManager.Instance.GetComponent<AudioSource>();
        offsetSamples = musicSource.clip.frequency * startDelay;

        // 生成中心Beat UI
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
        Debug.Log("BeatManager Ready (BPM: " + bpm + ")");
    }

    private void Update()
    {
        if (!isReady || musicSource == null || !musicSource.isPlaying || musicSource.clip == null)
            return;

        if (musicSource.timeSamples < offsetSamples)
            return;

        // 節拍檢查
        foreach (IntervalFix interval in intervals)
        {
            double sampledTime = (musicSource.timeSamples - offsetSamples) /
                                 (musicSource.clip.frequency * interval.GetIntervalLength(bpm));
            interval.CheckForNewInterval(sampledTime);
        }

        // 提前生成下一拍UI
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
            //if (leftSpawnPoint && beatPrefabLeft)
            //    SpawnBeatUI(beatPrefabLeft, leftSpawnPoint);

            //if (rightSpawnPoint && beatPrefabRight)
            //    SpawnBeatUI(beatPrefabRight, rightSpawnPoint);
            if (leftSpawnPoint && beatPrefabLeft)
                SpawnBeatUI(beatPrefabLeft, leftSpawnPoint, nextBeatIndex);

            if (rightSpawnPoint && beatPrefabRight)
                SpawnBeatUI(beatPrefabRight, rightSpawnPoint, nextBeatIndex);


            lastSpawnBeatIndex = nextBeatIndex;

            // ★ 改為延後設定 predictedNextBeat
            StartCoroutine(UpdatePredictedBeatDelayed(nextBeatIndex));

            // Debug.Log($"[Predicted] 下一拍將是第 {predictedNextBeat} 拍");
        }
    }

    private IEnumerator UpdatePredictedBeatDelayed(int nextBeatIndex)
    {
        // 延遲至拍點即將抵達中心再更新，確保不提前一拍
        float delay = Mathf.Max(0f, beatTravelTime - visualLeadTime);
        yield return new WaitForSecondsRealtime(delay);

        predictedNextBeat = ((nextBeatIndex - 1) % beatsPerMeasure) + 1;
        // Debug.Log($"[Predicted] 真正更新為第 {predictedNextBeat} 拍 (延遲 {delay:F2}s)");
    }



    // ============================================================
    // 每拍觸發邏輯（由 IntervalFix 事件呼叫）
    // ============================================================
    private bool isBeatLocked = false;

    public void MainBeatTrigger()
    {
        if (isBeatLocked) return;
        StartCoroutine(BeatLock());

        persistentBeat?.OnBeat();
        InvokeBeat();

        // ★ 嘲諷效果倒數：每拍執行一次
        if (BattleEffectManager.Instance != null)
        {
            BattleEffectManager.Instance.TickTauntBeats();
        }


        currentBeatIndex++;
        currentBeatInCycle = ((currentBeatIndex - 1) % beatsPerMeasure) + 1;

        if (beatText != null)
            beatText.text = "拍 " + currentBeatInCycle + " / " + beatsPerMeasure;

        //Debug.Log($"第 {currentBeatInCycle} 拍 / {beatsPerMeasure}");
    }

    private IEnumerator BeatLock()
    {
        isBeatLocked = true;
        yield return null; // 等一幀後解除
        isBeatLocked = false;
    }

    public static void InvokeBeat() => OnBeat?.Invoke();
    public float GetInterval() => 60f / bpm;

    public void TriggerBeat()
    {
        persistentBeat?.OnBeat();
        OnBeat?.Invoke();
    }

    private void SpawnBeatUI(GameObject prefab, RectTransform spawnPoint, int nextBeatIndex)
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

        // 依拍數決定顏色：每小節的第 4 拍改為金黃色，其餘用一般色
        int nextBeatInCycle = ((nextBeatIndex - 1) % beatsPerMeasure) + 1;
        var img = beatObj.GetComponent<Image>() ?? beatObj.GetComponentInChildren<Image>(true);
        if (img != null)
        {
            img.color = (nextBeatInCycle == beatsPerMeasure) ? heavyBeatColor : normalBeatColor;
        }
    }

}

[System.Serializable]
public class IntervalFix
{
    [Tooltip("分拍，例如 1=全拍，2=八分拍，4=十六分拍")]
    [SerializeField] private float step = 1f;

    [Tooltip("該層節拍觸發的事件（可掛 UI 動畫、音效等）")]
    [SerializeField] private UnityEvent trigger;

    [SerializeField] private bool enableTrigger = false;

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
            if (enableTrigger)
                trigger.Invoke();
        }
    }
}
