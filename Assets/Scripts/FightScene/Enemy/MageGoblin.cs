using System.Collections;
using UnityEngine;

public class MageGoblin : EnemyBase
{
    [Header("節拍縮放參數")]
    public Vector3 baseScale = new Vector3(0.16f, 0.16f, 0.16f);
    public float peakMultiplier = 1.25f;
    public float holdDuration = 0.05f;
    public float returnSpeed = 8f;

    [Header("攻擊設定")]
    public int attackDamage = 25;
    public float actionLockDuration = 0.5f;
    public int attackBeatsInterval = 8; // 每 8 拍施放一次火球
    public int warningBeats = 3;

    [Header("火球技能設定")]
    public GameObject fireBallPrefab;
    public Transform firePoint;

    [Header("警示設定")]
    public Color warningColor = Color.red;
    public GameObject targetWarningPrefab;
    private GameObject activeTargetWarning;

    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private bool isHolding = false;
    private bool isWarning = false;
    private bool isAttacking = false;
    private float holdTimer = 0f;

    private float nextAttackTime;
    private float warningTime;
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
        SetTargetToLastAlivePlayer();
    }

    void OnDestroy()
    {
        BeatManager.OnBeat -= OnBeat;
    }

    void Update()
    {
        if (forceMove || isAttacking) return;

        // 回復縮放動畫
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

        // 防呆：若警示結束但未攻擊，重新安排下一輪攻擊
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
                transform.localScale = baseScale * (peakMultiplier + 0.3f);

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

        SetTargetToLastAlivePlayer();

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

        // ★ Step 1：先選出原始目標（最後一位玩家）
        SetTargetToLastAlivePlayer();

        // ★ Step 2：嘲諷檢查（若被 Paladin 嘲諷，改成打 Paladin）
        if (BattleEffectManager.Instance != null && thisSlotInfo != null)
        {
            var taunter = BattleEffectManager.Instance.GetTaunter(thisSlotInfo);
            if (taunter != null && taunter.Actor != null)
            {
                Debug.Log($"【嘲諷生效】{name} 攻擊改為 Paladin {taunter.UnitName}");
                targetSlot = taunter;
            }
        }

        // ★ Step 3：防呆，目標不存在就跳過
        if (targetSlot == null || targetSlot.Actor == null)
        {
            Debug.LogWarning($"{name} 攻擊中止：目標為空");
            isAttacking = false;
            ScheduleNextAttack();
            yield break;
        }

        // 攻擊動畫延遲（可視覺上有「施法」動作）
        yield return new WaitForSeconds(0.2f);

        // 生成火球
        if (fireBallPrefab != null)
        {
            GameObject fireball = Instantiate(
                fireBallPrefab,
                firePoint != null ? firePoint.position : transform.position,
                Quaternion.identity
            );

            FireBallSkill skill = fireball.GetComponent<FireBallSkill>();
            if (skill != null)
            {
                skill.attacker = selfSlot;
                skill.target = targetSlot;
                skill.damage = attackDamage;
                skill.isPerfect = true;
                skill.isHeavyAttack = false;
            }
        }

        // 鎖住行動時間
        yield return new WaitForSeconds(actionLockDuration);

        // 重置狀態
        if (spriteRenderer != null)
            spriteRenderer.color = originalColor;

        if (activeTargetWarning != null)
            Destroy(activeTargetWarning);

        isWarning = false;
        isAttacking = false;

        SetTargetToLastAlivePlayer();
        ScheduleNextAttack();
    }

    // 取得「最後一位仍存活的玩家」
    private void SetTargetToLastAlivePlayer()
    {
        var teamInfo = BattleManager.Instance?.CTeamInfo;
        if (teamInfo == null)
            return;

        for (int i = teamInfo.Length - 1; i >= 0; i--)
        {
            var slot = teamInfo[i];
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
