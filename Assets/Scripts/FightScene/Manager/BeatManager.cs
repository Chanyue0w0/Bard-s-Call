using UnityEngine;
using System.Collections.Generic;
using System; // ���F Action

public class BeatManager : MonoBehaviour
{
    public static BeatManager Instance { get; private set; }

    [Header("BPM �]�w")]
    public float bpm = 145f;                // �C�������
    public int beatSubdivision = 1;         // �C������X�����]1=�|������, 2=�K�����š^

    [Header("UI �]�w")]
    public GameObject beatPrefab;           // �`�窫�� Prefab
    public Transform spawnPoint;            // �ͦ��_�I�]�ù��U�襪���^
    public Transform hitPoint;              // �P�w�ϡ]�ù��U��k���^
    public Transform endPoint;              // �����ϡ]�ù��U��k���^

    [Header("���ʳ]�w")]
    [Min(1.0f)]
    public float travelTime = 2.0f;



    private float beatInterval;             // �C�窺���j���
    private float nextBeatTime;             // �U�@�窺�ɶ��I

    // �s�W�ƥ�G�C�� SpawnBeat ���ɭԴN��@�ө�
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

        // �o�̧令�G���֮ɶ��W�L (noteTime - travelTime)�A�N�ͦ� Beat
        if (musicTime >= nextBeatTime - travelTime)
        {
            SpawnBeat(nextBeatTime); // noteTime �O�����өR�����ɶ�
            nextBeatTime += beatInterval;
        }

        UpdateBeats(musicTime);
    }


    private void SpawnBeat(float noteTime)
    {
        if (beatPrefab == null || spawnPoint == null || hitPoint == null || endPoint == null) return;

        // �ͦ��b spawnPoint �Ҧb�� Canvas �U
        GameObject beatObj = Instantiate(beatPrefab, spawnPoint.parent);
        beatObj.name = "BeatUI(Clone)";

        // �j����m��� spawnPoint �@�˪� anchoredPosition
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
        
         // �� �b�ͦ����I��Ĳ�o�ƥ�]���P�󭵼ֶi�J�@�� Beat�^
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
                // ��F�P�w�ϩιL�� �� ����
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
