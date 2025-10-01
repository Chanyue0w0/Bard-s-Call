using UnityEngine;
using System.Collections;

public class Explosion : MonoBehaviour
{
    [Tooltip("爆炸特效存在的時間 (秒)")]
    public float lifeTime = 1.5f;

    [Tooltip("是否使用不受 Time.timeScale 影響的時間")]
    public bool useUnscaledTime = false;

    private void OnEnable()
    {
        if (useUnscaledTime)
        {
            StartCoroutine(DestroyAfterUnscaledTime());
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
}
