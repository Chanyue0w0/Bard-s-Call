using UnityEngine;
using System.Collections;

public class BeatUI : MonoBehaviour
{
    private RectTransform rect;

    [Header("�Y��Ѽ�")]
    public float startScale = 3f;       // ��l��j��
    public float targetScale = 1.3f;    // ���d�j�p
    public float shrinkTime = 0.15f;    // �q3�Y��1.3�һݮɶ�
    public float holdTime = 0.05f;      // ���d�ɶ�

    private Coroutine flashCoroutine;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    public void Init()
    {
        rect.localScale = Vector3.zero; // ��l����
    }

    public void OnBeat()
    {
        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashAnim());
    }

    private IEnumerator FlashAnim()
    {
        // �� ���]���_�l�j�p 3
        rect.localScale = Vector3.one * startScale;

        // 3 �� 1.3
        float t = 0;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / shrinkTime;
            rect.localScale = Vector3.Lerp(Vector3.one * startScale, Vector3.one * targetScale, t);
            yield return null;
        }

        // �� ���d�b 1.3
        rect.localScale = Vector3.one * targetScale;
        yield return new WaitForSecondsRealtime(holdTime);

        // �� ��������
        rect.localScale = Vector3.zero;

        flashCoroutine = null;
    }
}
