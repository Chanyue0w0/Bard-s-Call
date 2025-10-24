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
    public Vector3 vfxOffset = new Vector3(0f, 1.0f, 0f);
    public Vector3 dashOffset = new Vector3(0.4f, 0f, 0f);
    public bool smoothDashMovement = true;

    [Header("強大連斬設定 (Skill 2)")]
    public GameObject multiSlashWarningPrefab;
    public float multiSlashBeatInterval = 1f;
    public float warningLifetime = 1.5f;

    [Header("重攻擊護盾設定 (Skill 3)")]
    public GameObject shieldVfxPrefab;
    public Vector3 shieldVfxOffset = new Vector3(0f, 0.5f, 0f); // ★ 可自訂護盾生成偏移
    public float shieldDurationBeats = 999f;

    [Header("召喚石像設定 (Skill 4)")]
    public GameObject stoneMinionPrefab;
    public Transform enemySlot2;

    // 狀態
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private bool isHolding = false;
    private float holdTimer = 0f;
    private bool isAttacking = false;
    private bool isWarning = false;

    // 護盾控制
    public bool isShieldActive = false;
    public bool isShieldBroken = false;
    private GameObject activeShieldVfx;

    private float nextAttackTime;
    private float warningTime;
    private int beatsBeforeAttack = -1;
    private int selectedSkill = 1;

    private GameObject activeTargetWarning;
    private GameObject activeBossWarning;
    private BattleManager.TeamSlotInfo randomTargetSlot;
    private CharacterData charData;

    protected override void Awake()
    {
        base.Awake();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;
        charData = GetComponent<CharacterData>();
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

        if (isHolding)
        {
            holdTimer -= Time.unscaledDeltaTime;
            if (holdTimer <= 0f)
                isHolding = false;
        }
        else
        {
            transform.localScale = Vector3.Lerp(transform.localScale, baseScale, Time.unscaledDeltaTime * returnSpeed);
        }

        if (!isWarning && Time.time >= warningTime)
            EnterWarningPhase();

        if (!isWarning && !isAttacking && Time.time >= nextAttackTime)
            ScheduleNextAttack();
    }

    // 拍點事件
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
            {
                switch (selectedSkill)
                {
                    case 1: StartCoroutine(Skill1_NormalSlash()); break;
                    case 2: StartCoroutine(Skill2_MultiSlash()); break;
                    case 3: StartCoroutine(Skill3_ShieldActivate()); break;
                    case 4: StartCoroutine(Skill4_SummonStone()); break;
                }
            }
        }
    }

    // 技能選擇條件
    private int ChooseNextSkill()
    {
        //if (!isShieldActive || isShieldBroken)
        //    return 3; // 沒護盾 → 強制開護盾
        float roll = Random.value * 100f;
        if (roll < 50f) return 1;
        if (roll < 70f) return 2;
        if (roll < 90f) return 3;
        return 4;
    }

    private void EnterWarningPhase()
    {
        isWarning = true;
        beatsBeforeAttack = warningBeats;

        if (spriteRenderer != null)
            spriteRenderer.color = warningColor;

        // 選擇技能
        selectedSkill = ChooseNextSkill();
        Debug.Log($"【DarkLongSwordKnight】進入警告階段，預計使用技能 {selectedSkill}");

        // 技能預警特效
        switch (selectedSkill)
        {
            case 1:
                // 普通斬擊：鎖定隨機玩家
                randomTargetSlot = GetRandomPlayerSlot();
                if (randomTargetSlot != null && randomTargetSlot.Actor != null && targetWarningPrefab != null)
                {
                    activeTargetWarning = Instantiate(
                        targetWarningPrefab,
                        randomTargetSlot.Actor.transform.position,
                        Quaternion.identity
                    );
                    Destroy(activeTargetWarning, warningLifetime); // 1.5 秒後自動刪除
                }
                break;

            case 2:
                // 強大連斬：生成多重警告
                if (multiSlashWarningPrefab != null)
                {
                    activeBossWarning = Instantiate(multiSlashWarningPrefab, transform.position, Quaternion.identity);
                    Destroy(activeBossWarning, warningLifetime);
                }
                StartCoroutine(SpawnMultiWarnings());
                break;

            case 3:
                if (spriteRenderer != null)
                    spriteRenderer.color = Color.cyan;

                if (shieldVfxPrefab != null)
                {
                    var preview = Instantiate(
                        shieldVfxPrefab,
                        transform.position + shieldVfxOffset,
                        Quaternion.identity
                    );
                    Destroy(preview, warningLifetime);
                }
                break;


            case 4:
                // 召喚石像：生成警告提示（使用同一個警告Prefab）
                if (multiSlashWarningPrefab != null)
                {
                    activeBossWarning = Instantiate(multiSlashWarningPrefab, transform.position, Quaternion.identity);
                    Destroy(activeBossWarning, warningLifetime);
                }
                break;
        }
    }

    private IEnumerator SpawnMultiWarnings()
    {
        var cTeam = BattleManager.Instance?.CTeamInfo;
        if (cTeam == null) yield break;

        float beatInterval = (BeatManager.Instance != null && BeatManager.Instance.bpm > 0)
            ? 60f / BeatManager.Instance.bpm
            : 0.4f;

        for (int i = 0; i < cTeam.Length; i++)
        {
            if (cTeam[i].Actor != null && targetWarningPrefab != null)
            {
                var warn = Instantiate(
                    targetWarningPrefab,
                    cTeam[i].Actor.transform.position,
                    Quaternion.identity
                );
                Destroy(warn, warningLifetime);
            }

            yield return new WaitForSeconds(beatInterval * multiSlashBeatInterval);
        }
    }


    // =====================
    // Skill 1：普通斬擊
    // =====================
    private IEnumerator Skill1_NormalSlash()
    {
        isAttacking = true;

        // 使用警告階段選定的目標，若當前為空才重新選
        if (randomTargetSlot == null || randomTargetSlot.Actor == null)
            randomTargetSlot = GetRandomPlayerSlot();

        if (randomTargetSlot == null || randomTargetSlot.Actor == null)
        {
            ResetState();
            yield break;
        }

        Vector3 origin = transform.position;
        Vector3 contactPoint = randomTargetSlot.Actor.transform.position
                             - BattleManager.Instance.meleeContactOffset
                             + (transform.localScale.x >= 0 ? dashOffset : -dashOffset);

        // 衝刺攻擊
        yield return Dash(origin, contactPoint, dashDuration);

        // 攻擊特效
        if (attackVfxPrefab != null)
        {
            Vector3 vfxPos = randomTargetSlot.Actor.transform.position + vfxOffset;
            var vfx = Instantiate(attackVfxPrefab, vfxPos, Quaternion.identity);
            Destroy(vfx, vfxLifetime);
        }

        // 傷害處理
        BattleEffectManager.Instance?.OnHit(selfSlot, randomTargetSlot, true);

        // 收尾
        yield return new WaitForSeconds(actionLockDuration);
        yield return Dash(transform.position, origin, dashDuration);

        ResetState();
        ScheduleNextAttack();
    }


    // =====================
    // Skill 2：強大連斬
    // =====================
    private IEnumerator Skill2_MultiSlash()
    {
        isAttacking = true;
        var cTeam = BattleManager.Instance?.CTeamInfo;
        if (cTeam == null) yield break;
        Vector3 origin = transform.position;
        for (int i = 0; i < cTeam.Length; i++)
        {
            if (cTeam[i].Actor == null) continue;
            Vector3 contactPoint = cTeam[i].Actor.transform.position
                                 - BattleManager.Instance.meleeContactOffset
                                 + (transform.localScale.x >= 0 ? dashOffset : -dashOffset);
            yield return Dash(origin, contactPoint, dashDuration);
            if (attackVfxPrefab != null)
            {
                Vector3 vfxPos = cTeam[i].Actor.transform.position + vfxOffset;
                var vfx = Instantiate(attackVfxPrefab, vfxPos, Quaternion.identity);
                Destroy(vfx, vfxLifetime);
            }
            BattleEffectManager.Instance?.OnHit(selfSlot, cTeam[i], true);
            yield return new WaitForSeconds(0.5f);
            yield return Dash(transform.position, origin, dashDuration);
        }
        yield return new WaitForSeconds(actionLockDuration);
        ResetState();
        ScheduleNextAttack();
    }

    // =====================
    // Skill 3：重攻擊護盾
    // =====================
    private IEnumerator Skill3_ShieldActivate()
    {
        isAttacking = true;
        isShieldBroken = false;
        isShieldActive = true;

        if (charData == null)
            charData = GetComponent<CharacterData>();

        // 移除這行（重複生成）
        // if (shieldVfxPrefab != null)
        // {
        //     activeShieldVfx = Instantiate(shieldVfxPrefab, transform.position + shieldVfxOffset, Quaternion.identity, transform);
        // }

        // 改成讓 BattleEffectManager 控制生成 + 註冊護盾狀態
        if (BattleEffectManager.Instance != null && charData != null)
        {
            BattleEffectManager.Instance.ActivateInfiniteBlock(gameObject, charData);
            Debug.Log("【DarkLongSwordKnight】啟動永久護盾狀態！");
        }

        ResetState();
        ScheduleNextAttack();
        yield break;
    }


    // ★ 提供外部呼叫（例如 BattleEffectManager 或重攻擊）
    public void BreakShield()
    {
        if (!isShieldActive || isShieldBroken) return;
        isShieldBroken = true;
        isShieldActive = false;
        if (activeShieldVfx != null)
            Destroy(activeShieldVfx);
        BattleEffectManager.Instance?.RemoveBlockEffect(gameObject);
        Debug.Log("【DarkLongSwordKnight】護盾被重攻擊破壞！");
    }

    // =====================
    // Skill 4：召喚石像
    // =====================
    private IEnumerator Skill4_SummonStone()
    {
        isAttacking = true;

        // 檢查 BattleManager 是否存在
        if (BattleManager.Instance == null)
        {
            Debug.LogWarning("【DarkLongSwordKnight】找不到 BattleManager，無法召喚石像。");
            ResetState();
            ScheduleNextAttack();
            yield break;
        }

        // 檢查第二個敵方位置是否有 Actor
        var enemySlots = BattleManager.Instance.EnemyTeamInfo;
        if (enemySlots == null || enemySlots.Length < 2)
        {
            Debug.LogWarning("【DarkLongSwordKnight】EnemyTeamInfo 長度不足，無法檢查第二格。");
            ResetState();
            ScheduleNextAttack();
            yield break;
        }

        if (enemySlots[1] != null && enemySlots[1].Actor != null)
        {
            Debug.Log("【DarkLongSwordKnight】第二格已有敵人，取消召喚。");
            ResetState();
            ScheduleNextAttack();
            yield break;
        }

        // 可以召喚
        if (stoneMinionPrefab != null && enemySlot2 != null)
        {
            var stone = Instantiate(stoneMinionPrefab, enemySlot2.position, Quaternion.identity);

            // 註冊到 BattleManager 的 EnemyTeamInfo[1]
            if (enemySlots[1] == null)
                enemySlots[1] = new BattleManager.TeamSlotInfo();

            enemySlots[1].Actor = stone;
            enemySlots[1].SlotTransform = enemySlot2;
            enemySlots[1].UnitName = "Rock Golem";
            enemySlots[1].ClassType = BattleManager.UnitClass.Enemy;

            Debug.Log("【DarkLongSwordKnight】成功在第二格召喚 Rock Golem。");
        }
        else
        {
            Debug.LogWarning("【DarkLongSwordKnight】stoneMinionPrefab 或 enemySlot2 為空，無法生成。");
        }

        yield return new WaitForSeconds(1f);
        ResetState();
        ScheduleNextAttack();
    }



    // =====================
    // Dash 通用
    // =====================
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

    // =====================
    // 工具
    // =====================
    private void ScheduleNextAttack()
    {
        float beatInterval = (BeatManager.Instance != null && BeatManager.Instance.bpm > 0)
            ? 60f / BeatManager.Instance.bpm : 0.4f;
        float wait = attackBeatsInterval * beatInterval;
        nextAttackTime = Time.time + wait;
        warningTime = nextAttackTime - warningBeats * beatInterval;
        if (warningTime <= Time.time) warningTime = Time.time;
        isWarning = false;
    }

    private void ResetState()
    {
        isWarning = false;
        isAttacking = false;
        if (spriteRenderer != null) spriteRenderer.color = originalColor;
        if (activeTargetWarning != null) Destroy(activeTargetWarning);
        if (activeBossWarning != null) Destroy(activeBossWarning);
    }

    private BattleManager.TeamSlotInfo GetRandomPlayerSlot()
    {
        var candidates = BattleManager.Instance?.CTeamInfo;
        if (candidates == null || candidates.Length == 0) return null;
        var validList = new System.Collections.Generic.List<BattleManager.TeamSlotInfo>();
        foreach (var slot in candidates)
            if (slot.Actor != null)
                validList.Add(slot);
        if (validList.Count == 0) return null;
        return validList[Random.Range(0, validList.Count)];
    }
}
