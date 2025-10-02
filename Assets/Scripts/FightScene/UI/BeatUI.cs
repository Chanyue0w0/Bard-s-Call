using UnityEngine;

public class BeatUI : MonoBehaviour
{
    private float noteTime;          // �`�����өR�����ɶ��]��^
    private Vector3 startPos;
    private Vector3 endPos;
    private float travelTime;

    public void Init(float noteTime, Vector3 startPos, Vector3 endPos, float travelTime)
    {
        this.noteTime = noteTime;
        this.startPos = startPos;
        this.endPos = endPos;
        this.travelTime = Mathf.Max(0.0001f, travelTime); // ����H 0
    }

    // �^�� true ��ܤw�g��F���I�A�n�P��
    public bool UpdatePosition(float musicTime)
    {
        if (float.IsNaN(musicTime)) return true; // ����D�k�ƭ�

        // �p��i��
        float t = (musicTime - noteTime + travelTime) / travelTime;

        // ���� t �b [0,1]
        if (t < 0f) t = 0f;
        if (t > 1f) return true;

        transform.position = Vector3.Lerp(startPos, endPos, t);
        return false;
    }

    public float GetNoteTime()
    {
        return noteTime;
    }
}
