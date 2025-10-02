using UnityEngine;
using System.Collections.Generic;

public class BeatManager : MonoBehaviour
{
    public static BeatManager Instance { get; private set; }

    [Header("BPM 設定")]
    public float bpm = 120f;                // 每分鐘拍數
    public int beatSubdivision = 1;         // 每拍分成幾等分（1=四分音符, 2=八分音符）

    [Header("UI 設定")]
    public GameObject beatPrefab;           // 節拍物件 Prefab
    public Transform spawnPoint;            // 生成起點（螢幕下方左側）
    public Transform hitPoint;              // 判定區（螢幕下方右側）

    [Header("移動設定")]
    public float travelTime = 2.0f;         // 從生成點到判定點的移動時間（秒）

    private float beatInterval;             // 每拍的間隔秒數
    private float nextBeatTime;             // 下一拍的時間點

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
        nextBeatTime = 0f;
    }

    private void Update()
    {
        float musicTime = MusicManager.Instance.GetMusicTime();

        // 檢查是否到生成節拍的時間
        if (musicTime >= nextBeatTime)
        {
            SpawnBeat(nextBeatTime);
            nextBeatTime += beatInterval;
        }

        // 更新所有節拍位置
        UpdateBeats(musicTime);
    }

    private void SpawnBeat(float noteTime)
    {
        if (beatPrefab == null || spawnPoint == null || hitPoint == null) return;

        GameObject beatObj = Instantiate(beatPrefab, spawnPoint.position, Quaternion.identity, transform);

        BeatUI beatUI = beatObj.AddComponent<BeatUI>();
        beatUI.Init(noteTime, spawnPoint.position, hitPoint.position, travelTime);

        activeBeats.Add(beatObj);
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
}
