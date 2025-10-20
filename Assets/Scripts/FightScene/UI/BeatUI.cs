using UnityEngine;
using System.Collections;

public class BeatUI : MonoBehaviour
{
    private RectTransform rect;

    [Header("縮放參數")]
    public float startScale = 0.8f;     // 起始縮放
    public float endScale = 1.3f;       // 到中心時縮放
    public float fadeOutTime = 0.05f;   // 抵達後停留再消失

    private Coroutine moveCoroutine;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    public void Init()
    {
        rect.localScale = Vector3.zero;
    }

    // 設定從起點飛向中心點
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

        // 抵達中心點
        rect.anchoredPosition = endPos;
        rect.localScale = Vector3.one * endScale;

        // ★ 在此時通知 BeatManager：實際拍點抵達！
        if (BeatManager.Instance != null)
            BeatManager.Instance.MainBeatTrigger();

        // 等待短暫時間後銷毀自己
        yield return new WaitForSecondsRealtime(fadeOutTime);
        Destroy(gameObject);
    }

    public void OnBeat()
    {
        // 中心Beat閃光效果可以在這裡加入動畫
    }
}
