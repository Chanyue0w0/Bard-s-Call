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
    public int attackDamage = 10;
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

    [Header("嘲諷特效設定")]
    public GameObject tauntVfxPrefab;  // 嘲諷期間顯示在敵人頭上的特效
    private GameObject activeTauntVfx; // 當前特效實例

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
        if (forceMove || isAttacking || IsFeverLocked()) return;

        // ★ 若剛被嘲諷且特效尚未生成 → 生成頭上特效
        if (activeTauntVfx == null && tauntedByObj != null && tauntVfxPrefab != null)
        {
            activeTauntVfx = Instantiate(tauntVfxPrefab, transform.position, Quaternion.identity); // + Vector3.up * 0f
            activeTauntVfx.transform.SetParent(transform);
            Debug.Log($"【嘲諷特效啟動】{name} 被 {tauntedByObj.name} 嘲諷");
        }


        // 嘲諷倒數更新（沿用 EnemyBase 內的 tauntBeatsRemaining 機制）
        if (tauntBeatsRemaining > 0)
        {
            float beatTime = (BeatManager.Instance != null) ? 60f / BeatManager.Instance.bpm : 0.4f;
            tauntBeatsRemaining -= Time.deltaTime / beatTime;

            if (tauntBeatsRemaining <= 0)
            {
                Debug.Log($"【嘲諷結束】{name} 嘲諷時間到，恢復自由目標。");
                tauntedByObj = null;
                tauntBeatsRemaining = 0;

                // ★ 銷毀嘲諷特效
                if (activeTauntVfx != null)
                {
                    Destroy(activeTauntVfx);
                    activeTauntVfx = null;
                    Debug.Log($"【嘲諷特效解除】{name} 嘲諷特效已刪除");
                }

                // ★ 嘲諷結束後恢復原本的攻擊邏輯
                SetTargetToLastAlivePlayer();

                // ★ 若有警示特效，立即更新位置
                if (activeTargetWarning != null && targetSlot?.Actor != null)
                {
                    activeTargetWarning.transform.position = targetSlot.Actor.transform.position;
                }
            }
        }


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

        // ★ 更新警示特效位置（讓紅圈持續跟隨攻擊目標）
        if (activeTargetWarning != null && targetSlot?.Actor != null)
        {
            activeTargetWarning.transform.position = targetSlot.Actor.transform.position;
        }

        // ★ 嘲諷特效持續跟隨敵人頭頂位置
        if (activeTauntVfx != null)
        {
            activeTauntVfx.transform.position = transform.position + Vector3.up * 2f;
        }

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

        // ★ 嘲諷檢查：如果目前被嘲諷 → 鎖定 Paladin
        if (tauntedByObj != null)
        {
            var paladinActor = tauntedByObj;
            var paladinSlot = System.Array.Find(
                BattleManager.Instance.CTeamInfo,
                t => t != null && t.Actor == paladinActor
            );

            if (paladinSlot != null)
                targetSlot = paladinSlot;
        }
        else
        {
            // 若沒被嘲諷，照舊選最後一位玩家
            SetTargetToLastAlivePlayer();
        }

        // ★ 更新警示生成位置為實際攻擊目標
        if (targetSlot != null && targetSlot.Actor != null && targetWarningPrefab != null)
        {
            if (activeTargetWarning != null)
                Destroy(activeTargetWarning);

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

        // 嘲諷檢查（若被 Paladin 嘲諷，改成打 Paladin）
        if (tauntedByObj != null)
        {
            var paladinActor = tauntedByObj;
            var paladinSlot = System.Array.Find(
                BattleManager.Instance.CTeamInfo,
                t => t != null && t.Actor == paladinActor
            );

            if (paladinSlot != null)
            {
                targetSlot = paladinSlot;
                Debug.Log($"【嘲諷生效】{name} 攻擊改為 Paladin {paladinSlot.UnitName}");
            }
        }
        else
        {
            // 嘲諷已解除，回復預設邏輯
            SetTargetToLastAlivePlayer();
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
