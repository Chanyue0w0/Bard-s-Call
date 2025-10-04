using UnityEngine;
using System.Collections;

public class BeatJudge : MonoBehaviour
{
    [Header("判定範圍設定 (秒)")]
    [Tooltip("允許提早多少秒內仍算完美")]
    public float earlyRange = 0.03f;
    [Tooltip("允許延遲多少秒內仍算完美")]
    public float lateRange = 0.07f;
    [Header("判定時間補償 (秒)")]
    [Tooltip("正值會讓判定提前，建議 0.03~0.12 秒之間")]
    public float judgeOffset = 0.08f;


    [Header("特效與 UI")]
    public GameObject beatHitLightUIPrefab;
    public GameObject missTextPrefab;
    public RectTransform beatHitPointUI;

    [Header("縮放動畫設定")]
    public float scaleUpSize = 2f;
    public float normalSize = 1.12f;
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
        audioSource.playOnAwake = false;
    }

    // ===============================================
    // 判定核心
    // ===============================================
    public bool IsOnBeat()
    {
        if (BeatManager.Instance == null || MusicManager.Instance == null)
            return false;

        // 從 MusicManager 取時間，並提前一點以補償播放延遲
        float musicTime = MusicManager.Instance.GetMusicTime() - judgeOffset;


        // 取當前最近的拍點時間
        float prevBeat = BeatManager.Instance.GetPreviousBeatTime();
        float nextBeat = BeatManager.Instance.GetNextBeatTime();

        // 計算距離哪個拍點更近
        float targetTime = (Mathf.Abs(musicTime - prevBeat) < Mathf.Abs(musicTime - nextBeat))
            ? prevBeat
            : nextBeat;

        float delta = musicTime - targetTime; // 正：延遲，負：提前

        // 容許誤差範圍
        bool perfect = (delta >= -earlyRange && delta <= lateRange);

        // 播放縮放動畫
        PlayScaleAnim();

        if (perfect)
        {
            SpawnPerfectEffect();

            if (audioSource != null && snapClip != null)
            {
                double playTime = AudioSettings.dspTime + 0.05; // 提前排程 10ms
                audioSource.clip = snapClip;
                audioSource.PlayScheduled(playTime);
            }

        }
        else
        {
            SpawnMissText();
        }

        return perfect;
    }

    // ===============================================
    // 特效顯示
    // ===============================================
    private void SpawnPerfectEffect()
    {
        if (beatHitLightUIPrefab == null || beatHitPointUI == null) return;

        GameObject effect = Instantiate(beatHitLightUIPrefab, beatHitPointUI.parent);
        RectTransform rect = effect.GetComponent<RectTransform>();
        if (rect != null)
            rect.anchoredPosition = beatHitPointUI.anchoredPosition;

        Destroy(effect, 0.5f);
    }

    private void SpawnMissText()
    {
        if (missTextPrefab == null || beatHitPointUI == null) return;

        GameObject missObj = Instantiate(missTextPrefab, beatHitPointUI.parent);
        RectTransform rect = missObj.GetComponent<RectTransform>();
        if (rect != null)
            rect.anchoredPosition = beatHitPointUI.anchoredPosition + new Vector2(0, 50f);

        Destroy(missObj, 0.3f);
    }

    // ===============================================
    // UI 動畫
    // ===============================================
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

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / animTime;
            beatHitPointUI.localScale = Vector3.Lerp(start, up, t);
            yield return null;
        }

        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / animTime;
            beatHitPointUI.localScale = Vector3.Lerp(up, start, t);
            yield return null;
        }

        beatHitPointUI.localScale = start;
        scaleCoroutine = null;
    }
}
