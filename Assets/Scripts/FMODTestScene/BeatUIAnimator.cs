using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

[Serializable]
public class BeatUIFrame
{
    [Tooltip("這一拍要顯示的圖片")]
    public Sprite sprite;

    [Tooltip("這張圖片要停留幾拍 (可用小數，例如 0.5)")]
    public float beatLength = 1f;
}

[Serializable]
public class BeatUISequence
{
    [Tooltip("節奏序列的全部 Frame，例如 4 拍、8 拍、12 拍 Pattern")]
    public BeatUIFrame[] frames;

    [Tooltip("播放到最後一格時是否從頭循環")]
    public bool loop = true;
}

public class BeatUIAnimator : MonoBehaviour
{
    [Header("UI 元件")]
    public Image targetImage;

    [Header("節奏序列 (可自由設定拍數與 Pattern)")]
    public BeatUISequence sequence;

    [Header("進階設定")]
    [Tooltip("是否一開始就套用第一張圖")]
    public bool applyFirstFrameOnEnable = true;

    // 狀態
    private int frameIndex = 0;
    private float accumulatedBeats = 0f;

    private Coroutine waitRoutine;
    private bool subscribed = false;

    // ========================================================================
    // Unity Life Cycle
    // ========================================================================
    private void OnEnable()
    {
        // 不直接判斷 Instance，要「等到它準備好」
        waitRoutine = StartCoroutine(WaitAndSubscribe());
    }

    private IEnumerator WaitAndSubscribe()
    {
        // 等到 FMODBeatListener 真正建好
        while (FMODBeatListener.Instance == null)
            yield return null;

        FMODBeatListener.OnBeatDelta += HandleBeatDelta;
        subscribed = true;

        if (applyFirstFrameOnEnable)
            ApplyFrame();

        // 給一點點初始 beat，避免正好卡在 delta=0 的那一瞬間
        HandleBeatDelta(0.0001f);
    }

    private void OnDisable()
    {
        if (waitRoutine != null)
        {
            StopCoroutine(waitRoutine);
            waitRoutine = null;
        }

        if (subscribed && FMODBeatListener.Instance != null)
        {
            FMODBeatListener.OnBeatDelta -= HandleBeatDelta;
        }
        subscribed = false;
    }

    // ========================================================================
    // Core: BeatDelta 推進 UI Frame
    // ========================================================================
    private void HandleBeatDelta(float delta)
    {
        // Debug 看看現在是不是有被叫到
        // Debug.Log($"[BeatUIAnimator] delta = {delta}");

        if (sequence == null ||
            sequence.frames == null ||
            sequence.frames.Length == 0 ||
            targetImage == null)
            return;

        accumulatedBeats += delta;

        float need = Mathf.Max(0.0001f, sequence.frames[frameIndex].beatLength);

        if (accumulatedBeats >= need)
        {
            accumulatedBeats -= need;
            AdvanceFrame();
        }
    }

    // ========================================================================
    // 換下一張圖
    // ========================================================================
    private void AdvanceFrame()
    {
        frameIndex++;

        // 播放到最後處理 Loop 或固定在最後
        if (frameIndex >= sequence.frames.Length)
        {
            if (sequence.loop)
                frameIndex = 0;
            else
                frameIndex = sequence.frames.Length - 1;
        }

        ApplyFrame();
    }

    // ========================================================================
    // 套用 Sprite
    // ========================================================================
    private void ApplyFrame()
    {
        if (targetImage == null ||
            sequence == null ||
            sequence.frames == null ||
            sequence.frames.Length == 0)
            return;

        var frame = sequence.frames[Mathf.Clamp(frameIndex, 0, sequence.frames.Length - 1)];
        if (frame.sprite != null)
        {
            targetImage.sprite = frame.sprite;
        }
    }

    // ========================================================================
    // Public API：可在外部隨時切換節奏序列
    // ========================================================================
    public void SetSequence(BeatUISequence newSequence, bool restart = true)
    {
        sequence = newSequence;

        if (restart)
        {
            frameIndex = 0;
            accumulatedBeats = 0;
            ApplyFrame();
        }
    }

    public void ResetAnimation()
    {
        frameIndex = 0;
        accumulatedBeats = 0;
        ApplyFrame();
    }

    // ========================================================================
    // Getter：讀取 FMODBeatListener 拍點資訊（你要的）
    // ========================================================================

    public int GlobalBeat =>
        FMODBeatListener.GlobalBeatIndex;

    public int BeatInBar =>
        FMODBeatListener.CurrentBeatInBar;

    public int BeatsPerMeasure =>
        FMODBeatListener.BeatsPerMeasure;

    public float BeatTime =>
        FMODBeatListener.Instance != null ?
        FMODBeatListener.Instance.GetCurrentBeatTime() : 0;

    public float BPM =>
        FMODBeatListener.Instance != null ?
        60f / FMODBeatListener.Instance.SecondsPerBeat : 120f;
}
