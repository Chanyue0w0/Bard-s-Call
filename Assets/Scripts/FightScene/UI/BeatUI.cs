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

    // �� �s�W�G�]�w�q�_�I���V�����I
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

        rect.anchoredPosition = endPos;
        rect.localScale = Vector3.one * endScale;

        yield return new WaitForSecondsRealtime(fadeOutTime);
        Destroy(gameObject);
    }

    public void OnBeat()
    {
        // �Y�Q�O�d���߰{�{�ĪG�i�d�ũΥ[�ʵe
    }
}
