using UnityEngine;

public class BeatScale : MonoBehaviour
{
    [Header("�Y��]�w")]
    public Vector3 baseScale = new Vector3(0.15f, 0.15f, 0.15f);
    public float beatScaleMultiplier = 1.2f;   // �C���j����
    public float scaleLerpSpeed = 6f;          // ���Ʀ^�_�t��

    private Vector3 targetScale;

    void OnEnable()
    {
        transform.localScale = baseScale;
        targetScale = baseScale;
        // �q�\ Beat �ƥ�
        BeatManager.OnBeat += OnBeat;
    }

    void OnDisable()
    {
        // �����q�\
        BeatManager.OnBeat -= OnBeat;
    }

    void Update()
    {
        // �����Y��^�h
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * scaleLerpSpeed);
    }

    private void OnBeat()
    {
        // �C��Ĳ�o �� ������j�A�M��A�����Y�^
        targetScale = baseScale * beatScaleMultiplier;
        transform.localScale = baseScale; // ���m����¦�j�p�A�~���u�u�_�v�P
    }
}
