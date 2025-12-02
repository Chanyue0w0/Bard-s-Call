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

    [Header("事件模式")]
    public bool triggerOnce = false;
    [HideInInspector] public bool hasTriggered = false;
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
    private float accumulatedBeats = 0f;
    private bool isPlaying = false;

    public event Action<string> OnClipFinished;
    public event Action<BeatSpriteFrame> OnFrameEvent; // ★ 怪物接事件

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
            if (!string.IsNullOrEmpty(clip.clipName))
                clipDict[clip.clipName] = clip;
        }
    }

    void OnEnable()
    {
        FMODBeatListener2.OnBeatDelta_Anim += HandleBeatDelta;
    }

    void OnDisable()
    {
        FMODBeatListener2.OnBeatDelta_Anim -= HandleBeatDelta;
    }

    void Start()
    {
        if (playDefaultOnStart && !string.IsNullOrEmpty(defaultClipName))
            Play(defaultClipName, true);
    }

    private void HandleBeatDelta(float beatDelta)
    {
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
            accumulatedBeats -= need;
            AdvanceFrame();
        }
    }

    private void AdvanceFrame()
    {
        currentFrameIndex++;

        if (currentFrameIndex >= currentClip.frames.Length)
        {
            if (currentClip.loop)
            {
                currentFrameIndex = 0;

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

        // ★ 派發事件給怪物
        TriggerFrameEvents(currentClip.frames[currentFrameIndex]);
    }

    private void ApplyFrame()
    {
        if (currentClip == null ||
            currentClip.frames == null ||
            currentClip.frames.Length == 0)
            return;

        BeatSpriteFrame frame = currentClip.frames[currentFrameIndex];
        if (frame.sprite != null)
            spriteRenderer.sprite = frame.sprite;
    }

    private void TriggerFrameEvents(BeatSpriteFrame frame)
    {
        if (frame == null) return;

        if (frame.triggerOnce && frame.hasTriggered)
            return;

        if (frame.triggerWarning || frame.triggerAttack)
        {
            OnFrameEvent?.Invoke(frame);

            if (frame.triggerOnce)
                frame.hasTriggered = true;
        }
    }

    public void Play(string clipName, bool restartIfSame = true)
    {
        if (!clipDict.TryGetValue(clipName, out var newClip))
            return;

        if (currentClip == newClip && !restartIfSame)
            return;

        currentClip = newClip;
        currentFrameIndex = 0;

        foreach (var f in currentClip.frames)
            f.hasTriggered = false;

        accumulatedBeats = 0.0001f;
        isPlaying = true;
        ApplyFrame();
        // ★★★ 修正：補觸發第一格事件
        TriggerFrameEvents(currentClip.frames[currentFrameIndex]);
    }

    public string GetCurrentClipName() => currentClip?.clipName;
    public bool IsPlaying() => isPlaying;
    public void Stop() => isPlaying = false;
}
