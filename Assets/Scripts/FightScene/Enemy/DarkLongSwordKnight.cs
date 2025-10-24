using System.Collections;
using UnityEngine;

public class DarkLongSwordKnight : EnemyBase
{
    [Header("節拍縮放參數")]
    public Vector3 baseScale = new Vector3(0.2f, 0.2f, 0.2f);
    public float peakMultiplier = 1.4f;
    public float holdDuration = 0.05f;
    public float returnSpeed = 8f;

    [Header("普通斬擊設定 (Skill 1)")]
    public int attackDamage = 25;
    public float dashDuration = 0.15f;
    public float actionLockDuration = 0.4f;
    public int attackBeatsInterval = 8;
    public int warningBeats = 3;
    public Color warningColor = Color.red;

    [Header("Prefab 與特效設定")]
    public GameObject targetWarningPrefab;
    public GameObject attackVfxPrefab;
    public float vfxLifetime = 1.5f;
    public Vector3 vfxOffset = new Vector3(0f, 1.0f, 0f);   // ★ 可調整的特效偏移
    public Vector3 dashOffset = new Vector3(0.4f, 0f, 0f);  // ★ 可調整的攻擊距離
    public bool smoothDashMovement = true;                  // ★ 平滑衝刺開關

    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private bool isHolding = false;
    private float holdTimer = 0f;
    private bool isAttacking = false;
    private bool isWarning = false;

    private float nextAttackTime;
    private float warningTime;
    private GameObject activeTargetWarning;
    private int beatsBeforeAttack = -1;
    private BattleManager.TeamSlotInfo randomTargetSlot;

    protected override void Awake()
    {
        base.Awake();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;
    }

    void Start()
    {
        ScheduleNextAttack();
        BeatManager.OnBeat += OnBeat;
    }

    void OnDestroy()
    {
        BeatManager.OnBeat -= OnBeat;
    }

    void Update()
    {
        if (forceMove || isAttacking) return;

        // 回復縮放
        if (isHolding)
        {
            holdTimer -= Time.unscaledDeltaTime;
            if (holdTimer <= 0f)
                isHolding = false;
        }
        else
        {
            transform.localScale = Vector3.Lerp(
                transform.localScale,
                baseScale,
                Time.unscaledDeltaTime * returnSpeed
            );
        }

        // 進入警示階段
        if (!isWarning && Time.time >= warningTime)
            EnterWarningPhase();

        // 若錯過時機則重新排程
        if (!isWarning && !isAttacking && Time.time >= nextAttackTime)
            ScheduleNextAttack();
    }

    private void OnBeat()
    {
        if (forceMove || isAttacking) return;

        transform.localScale = baseScale * peakMultiplier;
        isHolding = true;
        holdTimer = holdDuration;

        if (isWarning)
        {
            beatsBeforeAttack--;

            if (beatsBeforeAttack == 1)
                transform.localScale = baseScale * (peakMultiplier + 0.4f);

            if (beatsBeforeAttack <= 0)
                StartCoroutine(Skill1_NormalSlash());
        }
    }

    private void EnterWarningPhase()
    {
        isWarning = true;
        beatsBeforeAttack = warningBeats;

        if (spriteRenderer != null)
            spriteRenderer.color = warningColor;

        // 隨機選擇一位玩家作為攻擊目標
        randomTargetSlot = GetRandomPlayerSlot();

        if (randomTargetSlot != null && randomTargetSlot.Actor != null && targetWarningPrefab != null)
        {
            activeTargetWarning = Instantiate(
                targetWarningPrefab,
                randomTargetSlot.Actor.transform.position,
                Quaternion.identity
            );
        }
    }

    private IEnumerator Skill1_NormalSlash()
    {
        isAttacking = true;

        if (randomTargetSlot == null || randomTargetSlot.Actor == null)
        {
            Debug.LogWarning($"{name} 攻擊中止：目標為空");
            ResetState();
            yield break;
        }

        Vector3 origin = transform.position;

        // Dash 終點（可加上自訂 dashOffset）
        Vector3 contactPoint = randomTargetSlot.Actor.transform.position
                             - BattleManager.Instance.meleeContactOffset
                             + (transform.localScale.x >= 0 ? dashOffset : -dashOffset);

        // Dash 攻擊
        yield return Dash(origin, contactPoint, dashDuration);

        // 攻擊特效
        if (attackVfxPrefab != null)
        {
            Vector3 vfxPos = randomTargetSlot.Actor.transform.position + vfxOffset;
            var vfx = Instantiate(attackVfxPrefab, vfxPos, Quaternion.identity);
            if (vfxLifetime > 0f)
                Destroy(vfx, vfxLifetime);
        }

        // 傷害處理
        BattleEffectManager.Instance?.OnHit(selfSlot, randomTargetSlot, true);

        // 等待動作結束
        yield return new WaitForSeconds(actionLockDuration);

        // 回到原位
        yield return Dash(transform.position, origin, dashDuration);

        ResetState();
        ScheduleNextAttack();
    }

    private IEnumerator Dash(Vector3 from, Vector3 to, float duration)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float progress = smoothDashMovement ? Mathf.SmoothStep(0f, 1f, t) : t;
            transform.position = Vector3.Lerp(from, to, progress);
            yield return null;
        }
    }

    private void ScheduleNextAttack()
    {
        float beatInterval = (BeatManager.Instance != null && BeatManager.Instance.bpm > 0)
            ? 60f / BeatManager.Instance.bpm
            : 0.4f;

        float wait = attackBeatsInterval * beatInterval;
        nextAttackTime = Time.time + wait;
        warningTime = nextAttackTime - warningBeats * beatInterval;

        if (warningTime <= Time.time)
            warningTime = Time.time;

        isWarning = false;
    }

    private void ResetState()
    {
        isWarning = false;
        isAttacking = false;

        if (spriteRenderer != null)
            spriteRenderer.color = originalColor;

        if (activeTargetWarning != null)
            Destroy(activeTargetWarning);
    }

    private BattleManager.TeamSlotInfo GetRandomPlayerSlot()
    {
        var candidates = BattleManager.Instance?.CTeamInfo;
        if (candidates == null || candidates.Length == 0) return null;

        var validList = new System.Collections.Generic.List<BattleManager.TeamSlotInfo>();
        foreach (var slot in candidates)
        {
            if (slot.Actor != null)
                validList.Add(slot);
        }

        if (validList.Count == 0) return null;
        return validList[Random.Range(0, validList.Count)];
    }
}
