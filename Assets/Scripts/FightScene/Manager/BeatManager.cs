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
    [Tooltip("����`�� Prefab")]
    public GameObject beatPrefabLeft;    // �� �s�W
    [Tooltip("�k��`�� Prefab")]
    public GameObject beatPrefabRight;   // �� �s�W
    [Tooltip("���߰{���Ρ]Persistent Beat�^Prefab")]
    public GameObject beatCenterPrefab;  // �� �� beatPrefab ��W
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
        yield return new WaitUntil(() => MusicManager.Instance != null);
        yield return new WaitUntil(() => MusicManager.Instance.GetComponent<AudioSource>()?.clip != null);

        musicSource = MusicManager.Instance.GetComponent<AudioSource>();
        offsetSamples = musicSource.clip.frequency * startDelay;

        // �ͦ����߰{�� Persistent BeatUI
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

    public void MainBeatTrigger()
    {
        persistentBeat?.OnBeat();
        InvokeBeat();
    }

    public static void InvokeBeat()
    {
        OnBeat?.Invoke();
    }

    public float GetInterval() => 60f / bpm;

    public void TriggerBeat()
    {
        persistentBeat?.OnBeat();
        OnBeat?.Invoke();
    }

    // �� �ק�G�ǤJ prefab �Ѽ�
    private void SpawnBeatUI(GameObject prefab, RectTransform spawnPoint)
    {
        if (prefab == null || hitPoint == null || spawnPoint == null)
            return;

        // ���ͦ��A�����w������
        GameObject beatObj = Instantiate(prefab);
        beatObj.name = "BeatUI(Flying_" + prefab.name + ")";

        // �� ����G���s�]���l����ɡA���O�d�@�ɮy��
        beatObj.transform.SetParent(hitPoint.parent, false);

        // �� �j��]�Y��P��m
        RectTransform rect = beatObj.GetComponent<RectTransform>();
        rect.localScale = Vector3.one; // �o�˴N���|�Q��j
        rect.anchoredPosition = spawnPoint.anchoredPosition;

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
