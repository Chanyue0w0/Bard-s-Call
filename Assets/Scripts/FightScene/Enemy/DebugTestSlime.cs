using System.Collections;
using UnityEngine;

public class DebugTestSlime : EnemyBase
{
    [Header("節拍縮放參數")]
    public Vector3 baseScale = new Vector3(0.15f, 0.15f, 0.15f);
    public float peakMultiplier = 1.3f;
    public float holdDuration = 0.05f;
    public float returnSpeed = 8f;

    [Header("攻擊設定")]
    public int attackDamage = 20;
    public float dashDuration = 0.1f;
    public float actionLockDuration = 0.3f;

    [Header("拍點攻擊設定")]
    public int attackBeatsInterval = 8; // 每 8 拍攻擊一次

    [Header("特效設定")]
    public GameObject attackVfxPrefab;
    public float vfxLifetime = 1.5f;

    [Header("警示設定")]
    public int warningBeats = 3;
    public Color warningColor = Color.red;
    public GameObject targetWarningPrefab;

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

        // 初始化目標（首位存活玩家）
        SetTargetToFirstAlivePlayer();
    }

    void OnDestroy()
    {
        BeatManager.OnBeat -= OnBeat;
    }

    void Update()
    {
        if (forceMove || isAttacking || IsFeverLocked()) return;

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

        // 防呆保險
        if (!isWarning && !isAttacking && Time.time >= nextAttackTime)
            ScheduleNextAttack();
    }

    private void OnBeat()
    {
        if (forceMove || isAttacking || IsFeverLocked()) return;

        transform.localScale = baseScale * peakMultiplier;
        isHolding = true;
        holdTimer = holdDuration;

        if (isWarning)
        {
            beatsBeforeAttack--;

            if (beatsBeforeAttack == 1)
                transform.localScale = baseScale * (peakMultiplier + 0.4f);

            if (beatsBeforeAttack <= 0)
                StartCoroutine(AttackSequence());
        }
    }

    private void EnterWarningPhase()
    {
        isWarning = true;
        beatsBeforeAttack = warningBeats;

        if (spriteRenderer != null)
            spriteRenderer.color = warningColor;

        // 在進入警示階段前再次確認目標
        SetTargetToFirstAlivePlayer();

        if (targetSlot != null && targetSlot.Actor != null && targetWarningPrefab != null)
        {
            activeTargetWarning = Instantiate(
                targetWarningPrefab,
                targetSlot.Actor.transform.position,
                Quaternion.identity
            );
        }
    }

    private IEnumerator AttackSequence()
    {
        isAttacking = true;

        // 每次攻擊前都重新偵測首位存活玩家
        SetTargetToFirstAlivePlayer();

        if (targetSlot == null || targetSlot.Actor == null)
        {
            Debug.LogWarning($"{name} 攻擊中止：目標為空");
            isAttacking = false;
            ScheduleNextAttack();
            yield break;
        }

        Vector3 origin = transform.position;
        Vector3 contactPoint = targetSlot.Actor.transform.position - BattleManager.Instance.meleeContactOffset;

        yield return Dash(origin, contactPoint, dashDuration);

        if (attackVfxPrefab != null)
        {
            var vfx = Instantiate(attackVfxPrefab, targetSlot.Actor.transform.position, Quaternion.identity);
            if (vfxLifetime > 0f)
                Destroy(vfx, vfxLifetime);
        }

        // 造成傷害
        BattleEffectManager.Instance?.OnHit(selfSlot, targetSlot, true);

        yield return new WaitForSeconds(actionLockDuration);
        yield return Dash(transform.position, origin, dashDuration);

        if (spriteRenderer != null)
            spriteRenderer.color = originalColor;

        if (activeTargetWarning != null)
            Destroy(activeTargetWarning);

        isWarning = false;
        isAttacking = false;

        // 攻擊完後再一次確認目標（若前排死亡，改攻下一位）
        SetTargetToFirstAlivePlayer();
        ScheduleNextAttack();
    }

    private IEnumerator Dash(Vector3 from, Vector3 to, float duration)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.position = Vector3.Lerp(from, to, t);
            yield return null;
        }
    }

    // 永遠搜尋「首位仍存活的玩家」
    private void SetTargetToFirstAlivePlayer()
    {
        var teamInfo = BattleManager.Instance?.CTeamInfo;
        if (teamInfo == null)
            return;

        foreach (var slot in teamInfo)
        {
            if (slot != null && slot.Actor != null)
            {
                targetSlot = slot;
                return;
            }
        }

        targetSlot = null;
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

    public void RefreshBasePosition()
    {
        basePosLocal = transform.localPosition;
        basePosWorld = transform.position;
    }
}
