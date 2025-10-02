using UnityEngine;
using System.Collections.Generic;

public class BeatManager : MonoBehaviour
{
    public static BeatManager Instance { get; private set; }

    [Header("BPM �]�w")]
    public float bpm = 120f;                // �C�������
    public int beatSubdivision = 1;         // �C������X�����]1=�|������, 2=�K�����š^

    [Header("UI �]�w")]
    public GameObject beatPrefab;           // �`�窫�� Prefab
    public Transform spawnPoint;            // �ͦ��_�I�]�ù��U�襪���^
    public Transform hitPoint;              // �P�w�ϡ]�ù��U��k���^

    [Header("���ʳ]�w")]
    public float travelTime = 2.0f;         // �q�ͦ��I��P�w�I�����ʮɶ��]��^

    private float beatInterval;             // �C�窺���j���
    private float nextBeatTime;             // �U�@�窺�ɶ��I

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

        // �ˬd�O�_��ͦ��`�窺�ɶ�
        if (musicTime >= nextBeatTime)
        {
            SpawnBeat(nextBeatTime);
            nextBeatTime += beatInterval;
        }

        // ��s�Ҧ��`���m
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
                // ��F�P�w�ϩιL�� �� ����
                Destroy(activeBeats[i]);
                activeBeats.RemoveAt(i);
            }
        }
    }
}
