using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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
    public GameObject fireExplosionVFX;

    public GameObject demonObject;                // Demon 子物件（整個GO）
    private SpriteRenderer demonSR;               // 其 SpriteRenderer
    public BeatSpriteAnimator demonAnim;          // Demon 的 BeatSpriteAnimator

    //[Header("Demon Destruction Ray")]
    //public GameObject demonDestructionRayPrefab;   // 預設放入你的 Prefab（含 MultiStrikeSkill）
    //public Transform demonRaySpawnPoint;           // 生成位置（可用 Bard 前方或 Demon 前方）


    private void Awake()
    {
        FeverManager.OnFeverUltStart += HandleFeverStart;
        FeverManager.OnFeverEnd += HandleFeverEnd;
        //FeverManager.OnFever25Beat += OnFever25Beat;  // << 新增

        spr = GetComponentInChildren<SpriteRenderer>();
        if (spr != null)
            originalSortingOrder = spr.sortingOrder;

        if (anim == null)
            anim = GetComponentInChildren<BeatSpriteAnimator>();

        originalPos = transform.localPosition;

        // ★ Demon 初始化（如果 Inspector 已指定）
        if (demonObject != null)
        {
            demonSR = demonObject.GetComponentInChildren<SpriteRenderer>();
            demonObject.SetActive(false);
        }
    }


    private void OnDestroy()
    {
        FeverManager.OnFeverUltStart -= HandleFeverStart;
        FeverManager.OnFeverEnd -= HandleFeverEnd;
        //FeverManager.OnFever25Beat -= OnFever25Beat;
    }

    //private void OnFever25Beat(int totalQTEComboCount)
    //{
    //    TriggerDemonDestructionRay(totalQTEComboCount);
    //}


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

        // ★ 啟動 Demon 動畫流程（平行）
        if (demonObject != null)
            StartCoroutine(DemonFlow());

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

        // ★★★ 落地瞬間特效：FireExplosion ★★★
        if (fireExplosionVFX != null)
        {
            Instantiate(
                fireExplosionVFX,
                transform.position,            // 世界座標位置
                Quaternion.identity
            );
        }

        transform.localPosition = originalPos;
    }

    private IEnumerator DemonFlow()
    {
        float spb = FMODBeatListener2.Instance.SecondsPerBeat;

        // 等到第 7 拍
        yield return new WaitUntil(() => feverBeat >= 7);

        // 1) 第七拍：啟動 + Alpha = 0
        demonObject.SetActive(true);
        SetDemonAlpha(0f); // ★ 一開始透明度 = 0

        // 2) 7 → 9拍：淡入 (Alpha 0 → 100)
        float fadeInDuration = spb * 2f;  // 2拍
        float t = 0f;
        float startA = 0f;
        float endA = 100f / 255f;

        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / fadeInDuration);
            SetDemonAlpha(Mathf.Lerp(startA, endA, lerp));
            yield return null;
        }
        SetDemonAlpha(endA);   // = 100 Alpha

        // 3) 第25拍：播放 Release 動畫
        yield return new WaitUntil(() => feverBeat >= 25);
        if (demonAnim != null)
            demonAnim.Play("Release", false);

        // 4) 29 → 33拍：淡出 (Alpha 100 → 0)
        yield return new WaitUntil(() => feverBeat >= 29);

        float fadeOutDuration = spb * 4f; // 4拍
        t = 0f;
        startA = 100f / 255f;
        endA = 0f;

        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / fadeOutDuration);
            SetDemonAlpha(Mathf.Lerp(startA, endA, lerp));
            yield return null;
        }

        SetDemonAlpha(0f);
        demonObject.SetActive(false);
    }

    private void SetDemonAlpha(float a)
    {
        if (demonSR == null) return;

        Color c = demonSR.color;
        c.a = a;
        demonSR.color = c;
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

    //public void TriggerDemonDestructionRay(int totalQTEComboCount)
    //{
    //    if (demonDestructionRayPrefab == null)
    //    {
    //        Debug.LogWarning("[BardFever] demonDestructionRayPrefab 未指定！");
    //        return;
    //    }

    //    // 計算傷害
    //    int baseDamage = 100;
    //    int bonusDamage = 0;

    //    if (totalQTEComboCount < 10)
    //    {
    //        bonusDamage = totalQTEComboCount * 30;
    //    }
    //    else if (totalQTEComboCount < 20)
    //    {
    //        bonusDamage = (10 * 30) + ((totalQTEComboCount - 10) * 20);
    //    }
    //    else
    //    {
    //        bonusDamage = (10 * 30) + (10 * 20) + ((totalQTEComboCount - 20) * 10);
    //    }

    //    int finalDamage = baseDamage + bonusDamage;

    //    // 生成 Prefab
    //    Transform spawnPos = demonRaySpawnPoint != null ? demonRaySpawnPoint : this.transform;
    //    GameObject obj = Instantiate(demonDestructionRayPrefab, spawnPos.position, spawnPos.rotation);

    //    // 設定 MultiStrikeSkill 屬性
    //    MultiStrikeSkill skill = obj.GetComponent<MultiStrikeSkill>();
    //    if (skill != null)
    //    {
    //        skill.attacker = null;

    //        // 加入全體敵人為目標
    //        List<BattleManager.TeamSlotInfo> allEnemies = new List<BattleManager.TeamSlotInfo>();
    //        foreach (var enemy in EnemyTeamInfo)
    //        {
    //            if (enemy != null && enemy.Actor != null && enemy.HP > 0)
    //                allEnemies.Add(enemy);
    //        }

    //        skill.targets = allEnemies;
    //        skill.isPerfect = true;
    //        skill.isHeavyAttack = true;
    //    }

    //    Debug.Log($"[BardFever] Demon Ray fired! Final Damage = {finalDamage}, Combo = {totalQTEComboCount}");
    //}

}
