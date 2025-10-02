using UnityEngine;
using System.Collections;

public class BeatJudge : MonoBehaviour
{
    [Header("判定範圍 (秒)")]
    public float perfectRange = 0.05f;

    [Header("特效 UI Prefab")]
    public GameObject beatHitLightUIPrefab; // Perfect 命中特效
    public RectTransform beatHitPointUI;    // 打擊點 UI 的位置 (Canvas 下)

    [Header("縮放動畫設定")]
    public float scaleUpSize = 2f;
    public float normalSize = 1.1202f;
    public float animTime = 0.15f; // 放大縮小各自的時間

    public static BeatJudge Instance { get; private set; }

    private Coroutine scaleCoroutine;

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

        PlayScaleAnim();

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

        GameObject effect = Instantiate(beatHitLightUIPrefab, beatHitPointUI.parent);
        RectTransform effectRect = effect.GetComponent<RectTransform>();
        if (effectRect != null)
        {
            effectRect.anchoredPosition = beatHitPointUI.anchoredPosition;
        }

        Destroy(effect, 0.5f);
    }

    private void PlayScaleAnim()
    {
        if (beatHitPointUI == null) return;

        // 如果正在跑舊的動畫，先停掉
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
        }
        scaleCoroutine = StartCoroutine(ScaleAnim());
    }

    private IEnumerator ScaleAnim()
    {
        Vector3 start = Vector3.one * normalSize;
        Vector3 up = Vector3.one * scaleUpSize;

        // 先放大
        float t = 0;
        while (t < 1)
        {
            t += Time.unscaledDeltaTime / animTime; // 用 unscaled，避免 TimeScale=0
            beatHitPointUI.localScale = Vector3.Lerp(start, up, t);
            yield return null;
        }

        // 再縮回去
        t = 0;
        while (t < 1)
        {
            t += Time.unscaledDeltaTime / animTime;
            beatHitPointUI.localScale = Vector3.Lerp(up, start, t);
            yield return null;
        }

        beatHitPointUI.localScale = start;
        scaleCoroutine = null;
    }
}
