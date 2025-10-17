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
    public Transform hitPoint;

    private AudioSource musicSource;
    private double offsetSamples;
    private BeatUI persistentBeat;
    private bool isReady = false;

    // �� ����D��ƥ�]�� BeatScale�BBeatJudge ���ϥΡ^
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

    private IEnumerator Start()
    {
        // ���� MusicManager �ǳƧ���
        yield return new WaitUntil(() => MusicManager.Instance != null);
        yield return new WaitUntil(() => MusicManager.Instance.GetComponent<AudioSource>()?.clip != null);

        musicSource = MusicManager.Instance.GetComponent<AudioSource>();
        offsetSamples = musicSource.clip.frequency * startDelay;

        // �ͦ� BeatUI
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

        // �μ˥�����Ǫ��`�簻��
        foreach (IntervalFix interval in intervals)
        {
            double sampledTime = (musicSource.timeSamples - offsetSamples) /
                                 (musicSource.clip.frequency * interval.GetIntervalLength(bpm));
            interval.CheckForNewInterval(sampledTime);
        }
    }

    // ��~�D��Ĳ�o�]�� UI �Ρ^
    public void MainBeatTrigger()
    {
        persistentBeat?.OnBeat();
        InvokeBeat();
    }

    // �� �s�W�o�Ӥ�k�ѥ~���I�s�A�w��Ĳ�o���� OnBeat �ƥ�
    public static void InvokeBeat()
    {
        OnBeat?.Invoke();
    }

    public float GetInterval() => 60f / bpm;
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

            // �� �Y�O�D��]step == 1�^�h�z�L BeatManager �� InvokeBeat() �q������ƥ�
            if (Mathf.Approximately(step, 1f))
            {
                BeatManager.InvokeBeat();
            }
        }
    }
}
