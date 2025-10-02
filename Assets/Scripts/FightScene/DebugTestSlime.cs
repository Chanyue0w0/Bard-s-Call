using UnityEngine;

public class DebugTestSlime : MonoBehaviour
{
    [Header("左右微動參數")]
    public float amplitude = 0.05f;
    public float speed = 1.5f;
    public bool useLocalSpace = true;
    public bool randomizePhase = true;

    [Header("節拍縮放參數")]
    public Vector3 baseScale = new Vector3(0.15f, 0.15f, 0.15f); // 初始大小
    public float beatScaleMultiplier = 1.2f; // 縮放倍數
    public float scaleLerpSpeed = 6f;       // 回復速度

    private float phase = 0f;
    private Vector3 basePosLocal;
    private Vector3 basePosWorld;

    private Vector3 targetScale;

    void OnEnable()
    {
        basePosLocal = transform.localPosition;
        basePosWorld = transform.position;
        phase = randomizePhase ? Random.Range(0f, Mathf.PI * 2f) : 0f;

        // 初始大小
        transform.localScale = baseScale;
        targetScale = baseScale;

        // 訂閱 Beat 事件（假設 BeatManager 有這個事件）
        BeatManager.OnBeat += OnBeat;
    }

    void OnDisable()
    {
        // 記得解除訂閱，避免錯誤
        BeatManager.OnBeat -= OnBeat;
    }

    void Update()
    {
        // 左右擺動
        float offsetX = Mathf.Sin((Time.unscaledTime + phase) * speed) * amplitude;

        if (useLocalSpace)
        {
            Vector3 p = basePosLocal;
            p.x += offsetX;
            transform.localPosition = p;
        }
        else
        {
            Vector3 p = basePosWorld;
            p.x += offsetX;
            transform.position = p;
        }

        // 平滑縮放回 baseScale
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * scaleLerpSpeed);
    }

    private void OnBeat()
    {
        // 每次拍點觸發時，讓大小瞬間變大一點
        targetScale = baseScale * beatScaleMultiplier;

        // 立即設回 baseScale，讓 Lerp 往大再縮回
        transform.localScale = baseScale;
    }

    void OnValidate()
    {
        amplitude = Mathf.Max(0f, amplitude);
        speed = Mathf.Max(0f, speed);
    }
}
