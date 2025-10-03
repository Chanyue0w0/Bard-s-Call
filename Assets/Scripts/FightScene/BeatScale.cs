using UnityEngine;

public class BeatScale : MonoBehaviour
{
    [Header("縮放設定")]
    public Vector3 baseScale = new Vector3(0.15f, 0.15f, 0.15f);
    public float beatScaleMultiplier = 1.2f;   // 每拍放大倍數
    public float scaleLerpSpeed = 6f;          // 平滑回復速度

    private Vector3 targetScale;

    void OnEnable()
    {
        transform.localScale = baseScale;
        targetScale = baseScale;
        // 訂閱 Beat 事件
        BeatManager.OnBeat += OnBeat;
    }

    void OnDisable()
    {
        // 取消訂閱
        BeatManager.OnBeat -= OnBeat;
    }

    void Update()
    {
        // 平滑縮放回去
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * scaleLerpSpeed);
    }

    private void OnBeat()
    {
        // 每拍觸發 → 瞬間放大，然後再漸漸縮回
        targetScale = baseScale * beatScaleMultiplier;
        transform.localScale = baseScale; // 重置為基礎大小，才有「彈起」感
    }
}
