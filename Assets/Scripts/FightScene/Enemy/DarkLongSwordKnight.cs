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
    public float multiSlashBeatInterval = 1f; // 以拍為單位
    public float warningLifetime = 1.5f;      // 警告顯示時間（秒）

    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private bool isHolding = false;
    private float holdTimer = 0f;
    private bool isAttacking = false;
    private bool isWarning = false;

    private float nextAttackTime;
    private float warningTime;
    private GameObject activeTargetWarning;
    private GameObject activeBossWarning;
    private int beatsBeforeAttack = -1;
    private int selectedSkill = 1;

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
                if (selectedSkill == 1)
                    StartCoroutine(Skill1_NormalSlash());
                else
                    StartCoroutine(Skill2_MultiSlash());
            }
        }
    }

    private void EnterWarningPhase()
    {
        isWarning = true;
        beatsBeforeAttack = warningBeats;

        if (spriteRenderer != null)
            spriteRenderer.color = warningColor;

        // 隨機決定使用技能
        selectedSkill = Random.Range(1, 3); // 1 或 2

        if (selectedSkill == 1)
        {
            randomTargetSlot = GetRandomPlayerSlot();

            if (randomTargetSlot != null && randomTargetSlot.Actor != null && targetWarningPrefab != null)
            {
                activeTargetWarning = Instantiate(
                    targetWarningPrefab,
                    randomTargetSlot.Actor.transform.position,
                    Quaternion.identity
                );
                Destroy(activeTargetWarning, warningLifetime); // ★ 1.5 秒後自動刪除
            }
        }
        else if (selectedSkill == 2)
        {
            // 自身警示特效
            if (multiSlashWarningPrefab != null)
            {
                activeBossWarning = Instantiate(multiSlashWarningPrefab, transform.position, Quaternion.identity);
                Destroy(activeBossWarning, warningLifetime); // ★ 1.5 秒後自動刪除
            }

            StartCoroutine(SpawnMultiWarnings());
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
                var warn = Instantiate(targetWarningPrefab, cTeam[i].Actor.transform.position, Quaternion.identity);
                Destroy(warn, warningLifetime); // ★ 每個警告 1.5 秒後刪除
            }

            yield return new WaitForSeconds(beatInterval * multiSlashBeatInterval); // ★ 間隔一拍
        }
    }

    private IEnumerator Skill1_NormalSlash()
    {
        isAttacking = true;

        if (randomTargetSlot == null || randomTargetSlot.Actor == null)
        {
            ResetState();
            yield break;
        }

        Vector3 origin = transform.position;
        Vector3 contactPoint = randomTargetSlot.Actor.transform.position
                             - BattleManager.Instance.meleeContactOffset
                             + (transform.localScale.x >= 0 ? dashOffset : -dashOffset);

        yield return Dash(origin, contactPoint, dashDuration);

        if (attackVfxPrefab != null)
        {
            Vector3 vfxPos = randomTargetSlot.Actor.transform.position + vfxOffset;
            var vfx = Instantiate(attackVfxPrefab, vfxPos, Quaternion.identity);
            if (vfxLifetime > 0f) Destroy(vfx, vfxLifetime);
        }

        BattleEffectManager.Instance?.OnHit(selfSlot, randomTargetSlot, true);
        yield return new WaitForSeconds(actionLockDuration);
        yield return Dash(transform.position, origin, dashDuration);

        ResetState();
        ScheduleNextAttack();
    }

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
                if (vfxLifetime > 0f) Destroy(vfx, vfxLifetime);
            }

            BattleEffectManager.Instance?.OnHit(selfSlot, cTeam[i], true);

            yield return new WaitForSeconds(0.5f); // 小間隔
            yield return Dash(transform.position, origin, dashDuration);
        }

        yield return new WaitForSeconds(actionLockDuration);
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
        {
            if (slot.Actor != null)
                validList.Add(slot);
        }

        if (validList.Count == 0) return null;
        return validList[Random.Range(0, validList.Count)];
    }
}
