using UnityEngine;

public class BeatUI : MonoBehaviour
{
    private float noteTime;          // 節拍應該命中的時間（秒）
    private Vector3 startPos;
    private Vector3 endPos;
    private float travelTime;

    public void Init(float noteTime, Vector3 startPos, Vector3 endPos, float travelTime)
    {
        this.noteTime = noteTime;
        this.startPos = startPos;
        this.endPos = endPos;
        this.travelTime = Mathf.Max(0.0001f, travelTime); // 防止除以 0
    }

    // 回傳 true 表示已經到達終點，要銷毀
    public bool UpdatePosition(float musicTime)
    {
        if (float.IsNaN(musicTime)) return true; // 防止非法數值

        // 計算進度
        float t = (musicTime - noteTime + travelTime) / travelTime;

        // 限制 t 在 [0,1]
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
