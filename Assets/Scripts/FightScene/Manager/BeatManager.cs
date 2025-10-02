using UnityEngine;
using System.Collections.Generic;
using System; // 為了 Action

public class BeatManager : MonoBehaviour
{
    public static BeatManager Instance { get; private set; }

    [Header("BPM 設定")]
    public float bpm = 145f;                // 每分鐘拍數
    public int beatSubdivision = 1;         // 每拍分成幾等分（1=四分音符, 2=八分音符）

    [Header("UI 設定")]
    public GameObject beatPrefab;           // 節拍物件 Prefab
    public Transform spawnPoint;            // 生成起點（螢幕下方左側）
    public Transform hitPoint;              // 判定區（螢幕下方右側）
    public Transform endPoint;              // 消失區（螢幕下方右側）

    [Header("移動設定")]
    [Min(1.0f)]
    public float travelTime = 2.0f;



    private float beatInterval;             // 每拍的間隔秒數
    private float nextBeatTime;             // 下一拍的時間點

    // 新增事件：每次 SpawnBeat 的時候就算一個拍
    public static event Action OnBeat;

    private List<GameObject> activeBeats = new List<GameObject>();

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

    }


    private void Update()
    {
        float musicTime = MusicManager.Instance.GetMusicTime();

        // 這裡改成：當音樂時間超過 (noteTime - travelTime)，就生成 Beat
        if (musicTime >= nextBeatTime - travelTime)
        {
            SpawnBeat(nextBeatTime); // noteTime 是它應該命中的時間
            nextBeatTime += beatInterval;
        }

        UpdateBeats(musicTime);
    }


    private void SpawnBeat(float noteTime)
    {
        if (beatPrefab == null || spawnPoint == null || hitPoint == null || endPoint == null) return;

        // 生成在 spawnPoint 所在的 Canvas 下
        GameObject beatObj = Instantiate(beatPrefab, spawnPoint.parent);
        beatObj.name = "BeatUI(Clone)";

        // 強制把位置放到 spawnPoint 一樣的 anchoredPosition
        RectTransform beatRect = beatObj.GetComponent<RectTransform>();
        RectTransform spawnRect = spawnPoint.GetComponent<RectTransform>();
        RectTransform hitRect = hitPoint.GetComponent<RectTransform>();
        RectTransform endRect = endPoint.GetComponent<RectTransform>();

        beatRect.anchoredPosition = spawnRect.anchoredPosition;

        BeatUI beatUI = beatObj.GetComponent<BeatUI>();
        if (beatUI == null) beatUI = beatObj.AddComponent<BeatUI>();
        beatUI.Init(noteTime, spawnRect.anchoredPosition, endRect.anchoredPosition, travelTime);
        //Debug.Log($"SpawnBeat noteTime={noteTime}, BeatManager.travelTime={travelTime}");


        activeBeats.Add(beatObj);
        
         // ★ 在生成拍點時觸發事件（等同於音樂進入一個 Beat）
        OnBeat?.Invoke();
        //Debug.Log("BeatUI Spawned in Canvas!");
    }




    private void UpdateBeats(float musicTime)
    {
        for (int i = activeBeats.Count - 1; i >= 0; i--)
        {
            if (activeBeats[i] == null)
            {
                activeBeats.RemoveAt(i);
                continue;
            }

            BeatUI beatUI = activeBeats[i].GetComponent<BeatUI>();
            if (beatUI.UpdatePosition(musicTime))
            {
                // 抵達判定區或過期 → 移除
                Destroy(activeBeats[i]);
                activeBeats.RemoveAt(i);
            }
        }
    }

    // BeatManager.cs
    public BeatUI FindClosestBeat(float currentTime)
    {
        BeatUI closest = null;
        float minDelta = Mathf.Infinity;

        foreach (var beatObj in activeBeats)
        {
            if (beatObj == null) continue;

            BeatUI beat = beatObj.GetComponent<BeatUI>();
            float delta = Mathf.Abs(currentTime - beat.GetNoteTime());

            if (delta < minDelta)
            {
                minDelta = delta;
                closest = beat;
            }
        }
        return closest;
    }

    public void RemoveBeat(GameObject beatObj)
    {
        if (activeBeats.Contains(beatObj))
        {
            activeBeats.Remove(beatObj);
            Destroy(beatObj);
        }
    }


}
