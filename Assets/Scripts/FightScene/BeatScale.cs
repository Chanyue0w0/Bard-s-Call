using UnityEngine;

public class BeatScale : MonoBehaviour
{
    [Header("縮放設定")]
    public Vector3 baseScale = new Vector3(0.15f, 0.15f, 0.15f);
    public float peakMultiplier = 1.3f;   // 節拍瞬間最大放大倍數
    public float holdDuration = 0.05f;    // 保持放大時間
    public float returnSpeed = 8f;        // 回復速度

    private bool isHolding;
    private float holdTimer;

    void OnEnable()
    {
        transform.localScale = baseScale;

        // ★ 改用 FMOD 拍點事件
        FMODBeatListener.OnGlobalBeat += OnBeat;
        //（如果只想在每小節的拍子觸發，也可以改用 OnBarBeat）
    }

    void OnDisable()
    {
        FMODBeatListener.OnGlobalBeat -= OnBeat;
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
            // 節拍結束後快速縮回原本大小
            transform.localScale = Vector3.Lerp(transform.localScale, baseScale, Time.unscaledDeltaTime * returnSpeed);
        }
    }

    private void OnBeat(int globalBeatIndex)
    {
        // ★ FMOD真正每拍 callback → 在這邊執行彈跳縮放
        transform.localScale = baseScale * peakMultiplier;
        isHolding = true;
        holdTimer = holdDuration;
    }
}
