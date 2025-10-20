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

    // �� �����ƥ�]BattleManager�BBeatJudge���i�q�\�^
    public static event Action OnBeat;

    [Header("Beat UI ���ʳ]�w")]
    public float beatTravelTime = 0.8f;
    public float visualLeadTime = 0.08f;
    public RectTransform leftSpawnPoint;
    public RectTransform rightSpawnPoint;

    private int lastSpawnBeatIndex = -1;

    [Header("��Ƴ]�w")]
    public int beatsPerMeasure = 4;      // �C�p�`4��
    public int currentBeatIndex = 0;     // �`��p��
    public int currentBeatInCycle = 0;   // ��e�p�`��]1~4�^

    [Header("������")]
    public Text beatText; // ��ܡu�� X / 4�v

    // �� �s�W�G�w���U�@��]��BattleManager�ϥΡ^
    [HideInInspector] public int predictedNextBeat = 1;

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
        // ���ݭ��֨ӷ��N��
        yield return new WaitUntil(() => MusicManager.Instance != null);
        yield return new WaitUntil(() => MusicManager.Instance.GetComponent<AudioSource>()?.clip != null);

        musicSource = MusicManager.Instance.GetComponent<AudioSource>();
        offsetSamples = musicSource.clip.frequency * startDelay;

        // �ͦ�����Beat UI
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
        Debug.Log("BeatManager Ready (BPM: " + bpm + ")");
    }

    private void Update()
    {
        if (!isReady || musicSource == null || !musicSource.isPlaying || musicSource.clip == null)
            return;

        if (musicSource.timeSamples < offsetSamples)
            return;

        // �`���ˬd
        foreach (IntervalFix interval in intervals)
        {
            double sampledTime = (musicSource.timeSamples - offsetSamples) /
                                 (musicSource.clip.frequency * interval.GetIntervalLength(bpm));
            interval.CheckForNewInterval(sampledTime);
        }

        // ���e�ͦ��U�@��UI
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

            // �� �אּ����]�w predictedNextBeat
            StartCoroutine(UpdatePredictedBeatDelayed(nextBeatIndex));

            // Debug.Log($"[Predicted] �U�@��N�O�� {predictedNextBeat} ��");
        }
    }

    private IEnumerator UpdatePredictedBeatDelayed(int nextBeatIndex)
    {
        // ����ܩ��I�Y�N��F���ߦA��s�A�T�O�����e�@��
        float delay = Mathf.Max(0f, beatTravelTime - visualLeadTime);
        yield return new WaitForSecondsRealtime(delay);

        predictedNextBeat = ((nextBeatIndex - 1) % beatsPerMeasure) + 1;
        // Debug.Log($"[Predicted] �u����s���� {predictedNextBeat} �� (���� {delay:F2}s)");
    }



    // ============================================================
    // �C��Ĳ�o�޿�]�� IntervalFix �ƥ�I�s�^
    // ============================================================
    private bool isBeatLocked = false;

    public void MainBeatTrigger()
    {
        if (isBeatLocked) return;
        StartCoroutine(BeatLock());

        persistentBeat?.OnBeat();
        InvokeBeat();

        currentBeatIndex++;
        currentBeatInCycle = ((currentBeatIndex - 1) % beatsPerMeasure) + 1;

        if (beatText != null)
            beatText.text = "�� " + currentBeatInCycle + " / " + beatsPerMeasure;

        Debug.Log($"�� {currentBeatInCycle} �� / {beatsPerMeasure}");
    }

    private IEnumerator BeatLock()
    {
        isBeatLocked = true;
        yield return null; // ���@�V��Ѱ�
        isBeatLocked = false;
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

        // �� ��ܲ�4�笰����
        //int nextBeatInCycle = ((lastSpawnBeatIndex) % beatsPerMeasure) + 1;
        //if (nextBeatInCycle == beatsPerMeasure)
        //{
        //    var img = beatObj.GetComponent<Image>();
        //    if (img != null)
        //        img.color = Color.red;
        //}
    }
}

[System.Serializable]
public class IntervalFix
{
    [Tooltip("����A�Ҧp 1=����A2=�K����A4=�Q������")]
    [SerializeField] private float step = 1f;

    [Tooltip("�Ӽh�`��Ĳ�o���ƥ�]�i�� UI �ʵe�B���ĵ��^")]
    [SerializeField] private UnityEvent trigger;

    [SerializeField] private bool enableTrigger = false;

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
            if (enableTrigger)
                trigger.Invoke();
        }
    }
}
