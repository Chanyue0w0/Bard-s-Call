using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BeatSpriteFrame
{
    public Sprite sprite;
    public float beatLength = 1f;

    [Header("事件設定（可選）")]
    public bool triggerWarning = false;
    public bool triggerAttack = false;

    public GameObject warningPrefab;
    public GameObject attackPrefab;

    [Header("事件模式")]
    public bool triggerOnce = false;       // ★是否只觸發一次（每次 Play 時會重置）
    [HideInInspector] public bool hasTriggered = false;  // ★內部狀態

    [Header("生成位置偏移（可選）")]
    public Vector3 spawnOffset = Vector3.zero;
}



[Serializable]
public class BeatSpriteClip
{
    public string clipName = "Idle";
    public BeatSpriteFrame[] frames;
    public bool loop = true;
    public bool holdLastFrame = false;
}

[RequireComponent(typeof(SpriteRenderer))]
public class BeatSpriteAnimator : MonoBehaviour
{
    [Header("基本設定")]
    public SpriteRenderer spriteRenderer;

    public List<BeatSpriteClip> clips = new List<BeatSpriteClip>();
    public string defaultClipName = "Idle";
    public bool playDefaultOnStart = true;

    private BeatSpriteClip currentClip;
    private int currentFrameIndex = 0;
    private float accumulatedBeats = 0f;   // 取代 beatsOnThisFrame
    private bool isPlaying = false;

    public event Action<string> OnClipFinished;

    private Dictionary<string, BeatSpriteClip> clipDict = new Dictionary<string, BeatSpriteClip>();


    void Reset()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        clipDict.Clear();
        foreach (var clip in clips)
        {
            if (clip != null && !string.IsNullOrEmpty(clip.clipName))
                clipDict[clip.clipName] = clip;
        }
    }

    void OnEnable()
    {
        FMODBeatListener2.OnBeatDelta_Anim += HandleBeatDelta;  // 改浮點拍點
    }

    void OnDisable()
    {
        FMODBeatListener2.OnBeatDelta_Anim -= HandleBeatDelta;
    }

    void Start()
    {
        if (playDefaultOnStart && !string.IsNullOrEmpty(defaultClipName))
        {
            Play(defaultClipName, true);
        }
    }


    // ======================================================
    // 核心：使用 Float beatDelta 更新動畫
    // ======================================================
    private void HandleBeatDelta(float beatDelta)
    {
        //Debug.Log($"{name} 收到 beatDelta = {beatDelta}");
        if (!isPlaying || currentClip == null)
            return;

        StepBeatFloat(beatDelta);
    }

    private void StepBeatFloat(float beatDelta)
    {
        if (currentClip.frames == null || currentClip.frames.Length == 0)
            return;

        accumulatedBeats += beatDelta;

        BeatSpriteFrame frame = currentClip.frames[currentFrameIndex];
        float need = Mathf.Max(0.0001f, frame.beatLength);

        if (accumulatedBeats >= need)
        {
            accumulatedBeats -= need; // 保留多餘的拍點，避免掉幀
            AdvanceFrame();
        }
    }


    // ======================================================
    // 換下一幀
    // ======================================================
    private void AdvanceFrame()
    {
        currentFrameIndex++;

        if (currentFrameIndex >= currentClip.frames.Length)
        {
            if (currentClip.loop)
            {
                currentFrameIndex = 0;

                // ★重置 triggerOnce 狀態
                foreach (var f in currentClip.frames)
                    f.hasTriggered = false;

                ApplyFrame();
            }
            else
            {
                if (currentClip.holdLastFrame)
                {
                    currentFrameIndex = currentClip.frames.Length - 1;
                    ApplyFrame();
                }

                isPlaying = false;
                OnClipFinished?.Invoke(currentClip.clipName);

                if (!string.IsNullOrEmpty(defaultClipName) &&
                    defaultClipName != currentClip.clipName)
                {
                    Play(defaultClipName, true);
                }
            }
        }
        else
        {
            ApplyFrame();
        }


        // ★新增：套用後執行事件
        var frame = currentClip.frames[currentFrameIndex];
        TriggerFrameEvents(frame);
    }



    // ======================================================
    // 套用 sprite
    // ======================================================
    private void ApplyFrame()
    {
        if (currentClip == null ||
            currentClip.frames == null ||
            currentClip.frames.Length == 0)
            return;

        currentFrameIndex = Mathf.Clamp(currentFrameIndex, 0, currentClip.frames.Length - 1);
        BeatSpriteFrame frame = currentClip.frames[currentFrameIndex];

        if (frame != null && frame.sprite != null)
            spriteRenderer.sprite = frame.sprite;
    }

    private void TriggerFrameEvents(BeatSpriteFrame frame)
    {
        if (frame == null) return;

        // ★若設定「只觸發一次」且已觸發過 → 直接 return
        if (frame.triggerOnce && frame.hasTriggered)
            return;

        bool triggered = false;

        // 生成警告
        if (frame.triggerWarning && frame.warningPrefab != null)
        {
            Instantiate(frame.warningPrefab, transform.position + frame.spawnOffset, Quaternion.identity);
            triggered = true;
        }

        // 生成攻擊特效
        if (frame.triggerAttack && frame.attackPrefab != null)
        {
            Instantiate(frame.attackPrefab, transform.position + frame.spawnOffset, Quaternion.identity);
            triggered = true;
        }

        // ★若有事件被觸發 → 標記已觸發
        if (triggered && frame.triggerOnce)
        {
            frame.hasTriggered = true;
        }
    }


    // ======================================================
    // 對外 API
    // ======================================================
    public void Play(string clipName, bool restartIfSame = true)
    {
        if (!clipDict.TryGetValue(clipName, out var newClip))
            return;

        if (currentClip == newClip && !restartIfSame)
            return;

        currentClip = newClip;
        currentFrameIndex = 0;

        // ★重置所有 frame 的觸發紀錄
        foreach (var f in currentClip.frames)
            f.hasTriggered = false;

        accumulatedBeats = 0.0001f;
        isPlaying = true;
        ApplyFrame();
    }



    public string GetCurrentClipName()
    {
        return currentClip != null ? currentClip.clipName : null;
    }

    public bool IsPlaying()
    {
        return isPlaying;
    }

    public void Stop()
    {
        isPlaying = false;
    }
}
