using UnityEngine;

public class BeatScale : MonoBehaviour
{
    [Header("縮放設定")]
    public Vector3 baseScale = new Vector3(0.15f, 0.15f, 0.15f);
    public float peakMultiplier = 1.3f;       // 節拍瞬間最大放大倍數
    public float holdDuration = 0.05f;        // 保持放大時間
    public float returnSpeed = 8f;            // 回復速度

    private bool isHolding;
    private float holdTimer;

    void OnEnable()
    {
        transform.localScale = baseScale;
        BeatManager.OnBeat += OnBeat;
    }

    void OnDisable()
    {
        BeatManager.OnBeat -= OnBeat;
    }

    void Update()
    {
        if (isHolding)
        {
            holdTimer -= Time.unscaledDeltaTime;
            if (holdTimer <= 0f)
            {
                isHolding = false;
            }
        }
        else
        {
            // 節拍結束後快速縮回
            transform.localScale = Vector3.Lerp(transform.localScale, baseScale, Time.unscaledDeltaTime * returnSpeed);
        }
    }

    private void OnBeat()
    {
        // 節拍瞬間強化「彈起」感
        transform.localScale = baseScale * peakMultiplier;
        isHolding = true;
        holdTimer = holdDuration;
    }
}
