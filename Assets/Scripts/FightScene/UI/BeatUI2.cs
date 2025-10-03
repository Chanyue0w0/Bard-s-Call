using UnityEngine;

public class BeatUI2 : MonoBehaviour
{
    private float noteTime;       // ���I���өR�����ɶ�
    private float travelTime;     // �Y��ɶ�
    private float targetScale;    // �̫��Y�쪺�j�p
    private float startScale;     // �_�l�j�p

    private RectTransform rect;

    public void Init(float noteTime, float targetScale, float travelTime)
    {
        this.noteTime = noteTime;
        this.targetScale = targetScale;
        this.travelTime = travelTime;

        rect = GetComponent<RectTransform>();

        startScale = 2f; // �� �_�l�j�p�T�w�� 2
        rect.localScale = Vector3.one * startScale; // �T�O�@�}�l�N�O (2,2,2)
    }

    public bool UpdateScale(float currentTime)
    {
        if (rect == null) return true;

        // t �q 0 �� 1 ���� (noteTime - travelTime) �� noteTime
        float t = Mathf.Clamp01(Mathf.InverseLerp(noteTime - travelTime, noteTime, currentTime));

        // �q 2 �� 1.12
        float scale = Mathf.Lerp(startScale, targetScale, t);
        rect.localScale = Vector3.one * scale;

        //Debug.Log($"BeatUI scale={scale} t={t} time={currentTime}");

        // �L�F���I�@�I�I�N�R��
        if (currentTime > noteTime + 0.1f)
            return true;

        return false;
    }

    public float GetNoteTime()
    {
        return noteTime;
    }
}
