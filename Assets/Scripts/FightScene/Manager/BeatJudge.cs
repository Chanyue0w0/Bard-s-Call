using UnityEngine;
using System.Collections;

public class BeatJudge : MonoBehaviour
{
    [Header("�P�w�d��]�w (��)")]
    [Tooltip("���\�����h�֬����⧹��")]
    public float earlyRange = 0.03f;
    [Tooltip("���\����h�֬����⧹��")]
    public float lateRange = 0.07f;
    [Header("�P�w�ɶ����v (��)")]
    [Tooltip("���ȷ|���P�w���e�A��ĳ 0.03~0.12 ����")]
    public float judgeOffset = 0.08f;


    [Header("�S�ĻP UI")]
    public GameObject beatHitLightUIPrefab;
    public GameObject missTextPrefab;
    public RectTransform beatHitPointUI;

    [Header("�Y��ʵe�]�w")]
    public float scaleUpSize = 2f;
    public float normalSize = 1.12f;
    public float animTime = 0.15f;

    [Header("���ĳ]�w")]
    public AudioClip snapClip;
    private AudioSource audioSource;

    public static BeatJudge Instance { get; private set; }
    private Coroutine scaleCoroutine;

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
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    // ===============================================
    // �P�w�֤�
    // ===============================================
    public bool IsOnBeat()
    {
        if (BeatManager.Instance == null || MusicManager.Instance == null)
            return false;

        // �q MusicManager ���ɶ��A�ô��e�@�I�H���v���񩵿�
        float musicTime = MusicManager.Instance.GetMusicTime() - judgeOffset;


        // ����e�̪񪺩��I�ɶ�
        float prevBeat = BeatManager.Instance.GetPreviousBeatTime();
        float nextBeat = BeatManager.Instance.GetNextBeatTime();

        // �p��Z�����ө��I���
        float targetTime = (Mathf.Abs(musicTime - prevBeat) < Mathf.Abs(musicTime - nextBeat))
            ? prevBeat
            : nextBeat;

        float delta = musicTime - targetTime; // ���G����A�t�G���e

        // �e�\�~�t�d��
        bool perfect = (delta >= -earlyRange && delta <= lateRange);

        // �����Y��ʵe
        PlayScaleAnim();

        if (perfect)
        {
            SpawnPerfectEffect();

            if (audioSource != null && snapClip != null)
            {
                double playTime = AudioSettings.dspTime + 0.05; // ���e�Ƶ{ 10ms
                audioSource.clip = snapClip;
                audioSource.PlayScheduled(playTime);
            }

        }
        else
        {
            SpawnMissText();
        }

        return perfect;
    }

    // ===============================================
    // �S�����
    // ===============================================
    private void SpawnPerfectEffect()
    {
        if (beatHitLightUIPrefab == null || beatHitPointUI == null) return;

        GameObject effect = Instantiate(beatHitLightUIPrefab, beatHitPointUI.parent);
        RectTransform rect = effect.GetComponent<RectTransform>();
        if (rect != null)
            rect.anchoredPosition = beatHitPointUI.anchoredPosition;

        Destroy(effect, 0.5f);
    }

    private void SpawnMissText()
    {
        if (missTextPrefab == null || beatHitPointUI == null) return;

        GameObject missObj = Instantiate(missTextPrefab, beatHitPointUI.parent);
        RectTransform rect = missObj.GetComponent<RectTransform>();
        if (rect != null)
            rect.anchoredPosition = beatHitPointUI.anchoredPosition + new Vector2(0, 50f);

        Destroy(missObj, 0.3f);
    }

    // ===============================================
    // UI �ʵe
    // ===============================================
    private void PlayScaleAnim()
    {
        if (beatHitPointUI == null) return;

        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);

        scaleCoroutine = StartCoroutine(ScaleAnim());
    }

    private IEnumerator ScaleAnim()
    {
        Vector3 start = Vector3.one * normalSize;
        Vector3 up = Vector3.one * scaleUpSize;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / animTime;
            beatHitPointUI.localScale = Vector3.Lerp(start, up, t);
            yield return null;
        }

        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / animTime;
            beatHitPointUI.localScale = Vector3.Lerp(up, start, t);
            yield return null;
        }

        beatHitPointUI.localScale = start;
        scaleCoroutine = null;
    }
}
