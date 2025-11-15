using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 單一幀的設定：哪張圖、要停留幾拍
/// </summary>
[Serializable]
public class BeatSpriteFrame
{
    [Tooltip("要顯示的圖")]
    public Sprite sprite;

    [Tooltip("這張圖要停留幾拍（至少 1）")]
    public int beatLength = 1;
}

/// <summary>
/// 一個動畫 Clip，由多個 Frame 組成
/// 例如 Idle、Attack 等
/// </summary>
[Serializable]
public class BeatSpriteClip
{
    [Tooltip("這個 Clip 的名稱，如 Idle、Attack")]
    public string clipName = "Idle";

    [Tooltip("這個 Clip 的所有幀")]
    public BeatSpriteFrame[] frames;

    [Tooltip("是否循環播放（Idle 通常要勾）")]
    public bool loop = true;

    [Tooltip("不 Loop 且結束時是否停在最後一幀（例如 Attack 結束時）")]
    public bool holdLastFrame = false;
}

/// <summary>
/// 拍點驅動的 Sprite 動畫程式器。
/// 需要有 FMODBeatListener 在場景中，會訂閱 OnGlobalBeat。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class BeatSpriteAnimator : MonoBehaviour
{
    [Header("基本設定")]
    public SpriteRenderer spriteRenderer;

    [Tooltip("可設定多個動畫 Clip，例如 Idle、Attack 等")]
    public List<BeatSpriteClip> clips = new List<BeatSpriteClip>();

    [Tooltip("預設要播放的 Clip 名稱（通常是 Idle）")]
    public string defaultClipName = "Idle";

    [Tooltip("啟動時是否自動播放預設 Clip")]
    public bool playDefaultOnStart = true;

    // 目前狀態
    private BeatSpriteClip currentClip;
    private int currentFrameIndex = 0;
    private int beatsOnThisFrame = 0;
    private bool isPlaying = false;

    // 方便「目前在哪一拍切到哪一幀」做除錯
    private int lastBeatIndex = -1;

    // Clip 結束的事件（例如 Attack 播完要回 Idle，可以外部訂閱）
    public event Action<string> OnClipFinished;

    // 快速查表用
    private Dictionary<string, BeatSpriteClip> clipDict = new Dictionary<string, BeatSpriteClip>();

    private void Reset()
    {
        // 自動抓 SpriteRenderer
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        // 建立名稱 → Clip 的快速查詢表
        clipDict.Clear();
        foreach (var clip in clips)
        {
            if (clip != null && !string.IsNullOrEmpty(clip.clipName))
            {
                if (!clipDict.ContainsKey(clip.clipName))
                    clipDict.Add(clip.clipName, clip);
            }
        }
    }

    private void OnEnable()
    {
        
    }

    private void OnDisable()
    {
        if (FMODBeatListener.Instance != null)
        {
            FMODBeatListener.OnGlobalBeat -= HandleGlobalBeat;
        }
    }

    private void Start()
    {

        // 訂閱拍點事件
        if (FMODBeatListener.Instance != null)
        {
            FMODBeatListener.OnGlobalBeat += HandleGlobalBeat;
        }
        else
        {
            Debug.LogWarning("[BeatSpriteAnimator] 場景中沒有 FMODBeatListener，無法對拍。");
        }

        if (playDefaultOnStart && !string.IsNullOrEmpty(defaultClipName))
        {
            Play(defaultClipName, true);
        }
    }

    /// <summary>
    /// 拍點回呼：每次全局拍數增加就會被呼叫一次。
    /// </summary>
    private void HandleGlobalBeat(int globalBeat)
    {
        lastBeatIndex = globalBeat;

        if (!isPlaying || currentClip == null || currentClip.frames == null || currentClip.frames.Length == 0)
            return;

        StepOneBeat();
    }

    /// <summary>
    /// 讓目前幀「過一拍」，判斷是否要切下一幀。
    /// </summary>
    private void StepOneBeat()
    {
        if (currentClip.frames == null || currentClip.frames.Length == 0)
            return;

        beatsOnThisFrame++;

        BeatSpriteFrame frame = currentClip.frames[currentFrameIndex];
        int needBeats = Mathf.Max(1, frame.beatLength);

        if (beatsOnThisFrame >= needBeats)
        {
            // 換下一幀
            AdvanceFrame();
        }
    }

    /// <summary>
    /// 切到下一幀，根據是否 Loop / 結束決定後續動作。
    /// </summary>
    private void AdvanceFrame()
    {
        beatsOnThisFrame = 0;
        currentFrameIndex++;

        if (currentFrameIndex >= currentClip.frames.Length)
        {
            // Clip 播完
            if (currentClip.loop)
            {
                // 迴圈播放，從頭再來
                currentFrameIndex = 0;
                ApplyFrame();
            }
            else
            {
                // 不 Loop
                if (currentClip.holdLastFrame)
                {
                    // 停在最後一幀
                    currentFrameIndex = currentClip.frames.Length - 1;
                    ApplyFrame();
                }

                isPlaying = false;

                // 通知外部：這個 Clip 播完了
                OnClipFinished?.Invoke(currentClip.clipName);

                // 若有預設 Clip 且不是同一個，就自動切回預設（例如 Attack 播完回 Idle）
                if (!string.IsNullOrEmpty(defaultClipName) &&
                    defaultClipName != currentClip.clipName)
                {
                    Play(defaultClipName, true);
                }
            }
        }
        else
        {
            // 正常切到下一幀
            ApplyFrame();
        }
    }

    /// <summary>
    /// 將 currentFrameIndex 對應的 Sprite 套到 SpriteRenderer。
    /// </summary>
    private void ApplyFrame()
    {
        if (currentClip == null || currentClip.frames == null || currentClip.frames.Length == 0)
            return;

        currentFrameIndex = Mathf.Clamp(currentFrameIndex, 0, currentClip.frames.Length - 1);
        BeatSpriteFrame frame = currentClip.frames[currentFrameIndex];

        if (frame != null && frame.sprite != null && spriteRenderer != null)
        {
            spriteRenderer.sprite = frame.sprite;
        }
    }

    /// <summary>
    /// 播放指定名稱的 Clip。
    /// </summary>
    /// <param name="clipName">Clip 名稱</param>
    /// <param name="restartIfSame">如果當前正在播同一個 Clip，是否重新從頭開始</param>
    public void Play(string clipName, bool restartIfSame = true)
    {
        if (string.IsNullOrEmpty(clipName))
            return;

        if (!clipDict.TryGetValue(clipName, out var clip))
        {
            Debug.LogWarning($"[BeatSpriteAnimator] 找不到 Clip：{clipName}");
            return;
        }

        // 如果正在播同一個 Clip，且不想重啟，就直接忽略
        if (currentClip == clip && !restartIfSame)
            return;

        currentClip = clip;
        currentFrameIndex = 0;
        beatsOnThisFrame = 0;
        isPlaying = true;

        // 立刻套用第一幀（不用等下一拍）
        ApplyFrame();
    }

    /// <summary>
    /// 回傳目前正在播放的 Clip 名稱（沒有播放則回傳 null）。
    /// </summary>
    public string GetCurrentClipName()
    {
        return currentClip != null ? currentClip.clipName : null;
    }

    /// <summary>
    /// 回傳目前是否有在播放某個 Clip。
    /// </summary>
    public bool IsPlaying()
    {
        return isPlaying;
    }

    /// <summary>
    /// 立即停止動畫，不再隨拍點更新。
    /// （停在當前幀）
    /// </summary>
    public void Stop()
    {
        isPlaying = false;
    }
}
