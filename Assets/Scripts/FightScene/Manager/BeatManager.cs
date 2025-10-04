using UnityEngine;
using System;

public class BeatManager : MonoBehaviour
{
    public static BeatManager Instance { get; private set; }

    [Header("BPM 設定")]
    [Tooltip("每分鐘拍數")]
    public float bpm = 145f;

    [Tooltip("每拍細分數，例如 2=八分音符、4=十六分音符")]
    public int beatSubdivision = 1;

    [Tooltip("音樂播放後幾秒開始第一拍")]
    public float startDelay = 0f;

    [Header("UI 設定")]
    public GameObject beatPrefab;
    public Transform hitPoint;

    private double beatInterval;       // 每拍間隔（秒）
    private int beatCount = 0;         // 拍數計數（從 0 開始）
    private BeatUI persistentBeat;

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

    private void Start()
    {
        beatInterval = 60.0 / bpm / beatSubdivision;

        // 初始化 Persistent Beat UI
        if (beatPrefab != null && hitPoint != null)
        {
            GameObject beatObj = Instantiate(beatPrefab, hitPoint.parent);
            beatObj.name = "BeatUI(Persistent)";

            RectTransform beatRect = beatObj.GetComponent<RectTransform>();
            RectTransform hitRect = hitPoint.GetComponent<RectTransform>();
            beatRect.anchoredPosition = hitRect.anchoredPosition;

            persistentBeat = beatObj.GetComponent<BeatUI>();
            if (persistentBeat == null)
                persistentBeat = beatObj.AddComponent<BeatUI>();

            persistentBeat.Init();
        }
    }

    private void Update()
    {
        if (MusicManager.Instance == null || !MusicManager.Instance.IsPlaying())
            return;

        double musicTime = MusicManager.Instance.GetMusicTime();

        // 當前理論拍點時間
        double currentBeatTime = beatCount * beatInterval + startDelay;

        // 若時間已達該拍點 → 觸發
        while (musicTime >= currentBeatTime)
        {
            TriggerBeat();

            beatCount++;
            currentBeatTime = beatCount * beatInterval + startDelay;
        }
    }

    private void TriggerBeat()
    {
        persistentBeat?.OnBeat();
        OnBeat?.Invoke();
    }

    // ==============================
    // 對外介面
    // ==============================

    public BeatUI GetPersistentBeat()
    {
        return persistentBeat;
    }

    public float GetInterval()
    {
        return (float)beatInterval;
    }

    public float GetNextBeatTime()
    {
        return (float)(beatCount * beatInterval + startDelay);
    }

    public float GetPreviousBeatTime()
    {
        return (float)((beatCount - 1) * beatInterval + startDelay);
    }
}
