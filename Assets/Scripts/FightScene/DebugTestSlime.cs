using UnityEngine;

public class DebugTestSlime : MonoBehaviour
{
    [Header("���k�L�ʰѼ�")]
    public float amplitude = 0.05f;
    public float speed = 1.5f;
    public bool useLocalSpace = true;
    public bool randomizePhase = true;

    [Header("�`���Y��Ѽ�")]
    public Vector3 baseScale = new Vector3(0.15f, 0.15f, 0.15f); // ��l�j�p
    public float beatScaleMultiplier = 1.2f; // �Y�񭿼�
    public float scaleLerpSpeed = 6f;       // �^�_�t��

    private float phase = 0f;
    private Vector3 basePosLocal;
    private Vector3 basePosWorld;

    private Vector3 targetScale;

    void OnEnable()
    {
        basePosLocal = transform.localPosition;
        basePosWorld = transform.position;
        phase = randomizePhase ? Random.Range(0f, Mathf.PI * 2f) : 0f;

        // ��l�j�p
        transform.localScale = baseScale;
        targetScale = baseScale;

        // �q�\ Beat �ƥ�]���] BeatManager ���o�Өƥ�^
        BeatManager.OnBeat += OnBeat;
    }

    void OnDisable()
    {
        // �O�o�Ѱ��q�\�A�קK���~
        BeatManager.OnBeat -= OnBeat;
    }

    void Update()
    {
        // ���k�\��
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

        // �����Y��^ baseScale
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * scaleLerpSpeed);
    }

    private void OnBeat()
    {
        // �C�����IĲ�o�ɡA���j�p�����ܤj�@�I
        targetScale = baseScale * beatScaleMultiplier;

        // �ߧY�]�^ baseScale�A�� Lerp ���j�A�Y�^
        transform.localScale = baseScale;
    }

    void OnValidate()
    {
        amplitude = Mathf.Max(0f, amplitude);
        speed = Mathf.Max(0f, speed);
    }
}
