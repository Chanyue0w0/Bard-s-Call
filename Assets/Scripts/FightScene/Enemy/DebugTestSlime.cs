using System.Collections;
using UnityEngine;

public class DebugTestSlime : EnemyBase
{
    [Header("基本數值")]
    public int maxHP = 50;
    public int hp = 50;
    public float respawnDelay = 10f;

    [Header("節拍縮放參數")]
    public Vector3 baseScale = new Vector3(0.15f, 0.15f, 0.15f);
    public float peakMultiplier = 1.3f;
    public float holdDuration = 0.05f;
    public float returnSpeed = 8f;

    [Header("攻擊設定")]
    public int attackDamage = 20;
    public float dashDuration = 0.1f;
    public float actionLockDuration = 0.3f;

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

        // ★ 新增：訂閱 BeatManager 拍點事件
       
        BeatManager.OnBeat += OnBeat;
    }

    void OnDestroy()
    {
        // ★ 新增：取消訂閱，避免場景重載報錯
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

        // 若到了警示時間但還沒進入警示階段
        if (!isWarning && Time.time >= warningTime)
        {
            EnterWarningPhase();
        }

        // 若攻擊間隔時間已到但未進入警示（安全檢查）
        if (!isWarning && !isAttacking && Time.time >= nextAttackTime)
        {
            ScheduleNextAttack();
        }
    }

    // 每拍觸發
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
            {
                transform.localScale = baseScale * (peakMultiplier + 0.4f);
            }

            if (beatsBeforeAttack <= 0)
            {
                StartCoroutine(AttackSequence());
            }
        }
    }

    private void EnterWarningPhase()
    {
        isWarning = true;
        beatsBeforeAttack = warningBeats;

        if (spriteRenderer != null)
            spriteRenderer.color = warningColor;

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

        Vector3 origin = transform.position;
        Vector3 contactPoint = targetSlot.Actor.transform.position - BattleManager.Instance.meleeContactOffset;

        // 移動突進
        yield return Dash(origin, contactPoint, dashDuration);

        // 生成攻擊特效
        if (attackVfxPrefab != null)
        {
            var vfx = Instantiate(attackVfxPrefab, targetSlot.Actor.transform.position, Quaternion.identity);
            if (vfxLifetime > 0f)
                Destroy(vfx, vfxLifetime);
        }

        // 傷害判定
        BattleEffectManager.Instance?.OnHit(selfSlot, targetSlot, true);

        // 行動鎖（停頓）
        yield return new WaitForSeconds(actionLockDuration);

        // 平滑回原位
        yield return Dash(transform.position, origin, dashDuration);

        // 狀態重置
        if (spriteRenderer != null)
            spriteRenderer.color = originalColor;

        if (activeTargetWarning != null)
            Destroy(activeTargetWarning);

        isWarning = false;
        isAttacking = false;

        // 重新安排下一輪攻擊
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

    private void ScheduleNextAttack()
    {
        float wait = Random.Range(2f, 5f);
        nextAttackTime = Time.time + wait;

        float beatInterval = 60f / BeatManager.Instance.bpm;
        warningTime = nextAttackTime - warningBeats * beatInterval;

        // 若警示時間已經過 → 立即進入警示
        if (warningTime <= Time.time)
            warningTime = Time.time;

        // 允許重新進入警示
        isWarning = false;
    }

    public void RefreshBasePosition()
    {
        basePosLocal = transform.localPosition;
        basePosWorld = transform.position;
    }
}
