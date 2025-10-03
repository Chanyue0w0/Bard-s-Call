using UnityEngine;
using System;

public class BeatManager : MonoBehaviour
{
    public static BeatManager Instance { get; private set; }

    [Header("BPM 設定")]
    public float bpm = 145f;
    public int beatSubdivision = 1;

    [Header("UI 設定")]
    public GameObject beatPrefab;
    public Transform hitPoint;

    private float beatInterval;
    private float nextBeatTime;
    private BeatUI persistentBeat; // ★ 永遠只有這一個

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
        beatInterval = 60f / bpm / beatSubdivision;
        nextBeatTime = 3f;

        // 開場生成一個常駐 BeatUI
        if (beatPrefab != null && hitPoint != null)
        {
            GameObject beatObj = Instantiate(beatPrefab, hitPoint.parent);
            beatObj.name = "BeatUI(Persistent)";

            RectTransform beatRect = beatObj.GetComponent<RectTransform>();
            RectTransform hitRect = hitPoint.GetComponent<RectTransform>();
            beatRect.anchoredPosition = hitRect.anchoredPosition;

            persistentBeat = beatObj.GetComponent<BeatUI>();
            if (persistentBeat == null) persistentBeat = beatObj.AddComponent<BeatUI>();
            persistentBeat.Init();
        }
    }

    private void Update()
    {
        float musicTime = MusicManager.Instance.GetMusicTime();

        if (musicTime >= nextBeatTime)
        {
            // 每到節拍 → 讓 BeatUI 閃動
            persistentBeat?.OnBeat();

            OnBeat?.Invoke();
            nextBeatTime += beatInterval;
        }
    }

    // ★ 新增：直接回傳唯一的 BeatUI
    public BeatUI GetPersistentBeat()
    {
        return persistentBeat;
    }
}
