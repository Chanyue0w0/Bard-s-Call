using UnityEngine;
using System;

public class BeatManager : MonoBehaviour
{
    public static BeatManager Instance { get; private set; }

    [Header("BPM �]�w")]
    [Tooltip("�C�������")]
    public float bpm = 145f;

    [Tooltip("�C��Ӥ��ơA�Ҧp 2=�K�����šB4=�Q��������")]
    public int beatSubdivision = 1;

    [Tooltip("���ּ����X��}�l�Ĥ@��")]
    public float startDelay = 0f;

    [Header("UI �]�w")]
    public GameObject beatPrefab;
    public Transform hitPoint;

    private double beatInterval;       // �C�綡�j�]��^
    private int beatCount = 0;         // ��ƭp�ơ]�q 0 �}�l�^
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

        // ��l�� Persistent Beat UI
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

        // ��e�z�ש��I�ɶ�
        double currentBeatTime = beatCount * beatInterval + startDelay;

        // �Y�ɶ��w�F�ө��I �� Ĳ�o
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
    // ��~����
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
