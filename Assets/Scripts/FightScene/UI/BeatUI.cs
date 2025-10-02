using UnityEngine;

public class BeatUI : MonoBehaviour
{
    private float noteTime;
    private Vector2 startPos;
    private Vector2 endPos;
    private float travelTime;

    private RectTransform rect;

    public void Init(float noteTime, Vector3 startPos, Vector3 endPos, float travelTime)
    {
        this.noteTime = noteTime;
        this.startPos = startPos;   // 注意：這裡的 startPos, endPos 會被轉成 Vector2
        this.endPos = endPos;
        this.travelTime = Mathf.Max(0.0001f, travelTime);

        rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchoredPosition = this.startPos;
        }
    }

    public bool UpdatePosition(float musicTime)
    {
        Debug.Log($"BeatUI Update → musicTime={musicTime}, noteTime={noteTime}, travelTime={travelTime}, t={(musicTime - noteTime + travelTime) / travelTime}");

        if (rect == null) return true;

        float t = (musicTime - noteTime + travelTime) / travelTime;


        if (t < 0f) t = 0f;
        if (t > 1f) return true;

        rect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
        return false;
    }

    public float GetNoteTime()
    {
        return noteTime;
    }
}
