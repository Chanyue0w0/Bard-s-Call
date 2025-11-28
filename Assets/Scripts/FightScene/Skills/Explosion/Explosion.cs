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
        
    }

    private void Start()
    {
        // 永遠誰來都重新初始化
        isInitialized = false;
        // 若未由外部呼叫 Initialize()，自動以預設參數啟動
        if (!isInitialized)
        {
            AutoInitialize();
        }
    }

    // 初始化：外部呼叫時使用
    public void Initialize()
    {
        if (isInitialized) return;
        isInitialized = true;

        StartLifeRoutine();
    }

    // 自動初始化：內部使用（OnEnable時啟動）
    private void AutoInitialize()
    {
        isInitialized = true;
        StartLifeRoutine();
    }

    private void StartLifeRoutine()
    {
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
