using UnityEngine;

public class BeatJudge : MonoBehaviour
{
    [Header("判定範圍 (秒)")]
    public float perfectRange = 0.05f;

    [Header("特效 UI Prefab")]
    public GameObject beatHitLightUIPrefab; // Perfect 命中特效
    public RectTransform beatHitPointUI;    // 打擊點 UI 的位置 (Canvas 下)

    public static BeatJudge Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // 檢查是否對拍（簡單版）
    public bool IsOnBeat()
    {
        float musicTime = MusicManager.Instance.GetMusicTime();
        BeatUI targetBeat = BeatManager.Instance.FindClosestBeat(musicTime);
        if (targetBeat == null) return false;

        float delta = Mathf.Abs(musicTime - targetBeat.GetNoteTime());
        bool perfect = delta <= perfectRange;

        if (perfect)
        {
            SpawnPerfectEffect();
        }

        return perfect;
    }

    private void SpawnPerfectEffect()
    {
        if (beatHitLightUIPrefab == null || beatHitPointUI == null) return;

        // 生成在 Canvas 下，位置對齊打擊點
        GameObject effect = Instantiate(beatHitLightUIPrefab, beatHitPointUI.parent);
        RectTransform effectRect = effect.GetComponent<RectTransform>();
        if (effectRect != null)
        {
            effectRect.anchoredPosition = beatHitPointUI.anchoredPosition;
        }

        // 自動銷毀，避免堆積
        Destroy(effect, 0.5f);
    }
}
