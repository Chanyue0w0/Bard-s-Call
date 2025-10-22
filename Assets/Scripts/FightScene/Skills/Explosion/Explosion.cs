using UnityEngine;
using System.Collections;

public class Explosion : MonoBehaviour
{
    [Header("Explosion Settings")]

    [SerializeField, Tooltip("爆炸特效存在的時間 (秒)")]
    private float lifeTime = 1.5f;

    [SerializeField, Tooltip("是否使用不受 Time.timeScale 影響的時間")]
    private bool useUnscaledTime = false;

    private bool isInitialized = false;
    private Coroutine lifeRoutine;

    private void OnEnable()
    {
        // 不自動啟動，需外部呼叫 Initialize()
    }

    // 初始化：在外部設定參數後再呼叫
    public void Initialize()
    {
        if (isInitialized) return;
        isInitialized = true;

        if (useUnscaledTime)
        {
            lifeRoutine = StartCoroutine(DestroyAfterUnscaledTime());
        }
        else
        {
            Destroy(gameObject, lifeTime);
        }
    }

    private IEnumerator DestroyAfterUnscaledTime()
    {
        float elapsed = 0f;
        while (elapsed < lifeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        Destroy(gameObject);
    }

    private void OnDisable()
    {
        if (lifeRoutine != null)
        {
            StopCoroutine(lifeRoutine);
            lifeRoutine = null;
        }
        isInitialized = false;
    }

    // =========================================================
    // 公開設定介面（供外部程式修改）
    // =========================================================
    public void SetLifeTime(float time)
    {
        lifeTime = Mathf.Max(0f, time);
    }

    public void SetUseUnscaledTime(bool use)
    {
        useUnscaledTime = use;
    }

    public float GetLifeTime() => lifeTime;
    public bool IsUsingUnscaledTime() => useUnscaledTime;
}
