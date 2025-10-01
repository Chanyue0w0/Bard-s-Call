using UnityEngine;

public class DebugTestSlime : MonoBehaviour
{
    [Header("���k�L�ʰѼ�")]
    [Tooltip("���k�\�ʪ��̤j�첾�]���ء^")]
    public float amplitude = 0.05f;

    [Tooltip("�\�ʳt�ס]�C�����t�סA�V�j�V�֡^")]
    public float speed = 1.5f;

    [Tooltip("�O�_�H�ۨ��y�Шt���ʡF�_�h�H�@�ɮy�в���")]
    public bool useLocalSpace = true;

    [Tooltip("�O�_�b�ҥήɥ[�J�H���ۦ�A�קK�h���v�ܩi�P�ɦP�V�\��")]
    public bool randomizePhase = true;

    // �������A
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

    // �b�Ѽ��ܧ�ɧY�ɧ�s����I
    void OnValidate()
    {
        amplitude = Mathf.Max(0f, amplitude);
        speed = Mathf.Max(0f, speed);
    }
}
