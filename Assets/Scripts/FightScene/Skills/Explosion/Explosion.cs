using UnityEngine;
using System.Collections;

public class Explosion : MonoBehaviour
{
    [Header("Explosion Settings")]

    [SerializeField, Tooltip("�z���S�Ħs�b���ɶ� (��)")]
    private float lifeTime = 1.5f;

    [SerializeField, Tooltip("�O�_�ϥΤ��� Time.timeScale �v�T���ɶ�")]
    private bool useUnscaledTime = false;

    private bool isInitialized = false;
    private Coroutine lifeRoutine;

    private void OnEnable()
    {
        
    }

    private void Start()
    {
        // �Y���ѥ~���I�s Initialize()�A�۰ʥH�w�]�ѼƱҰ�
        if (!isInitialized)
        {
            AutoInitialize();
        }
    }

    // ��l�ơG�~���I�s�ɨϥ�
    public void Initialize()
    {
        if (isInitialized) return;
        isInitialized = true;

        StartLifeRoutine();
    }

    // �۰ʪ�l�ơG�����ϥΡ]OnEnable�ɱҰʡ^
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
        isInitialized = false;
    }

    // =========================================================
    // ���}�]�w�����]�ѥ~���{���ק�^
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
