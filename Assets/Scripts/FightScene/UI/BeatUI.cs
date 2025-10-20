using UnityEngine;
using System.Collections;

public class BeatUI : MonoBehaviour
{
    private RectTransform rect;

    [Header("�Y��Ѽ�")]
    public float startScale = 0.8f;     // �_�l�Y��
    public float endScale = 1.3f;       // �줤�߮��Y��
    public float fadeOutTime = 0.05f;   // ��F�ᰱ�d�A����

    private Coroutine moveCoroutine;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    public void Init()
    {
        rect.localScale = Vector3.zero;
    }

    // �]�w�q�_�I���V�����I
    public void InitFly(RectTransform startPoint, RectTransform targetPoint, float travelTime)
    {
        rect.anchoredPosition = startPoint.anchoredPosition;
        rect.localScale = Vector3.one * startScale;

        if (moveCoroutine != null)
            StopCoroutine(moveCoroutine);
        moveCoroutine = StartCoroutine(FlyToTarget(startPoint, targetPoint, travelTime));
    }

    private IEnumerator FlyToTarget(RectTransform start, RectTransform target, float travelTime)
    {
        Vector2 startPos = start.anchoredPosition;
        Vector2 endPos = target.anchoredPosition;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / travelTime;
            rect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            rect.localScale = Vector3.Lerp(Vector3.one * startScale, Vector3.one * endScale, t);
            yield return null;
        }

        // ��F�����I
        rect.anchoredPosition = endPos;
        rect.localScale = Vector3.one * endScale;

        // �� �b���ɳq�� BeatManager�G��ک��I��F�I
        if (BeatManager.Instance != null)
            BeatManager.Instance.MainBeatTrigger();

        // ���ݵu�Ȯɶ���P���ۤv
        yield return new WaitForSecondsRealtime(fadeOutTime);
        Destroy(gameObject);
    }

    public void OnBeat()
    {
        // ����Beat�{���ĪG�i�H�b�o�̥[�J�ʵe
    }
}
