using UnityEngine;
using System.Collections;

public class BeatUI : MonoBehaviour
{
    private RectTransform rect;

    [Header("縮放參數")]
    public float startScale = 3f;       // 初始放大值
    public float targetScale = 1.3f;    // 停留大小
    public float shrinkTime = 0.15f;    // 從3縮到1.3所需時間
    public float holdTime = 0.05f;      // 停留時間

    private Coroutine flashCoroutine;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    public void Init()
    {
        rect.localScale = Vector3.zero; // 初始隱藏
    }

    public void OnBeat()
    {
        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashAnim());
    }

    private IEnumerator FlashAnim()
    {
        // ★ 先設為起始大小 3
        rect.localScale = Vector3.one * startScale;

        // 3 → 1.3
        float t = 0;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / shrinkTime;
            rect.localScale = Vector3.Lerp(Vector3.one * startScale, Vector3.one * targetScale, t);
            yield return null;
        }

        // ★ 停留在 1.3
        rect.localScale = Vector3.one * targetScale;
        yield return new WaitForSecondsRealtime(holdTime);

        // ★ 瞬間消失
        rect.localScale = Vector3.zero;

        flashCoroutine = null;
    }
}
