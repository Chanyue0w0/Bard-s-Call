using UnityEngine;
using System.Collections;

public class BeatJudge : MonoBehaviour
{
    [Header("�P�w�d�� (��)")]
    public float perfectRange = 0.05f;

    [Header("�S�� UI Prefab")]
    public GameObject beatHitLightUIPrefab; // Perfect �R���S��
    public RectTransform beatHitPointUI;    // �����I UI ����m (Canvas �U)

    [Header("�Y��ʵe�]�w")]
    public float scaleUpSize = 2f;
    public float normalSize = 1.1202f;
    public float animTime = 0.15f; // ��j�Y�p�U�۪��ɶ�

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

    // �ˬd�O�_���]²�檩�^
    public bool IsOnBeat()
    {
        float musicTime = MusicManager.Instance.GetMusicTime();
        BeatUI targetBeat = BeatManager.Instance.FindClosestBeat(musicTime);
        if (targetBeat == null) return false;

        PlayScaleAnim();

        float delta = Mathf.Abs(musicTime - targetBeat.GetNoteTime());
        bool perfect = delta <= perfectRange;

        if (perfect)
        {
            SpawnPerfectEffect();
        }

        return perfect;
    }

    private void SpawnPerfectEffect()
    {
        if (beatHitLightUIPrefab == null || beatHitPointUI == null) return;

        GameObject effect = Instantiate(beatHitLightUIPrefab, beatHitPointUI.parent);
        RectTransform effectRect = effect.GetComponent<RectTransform>();
        if (effectRect != null)
        {
            effectRect.anchoredPosition = beatHitPointUI.anchoredPosition;
        }

        Destroy(effect, 0.5f);
    }

    private void PlayScaleAnim()
    {
        if (beatHitPointUI == null) return;

        // �p�G���b�]�ª��ʵe�A������
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
        }
        scaleCoroutine = StartCoroutine(ScaleAnim());
    }

    private IEnumerator ScaleAnim()
    {
        Vector3 start = Vector3.one * normalSize;
        Vector3 up = Vector3.one * scaleUpSize;

        // ����j
        float t = 0;
        while (t < 1)
        {
            t += Time.unscaledDeltaTime / animTime; // �� unscaled�A�קK TimeScale=0
            beatHitPointUI.localScale = Vector3.Lerp(start, up, t);
            yield return null;
        }

        // �A�Y�^�h
        t = 0;
        while (t < 1)
        {
            t += Time.unscaledDeltaTime / animTime;
            beatHitPointUI.localScale = Vector3.Lerp(up, start, t);
            yield return null;
        }

        beatHitPointUI.localScale = start;
        scaleCoroutine = null;
    }
}
