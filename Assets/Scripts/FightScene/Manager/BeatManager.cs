using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System;
using System.Collections;

public class BeatManager : MonoBehaviour
{
    public static BeatManager Instance { get; private set; }

    [Header("BPM �]�w")]
    public float bpm = 145f;
    public float startDelay = 0f;

    [Header("�`��h�ų]�w")]
    [SerializeField] private IntervalFix[] intervals;

    [Header("UI �]�w")]
    public GameObject beatPrefabLeft;
    public GameObject beatPrefabRight;
    public GameObject beatCenterPrefab;
    public RectTransform hitPoint;

    private AudioSource musicSource;
    private double offsetSamples;
    private BeatUI persistentBeat;
    private bool isReady = false;

    public static event Action OnBeat;

    [Header("Beat UI ���ʳ]�w")]
    public float beatTravelTime = 0.8f;
    public float visualLeadTime = 0.08f;
    public RectTransform leftSpawnPoint;
    public RectTransform rightSpawnPoint;

    private int lastSpawnBeatIndex = -1;

    [Header("������")]
    public int beatsPerMeasure = 4;
    public int currentBeatIndex = 0;
    public int currentMeasure = 1;

    [Header("�Ϭq��ܳ]�w")]
    public Text sectionText; // �� ���w Text (Legacy) ����
    private int[] sectionBeats = { 4, 4, 4, 4, 4, 4, 4, 4 }; // �C�ӰϬq����� (�@32��)
    private string[] sectionNames = {
        "���a�^�X - ���q����1",
        "���a�^�X - ���q����2",
        "���a�^�X - ���q����3",
        "���a�^�X - Combo�ɶ�",
        "���a�^�X - Combo�ޯ�ʵe",
        "���a�^�X - ���ഫ",
        "�ĤH�^�X - ����A",
        "�ĤH�^�X - ����B"
    };
    private int totalBeatsInCycle; // 32
    private int currentSectionIndex = 0;

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

        // �p���Ӵ`���`���
        foreach (int b in sectionBeats)
            totalBeatsInCycle += b;

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

    // �� �C��Ĳ�o
    public void MainBeatTrigger()
    {
        persistentBeat?.OnBeat();
        InvokeBeat();

        // ��s������
        currentBeatIndex++;

        if ((currentBeatIndex - 1) % beatsPerMeasure == 0 && currentBeatIndex > 1)
            currentMeasure++;

        // �� ���o��e�`����ơ]1~32�^
        int beatInCycle = ((currentBeatIndex - 1) % totalBeatsInCycle) + 1;

        // �� ��X�ثe�b���ӰϬq
        int sum = 0;
        for (int i = 0; i < sectionBeats.Length; i++)
        {
            sum += sectionBeats[i];
            if (beatInCycle <= sum)
            {
                currentSectionIndex = i;
                break;
            }
        }

        // �� �p��ӰϬq���ĴX��
        int sectionStartBeat = 0;
        for (int j = 0; j < currentSectionIndex; j++)
            sectionStartBeat += sectionBeats[j];
        int beatInSection = beatInCycle - sectionStartBeat;

        // �� ��s Text (Legacy)
        if (sectionText)
        {
            sectionText.text = $"{sectionNames[currentSectionIndex]} | �� {beatInSection} / {sectionBeats[currentSectionIndex]}";
        }

        // Console Debug (��K����)
        Debug.Log($"{sectionNames[currentSectionIndex]} | �� {beatInSection} / {sectionBeats[currentSectionIndex]}");
    }

    public static void InvokeBeat() => OnBeat?.Invoke();
    public float GetInterval() => 60f / bpm;

    public void TriggerBeat()
    {
        persistentBeat?.OnBeat();
        OnBeat?.Invoke();
    }

    private void SpawnBeatUI(GameObject prefab, RectTransform spawnPoint)
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
