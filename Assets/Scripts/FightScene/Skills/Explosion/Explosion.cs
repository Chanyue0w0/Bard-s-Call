using UnityEngine;
using System.Collections;

public class Explosion : MonoBehaviour
{
    [Tooltip("�z���S�Ħs�b���ɶ� (��)")]
    public float lifeTime = 1.5f;

    [Tooltip("�O�_�ϥΤ��� Time.timeScale �v�T���ɶ�")]
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
