using UnityEngine;

public class BeatScale : MonoBehaviour
{
    [Header("�Y��]�w")]
    public Vector3 baseScale = new Vector3(0.15f, 0.15f, 0.15f);
    public float peakMultiplier = 1.3f;       // �`�������̤j��j����
    public float holdDuration = 0.05f;        // �O����j�ɶ�
    public float returnSpeed = 8f;            // �^�_�t��

    private bool isHolding;
    private float holdTimer;

    void OnEnable()
    {
        transform.localScale = baseScale;
        BeatManager.OnBeat += OnBeat;
    }

    void OnDisable()
    {
        BeatManager.OnBeat -= OnBeat;
    }

    void Update()
    {
        if (isHolding)
        {
            holdTimer -= Time.unscaledDeltaTime;
            if (holdTimer <= 0f)
            {
                isHolding = false;
            }
        }
        else
        {
            // �`�絲����ֳt�Y�^
            transform.localScale = Vector3.Lerp(transform.localScale, baseScale, Time.unscaledDeltaTime * returnSpeed);
        }
    }

    private void OnBeat()
    {
        // �`�������j�ơu�u�_�v�P
        transform.localScale = baseScale * peakMultiplier;
        isHolding = true;
        holdTimer = holdDuration;
    }
}
