using UnityEngine;
using System.Collections;

public class BardFeverController : MonoBehaviour
{
    public BeatSpriteAnimator anim;

    private bool isFever;
    private int feverBeat;
    private Coroutine feverRoutine;
    private Coroutine shakeRoutine;

    private Vector3 originalPos;

    private SpriteRenderer spr;
    private int originalSortingOrder;

    public GameObject burningEffectVFX;
    private GameObject spawnedBurningVFX;

    private void Awake()
    {
        FeverManager.OnFeverUltStart += HandleFeverStart;
        FeverManager.OnFeverEnd += HandleFeverEnd;

        spr = GetComponentInChildren<SpriteRenderer>();
        if (spr != null)
            originalSortingOrder = spr.sortingOrder;   // 記錄原本的，例如 2


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


        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);

        // 一開始立刻切 HandsUp (Loop)
        if (anim != null)
            anim.Play("HandsUp", true);

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

        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);

        transform.localPosition = originalPos;

        if (spr != null)
            spr.sortingOrder = originalSortingOrder;

        if (anim != null)
            anim.Play("Idle", true);

        if (spawnedBurningVFX != null)
        {
            Destroy(spawnedBurningVFX);
            spawnedBurningVFX = null;
        }

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
    // ★ Bard Fever 動畫流程
    // HandsUp -> (7.5拍) 小跳躍 + ChangeGuitar -> FeverComboStrike(左右抖動)
    // -> (25拍) FeverRelease
    // -------------------------------------------------------------
    private IEnumerator FeverAnimFlow()
    {
        float spb = FMODBeatListener2.Instance.SecondsPerBeat;

        // -------------------------
        // ★ 等到第 7.5 拍
        // -------------------------
        yield return new WaitUntil(() => feverBeat >= 7.5);

        // -------------------------
        // ★ ChangeGuitar（1.5 拍，不 loop）
        // -------------------------
        if (anim != null)
            anim.Play("ChangeGuitar", false);

        // -------------------------
        // ★ ChangeGuitar 的前 1 拍：執行跳躍（7~8 拍）
        // -------------------------
        yield return StartCoroutine(JumpSmall(spb * 1f));  // 1拍跳躍

        // ★ 跳躍結束後生成 BurningEffect
        if (burningEffectVFX != null)
        {
            spawnedBurningVFX = Instantiate(
                burningEffectVFX,
                transform.position,
                Quaternion.identity,
                this.transform   // 跟隨 Bard
            );
        }

        // -------------------------
        // ★ 等待 ChangeGuitar 剩餘的 0.5 拍
        // -------------------------
        yield return new WaitForSeconds(spb * 0.5f);


        // -------------------------
        // ★ FeverComboStrike (Loop)
        // -------------------------
        if (anim != null)
            anim.Play("FeverComboStrike", true);

        // 啟動左右抖動
        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);

        shakeRoutine = StartCoroutine(ShakeDuringCombo());

        // -------------------------
        // ★ 等到第 25 拍
        // -------------------------
        yield return new WaitUntil(() => feverBeat >= 25);
        // ★★★ 第 25 拍 → 刪除 BurningEffect
        if (spawnedBurningVFX != null)
        {
            Destroy(spawnedBurningVFX);
            spawnedBurningVFX = null;
        }


        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);

        transform.localPosition = originalPos;

        if (anim != null)
            anim.Play("FeverRelease", false);
    }

    // =============================================================
    // ★ 小跳躍（7.5~8拍）
    // =============================================================
    private IEnumerator JumpSmall(float duration)
    {
        Vector3 start = originalPos;
        Vector3 peak = originalPos + new Vector3(0f, 0.5f, 0f); // 小跳 0.2f 單位

        float t = 0f;

        // 上升：0 → 0.5
        while (t < duration * 0.5f)
        {
            t += Time.deltaTime;
            float lerp = t / (duration * 0.5f);
            transform.localPosition = Vector3.Lerp(start, peak, lerp);
            yield return null;
        }

        t = 0f;

        // 下落：0.5 → 1
        while (t < duration * 0.5f)
        {
            t += Time.deltaTime;
            float lerp = t / (duration * 0.5f);
            transform.localPosition = Vector3.Lerp(peak, start, lerp);
            yield return null;
        }

        transform.localPosition = originalPos;
    }

    // =============================================================
    // ★ FeverComboStrike 期間左右抖動
    // =============================================================
    private IEnumerator ShakeDuringCombo()
    {
        float amount = 0.05f;     // 左右抖動幅度
        float speed = 64f;        // 抖動頻率

        while (isFever)
        {
            float x = Mathf.Sin(Time.time * speed) * amount;
            transform.localPosition = originalPos + new Vector3(x, 0, 0);
            yield return null;
        }

        transform.localPosition = originalPos;
    }
}
