using UnityEngine;

public class DebugTestSlime : MonoBehaviour
{
    [Header("左右微動參數")]
    [Tooltip("左右擺動的最大位移（公尺）")]
    public float amplitude = 0.05f;

    [Tooltip("擺動速度（每秒的角速度，越大越快）")]
    public float speed = 1.5f;

    [Tooltip("是否以自身座標系移動；否則以世界座標移動")]
    public bool useLocalSpace = true;

    [Tooltip("是否在啟用時加入隨機相位，避免多隻史萊姆同時同向擺動")]
    public bool randomizePhase = true;

    // 內部狀態
    private float phase = 0f;
    private Vector3 basePosLocal;
    private Vector3 basePosWorld;

    void OnEnable()
    {
        basePosLocal = transform.localPosition;
        basePosWorld = transform.position;
        phase = randomizePhase ? Random.Range(0f, Mathf.PI * 2f) : 0f;
    }

    void Update()
    {
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
    }

    // 在參數變更時即時更新基準點
    void OnValidate()
    {
        amplitude = Mathf.Max(0f, amplitude);
        speed = Mathf.Max(0f, speed);
    }
}
