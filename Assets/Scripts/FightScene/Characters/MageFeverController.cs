using UnityEngine;
using System.Collections;

public class MageFeverController : MonoBehaviour
{
    public BeatSpriteAnimator anim;

    private bool isFever;
    private int feverBeat;
    private Coroutine feverRoutine;

    private Vector3 originalPos;

    private SpriteRenderer spr;
    private int originalSortingOrder;

    private void Awake()
    {
        FeverManager.OnFeverUltStart += HandleFeverStart;
        FeverManager.OnFeverEnd += HandleFeverEnd;

        spr = GetComponentInChildren<SpriteRenderer>();
        if (spr != null)
            originalSortingOrder = spr.sortingOrder;

        if (anim == null)
            anim = GetComponentInChildren<BeatSpriteAnimator>();

        originalPos = transform.localPosition;
    }

    private void OnDestroy()
    {
        FeverManager.OnFeverUltStart -= HandleFeverStart;
        FeverManager.OnFeverEnd -= HandleFeverEnd;
    }

    // -------------------------------------------------------------
    // Fever 開始
    // -------------------------------------------------------------
    private void HandleFeverStart(int totalBeats)
    {
        isFever = true;
        feverBeat = 0;

        transform.localPosition = originalPos;

        if (spr != null)
            spr.sortingOrder = 20;

        if (feverRoutine != null)
            StopCoroutine(feverRoutine);

        feverRoutine = StartCoroutine(FeverAnimFlow());
    }

    // -------------------------------------------------------------
    // Fever 結束
    // -------------------------------------------------------------
    private void HandleFeverEnd()
    {
        isFever = false;

        if (feverRoutine != null)
            StopCoroutine(feverRoutine);

        transform.localPosition = originalPos;

        if (spr != null)
        {
            spr.sortingOrder = originalSortingOrder;
            spr.flipX = false;  // 保險
        }

        if (anim != null)
            anim.Play("Idle", true);
    }

    // -------------------------------------------------------------
    // Beat 計數
    // -------------------------------------------------------------
    private void OnEnable()
    {
        FMODBeatListener2.OnGlobalBeat += OnBeat;
    }

    private void OnDisable()
    {
        FMODBeatListener2.OnGlobalBeat -= OnBeat;
    }

    private void OnBeat(int beat)
    {
        if (!isFever) return;
        feverBeat++;
    }

    // -------------------------------------------------------------
    // ★ Paladin Fever 動畫流程
    // 第5拍跳＋FlipX = true
    // 第32拍跳＋FlipX = false
    // -------------------------------------------------------------
    private IEnumerator FeverAnimFlow()
    {
        float spb = FMODBeatListener2.Instance.SecondsPerBeat;

        // ===========================
        // ★ 等到第 5 拍
        // ===========================
        yield return new WaitUntil(() => feverBeat >= 6);

        // 翻面
        //if (spr != null)
        //    spr.flipX = true;

        // 小跳 1 拍
        yield return StartCoroutine(JumpSmall(spb * 1f));

        spr.sortingOrder = originalSortingOrder;

        // ===========================
        // ★ 等到第 32 拍
        // ===========================
        yield return new WaitUntil(() => feverBeat >= 32);

        //if (spr != null)
        //    spr.flipX = false;
        spr.sortingOrder = 20;

        // 再跳一次
        yield return StartCoroutine(JumpSmall(spb * 1f));
        spr.sortingOrder = originalSortingOrder;
    }

    // =============================================================
    // ★ 小跳躍
    // =============================================================
    private IEnumerator JumpSmall(float duration)
    {
        Vector3 start = originalPos;
        Vector3 peak = originalPos + new Vector3(0f, 0.5f, 0f);

        float t = 0f;

        // 上升
        while (t < duration * 0.5f)
        {
            t += Time.deltaTime;
            float lerp = t / (duration * 0.5f);
            transform.localPosition = Vector3.Lerp(start, peak, lerp);
            yield return null;
        }

        t = 0f;

        // 下落
        while (t < duration * 0.5f)
        {
            t += Time.deltaTime;
            float lerp = t / (duration * 0.5f);
            transform.localPosition = Vector3.Lerp(peak, start, lerp);
            yield return null;
        }

        transform.localPosition = originalPos;
    }
}
