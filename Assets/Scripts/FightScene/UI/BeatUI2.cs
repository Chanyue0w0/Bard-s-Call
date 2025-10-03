using UnityEngine;

public class BeatUI2 : MonoBehaviour
{
    private float noteTime;       // 拍點應該命中的時間
    private float travelTime;     // 縮放時間
    private float targetScale;    // 最後縮到的大小
    private float startScale;     // 起始大小

    private RectTransform rect;

    public void Init(float noteTime, float targetScale, float travelTime)
    {
        this.noteTime = noteTime;
        this.targetScale = targetScale;
        this.travelTime = travelTime;

        rect = GetComponent<RectTransform>();

        startScale = 2f; // ★ 起始大小固定為 2
        rect.localScale = Vector3.one * startScale; // 確保一開始就是 (2,2,2)
    }

    public bool UpdateScale(float currentTime)
    {
        if (rect == null) return true;

        // t 從 0 → 1 對應 (noteTime - travelTime) → noteTime
        float t = Mathf.Clamp01(Mathf.InverseLerp(noteTime - travelTime, noteTime, currentTime));

        // 從 2 → 1.12
        float scale = Mathf.Lerp(startScale, targetScale, t);
        rect.localScale = Vector3.one * scale;

        //Debug.Log($"BeatUI scale={scale} t={t} time={currentTime}");

        // 過了拍點一點點就刪掉
        if (currentTime > noteTime + 0.1f)
            return true;

        return false;
    }

    public float GetNoteTime()
    {
        return noteTime;
    }
}
