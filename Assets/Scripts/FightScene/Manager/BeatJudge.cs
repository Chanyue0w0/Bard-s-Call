using UnityEngine;
using System.Collections;

public class BeatJudge : MonoBehaviour
{
    [Header("判定範圍 (秒)")]
    public float perfectRange = 0.05f;

    [Header("特效 UI Prefab")]
    public GameObject beatHitLightUIPrefab;
    public RectTransform beatHitPointUI;

    [Header("Miss UI Prefab")]
    public GameObject missTextPrefab; // ★ 新增：Miss 文字的 UI Prefab

    [Header("縮放動畫設定")]
    public float scaleUpSize = 2f;
    public float normalSize = 1.1202f;
    public float animTime = 0.15f;

    [Header("音效設定")]
    public AudioClip snapClip;
    private AudioSource audioSource;

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

    private void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
    }

    public bool IsOnBeat()
    {
        float musicTime = MusicManager.Instance.GetMusicTime();
        BeatUI targetBeat = BeatManager.Instance.GetPersistentBeat();
        if (targetBeat == null) return false;

        PlayScaleAnim();

        float delta = Mathf.Abs(musicTime - GetClosestNoteTime(musicTime));
        bool perfect = delta <= perfectRange;

        if (perfect)
        {
            SpawnPerfectEffect();

            if (audioSource != null && snapClip != null)
                audioSource.PlayOneShot(snapClip);
        }
        else
        {
            SpawnMissText(); // ★ 非 Perfect 時顯示 Miss
        }

        return perfect;
    }

    // ★ 計算最近的理論節拍時間
    private float GetClosestNoteTime(float currentTime)
    {
        float interval = 60f / BeatManager.Instance.bpm / BeatManager.Instance.beatSubdivision;
        return Mathf.Round(currentTime / interval) * interval;
    }

    private void SpawnPerfectEffect()
    {
        if (beatHitLightUIPrefab == null || beatHitPointUI == null) return;

        GameObject effect = Instantiate(beatHitLightUIPrefab, beatHitPointUI.parent);
        RectTransform effectRect = effect.GetComponent<RectTransform>();
        if (effectRect != null)
            effectRect.anchoredPosition = beatHitPointUI.anchoredPosition;

        Destroy(effect, 0.5f);
    }

    private void SpawnMissText()
    {
        if (missTextPrefab == null || beatHitPointUI == null) return;

        GameObject missObj = Instantiate(missTextPrefab, beatHitPointUI.parent);
        RectTransform missRect = missObj.GetComponent<RectTransform>();
        if (missRect != null)
        {
            // 生成在 HitPoint 上方一點
            missRect.anchoredPosition = beatHitPointUI.anchoredPosition + new Vector2(0, 50f);
        }

        Destroy(missObj, 0.5f); // 0.5 秒後自動清掉
    }

    private void PlayScaleAnim()
    {
        if (beatHitPointUI == null) return;

        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(ScaleAnim());
    }

    private IEnumerator ScaleAnim()
    {
        Vector3 start = Vector3.one * normalSize;
        Vector3 up = Vector3.one * scaleUpSize;

        float t = 0;
        while (t < 1)
        {
            t += Time.unscaledDeltaTime / animTime;
            beatHitPointUI.localScale = Vector3.Lerp(start, up, t);
            yield return null;
        }

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
