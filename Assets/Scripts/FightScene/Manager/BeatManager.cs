using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections;

public class BeatManager : MonoBehaviour
{
    public static BeatManager Instance { get; private set; }

    [Header("BPM �]�w")]
    [Tooltip("�C�������")]
    public float bpm = 145f;

    [Tooltip("���ּ����X��}�l�Ĥ@�� (����)")]
    public float startDelay = 0f;

    [Header("�`��h�ų]�w")]
    [Tooltip("�i�]�w���P����h�A�Ҧp 1=����B2=�K����B4=�Q�����絥")]
    [SerializeField] private IntervalFix[] intervals;

    [Header("UI �]�w")]
    public GameObject beatPrefab;
    public RectTransform hitPoint;

    private AudioSource musicSource;
    private double offsetSamples;
    private BeatUI persistentBeat;
    private bool isReady = false;

    // �� ����D��ƥ�]�� BeatScale�BBeatJudge ���ϥΡ^
    public static event Action OnBeat;

    [Header("Beat UI ���ʳ]�w")]
    public float beatTravelTime = 0.8f; // BeatUI �q��t���줤�ߩһݮɶ�
    [Tooltip("�e���������v��ơA�Ψӭץ��ϩ�{�H (��ĳ 0.05~0.12)")]
    public float visualLeadTime = 0.08f;
    public RectTransform leftSpawnPoint;
    public RectTransform rightSpawnPoint;

    // �� �s�W�G�w���һݪ����A
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
        // ���� MusicManager �ǳƧ���
        yield return new WaitUntil(() => MusicManager.Instance != null);
        yield return new WaitUntil(() => MusicManager.Instance.GetComponent<AudioSource>()?.clip != null);

        musicSource = MusicManager.Instance.GetComponent<AudioSource>();
        offsetSamples = musicSource.clip.frequency * startDelay;

        // �ͦ����ߪ� Persistent BeatUI�]�u�t�d���߰{���^
        if (beatPrefab && hitPoint)
        {
            GameObject beatObj = Instantiate(beatPrefab, hitPoint.parent);
            beatObj.name = "BeatUI(Persistent)";
            RectTransform beatRect = beatObj.GetComponent<RectTransform>();
            RectTransform hitRect = hitPoint.GetComponent<RectTransform>();
            beatRect.anchoredPosition = hitRect.anchoredPosition;

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

        // 1) �`�簻���]�����A������������^
        foreach (IntervalFix interval in intervals)
        {
            double sampledTime = (musicSource.timeSamples - offsetSamples) /
                                 (musicSource.clip.frequency * interval.GetIntervalLength(bpm));
            interval.CheckForNewInterval(sampledTime);
        }

        // 2) �� �w���ͦ� BeatUI�]�b�U�@��e beatTravelTime ��ͦ��^
        PreRollSpawnForNextBeat();
    }

    // �� �w���G��Z���u�U�@��v���ɶ� <= beatTravelTime�A�N���ͦ��@������`��y
    private void PreRollSpawnForNextBeat()
    {
        if (beatPrefab == null || hitPoint == null || (leftSpawnPoint == null && rightSpawnPoint == null))
            return;

        var clip = musicSource.clip;
        int freq = clip.frequency;

        // �C�窺�˥���
        double samplesPerBeat = freq * GetInterval();

        // �ثe�w�g�L���u��ơ]�t�p�ơ^�v
        double beatFloat = (musicSource.timeSamples - offsetSamples) / samplesPerBeat;

        // �U�@�窺 index �P�˥���m
        int nextBeatIndex = Mathf.FloorToInt((float)beatFloat) + 1;
        double nextBeatSample = offsetSamples + nextBeatIndex * samplesPerBeat;

        // �Z�U�@�窺���
        double timeToNextBeat = (nextBeatSample - musicSource.timeSamples) / freq;

        // �� ���e���v�G���ͦ���w���A���@�I
        double adjustedTravelTime = Mathf.Max(0f, beatTravelTime - visualLeadTime);

        // �|�����o�� nextBeatIndex �ͦ��L�A�B�ɶ��w�i�J�w����
        if (timeToNextBeat <= adjustedTravelTime && nextBeatIndex != lastSpawnBeatIndex)
        {
            // �o�̧A�i�令�u�ͤ@��A�Υ���F�w�]����U�@��
            if (leftSpawnPoint) SpawnBeatUI(leftSpawnPoint);
            if (rightSpawnPoint) SpawnBeatUI(rightSpawnPoint);

            lastSpawnBeatIndex = nextBeatIndex; // �O���קK���ƥͦ�
        }
    }

    // ��~�D��Ĳ�o�]�� UI / �P�w �Ρ^�X �O���u��U����v�ߧYĲ�o�A���A�ͦ�����y
    public void MainBeatTrigger()
    {
        persistentBeat?.OnBeat(); // ���߰{��
        InvokeBeat();             // ��L�t�Ρ]�P�w/�ƭȡ^
    }

    // �w��Ĳ�o���� OnBeat �ƥ�
    public static void InvokeBeat()
    {
        OnBeat?.Invoke();
    }

    public float GetInterval() => 60f / bpm;

    // �� ���A�ͦ�����y�A�קK�P�������ͦ�
    public void TriggerBeat()
    {
        persistentBeat?.OnBeat();
        OnBeat?.Invoke();
    }

    private void SpawnBeatUI(RectTransform spawnPoint)
    {
        if (beatPrefab == null || hitPoint == null || spawnPoint == null)
            return;

        GameObject beatObj = Instantiate(beatPrefab, hitPoint.parent);
        beatObj.name = "BeatUI(Flying)";
        BeatUI beatUI = beatObj.GetComponent<BeatUI>() ?? beatObj.AddComponent<BeatUI>();
        beatUI.InitFly(spawnPoint, hitPoint, beatTravelTime);
    }
}

[System.Serializable]
public class IntervalFix
{
    [Tooltip("����A�Ҧp 1=����A2=�K����A4=�Q������")]
    [SerializeField] private float step = 1f;

    [Tooltip("�Ӽh�`��Ĳ�o���ƥ�]�i�� UI �ʵe�B���ĵ��^")]
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
