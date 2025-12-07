using UnityEngine;
using System.Collections;

public class Orc : EnemyBase
{
    public BeatSpriteAnimator anim;

    [Header("警告特效")]
    public GameObject warningPrefab;
    public Vector3 warningOffset;

    [Header("攻擊特效（技能 Prefab）")]
    public GameObject attackPrefab;
    public Vector3 attackOffset;

    [Header("充能特效")]
    public GameObject chargeVfxPrefab;
    public Vector3 chargeVfxOffset;
    private GameObject currentChargeVfx;

    [Header("攻擊頻率（距離上一次攻擊啟動的拍數）")]
    public int minAttackBeats = 10;
    public int maxAttackBeats = 15;
    private int nextAttackBeat = -999;

    [Header("Charge / 暈眩設定")]
    public int chargeBeats = 5;
    public int stunBeats = 3;
    public int chargeInterruptDamageThreshold = 50;

    [Header("衝刺 / 回歸設定")]
    public float dashTime = 0.05f;
    public float waitTime = 1.0f;
    public float returnTime = 0.05f;

    [Header("衝刺目標位置調整")]
    public Vector3 attackPositionOffset = Vector3.zero;

    [Header("Charge UI")]
    public Transform chargePoint;        // Orc 身上的定位點 (子物件)
    public GameObject chargeBarPrefab;   // UI Prefab
    private ChargeBarUI chargeBarUI;     // 動態生成出來的 UI

    // 狀態
    private CharacterData charData;
    private bool isMoving = false;
    private Vector3 originalPos;

    private bool isCharging = false;
    private int chargeStartBeat = -1;
    private int damageAccumDuringCharge = 0;

    private bool isStunned = false;
    private int stunEndBeat = -1;

    // ======================
    // Awake
    // ======================
    protected override void Awake()
    {
        base.Awake();
        charData = GetComponent<CharacterData>();

        if (charData == null)
            Debug.LogWarning($"{name} 找不到 CharacterData");
    }

    private void Reset()
    {
        if (anim == null)
            anim = GetComponent<BeatSpriteAnimator>();
    }

    private void Start()
    {
        // ★ 動態 UI 生成
        if (chargeBarPrefab != null && chargePoint != null)
        {
            var canvas = FindObjectOfType<Canvas>();

            GameObject uiObj = Instantiate(chargeBarPrefab, canvas.transform);

            chargeBarUI = uiObj.GetComponent<ChargeBarUI>();
            if (chargeBarUI == null)
            {
                Debug.LogError("ChargeBarPrefab 缺少 ChargeBarUI 腳本！");
                return;
            }

            chargeBarUI.worldFollowTarget = chargePoint;
            chargeBarUI.worldOffset = Vector3.zero;

            chargeBarUI.SetActive(false);
        }
    }

    // ======================
    // Enable / Disable
    // ======================
    private void OnEnable()
    {
        FMODBeatListener2.OnGlobalBeat += HandleBeat;

        int currentBeat = FMODBeatListener2.Instance != null
            ? FMODBeatListener2.Instance.GlobalBeatIndex
            : 0;

        nextAttackBeat = currentBeat + Random.Range(minAttackBeats, maxAttackBeats + 1);

        if (anim != null)
            anim.OnFrameEvent += HandleAnimEvent;
    }

    private void OnDisable()
    {
        if (chargeBarUI != null)
            Destroy(chargeBarUI.gameObject);

        if (currentChargeVfx != null)
            Destroy(currentChargeVfx);

        FMODBeatListener2.OnGlobalBeat -= HandleBeat;

        if (anim != null)
            anim.OnFrameEvent -= HandleAnimEvent;
    }

    // ======================
    // Beat
    // ======================
    private void HandleBeat(int globalBeat)
    {
        if (IsFeverLocked()) return;
        if (isMoving) return;

        // 暈眩中
        if (isStunned)
        {
            if (globalBeat >= stunEndBeat)
                ExitStun();
            return;
        }

        // Charge 中
        if (isCharging)
        {
            // ★ 改用 BattleEffectManager 的治療
            if (thisSlotInfo != null)
                BattleEffectManager.Instance.HealEnemy(thisSlotInfo, 10);

            // Charge 是否結束
            if (globalBeat - chargeStartBeat >= chargeBeats)
                FinishCharge();    // ★ 不再開始 Attack

            return;
        }


        // 普通狀態 → 是否開始行動
        if (globalBeat >= nextAttackBeat)
        {
            bool doCharge = (Random.value < 0.5f);   // ★ 50% 機率 Charge

            if (doCharge)
                StartCharge(globalBeat);
            else
                DoAttack();

            nextAttackBeat = globalBeat + Random.Range(minAttackBeats, maxAttackBeats + 1);
        }
    }


    // ======================
    // Charge 流程
    // ======================
    private void StartCharge(int startBeat)
    {
        if (isCharging || isStunned) return;

        isCharging = true;
        chargeStartBeat = startBeat;
        damageAccumDuringCharge = 0;

        if (anim != null)
            anim.Play("Charge", true);

        // Charge 開始時條是滿的（1）
        if (chargeBarUI != null)
        {
            chargeBarUI.SetActive(true);
            chargeBarUI.SetValue(1f);
        }

        if (chargeVfxPrefab != null)
        {
            currentChargeVfx = Instantiate(
                chargeVfxPrefab,
                transform.position + chargeVfxOffset,
                Quaternion.identity
            );
            currentChargeVfx.transform.SetParent(transform, true);
        }
    }

    private void FinishCharge()
    {
        isCharging = false;
        chargeStartBeat = -1;
        damageAccumDuringCharge = 0;

        if (chargeBarUI != null)
            chargeBarUI.SetActive(false);

        if (currentChargeVfx != null)
            Destroy(currentChargeVfx);

        // ★ 不再 Attack，轉為 Idle
        if (anim != null)
            anim.Play("Idle", true);
    }


    private void InterruptChargeAndStun()
    {
        if (!isCharging) return;

        if (chargeBarUI != null)
        {
            // 可視需要，可設為 0 再關閉
            chargeBarUI.SetValue(0f);
            chargeBarUI.SetActive(false);
        }

        isCharging = false;
        damageAccumDuringCharge = 0;

        if (currentChargeVfx != null)
            Destroy(currentChargeVfx);

        isStunned = true;

        int currentBeat = FMODBeatListener2.Instance != null
            ? FMODBeatListener2.Instance.GlobalBeatIndex
            : 0;

        stunEndBeat = currentBeat + stunBeats;

        if (anim != null)
        {
            anim.Play("HitCry", true);
            StartCoroutine(ShakeOneBeat());
        }
    }

    private IEnumerator ShakeOneBeat()
    {
        float beatDuration = FMODBeatListener2.Instance.SecondsPerBeat;
        float shakeTime = beatDuration * 2f; // 兩拍

        // ★★ 正確：使用敵人 Slot 標準站位（永遠不會錯）
        Vector3 basePos = thisSlotInfo.SlotTransform.position;

        float shakeMagnitude = 0.08f;
        float shakeSpeed = 60f;

        float timer = 0f;

        while (timer < shakeTime)
        {
            timer += Time.deltaTime;

            float offset = Mathf.Sin(timer * shakeSpeed) * shakeMagnitude;

            transform.position = basePos + new Vector3(offset, 0, 0);

            yield return null;
        }

        // ★ 回正到固定站位，不受衝刺影響
        transform.position = basePos;
    }


    private void ExitStun()
    {
        isStunned = false;
        stunEndBeat = -1;

        if (anim != null)
            anim.Play("Idle", true);
    }

    // ======================
    // EnemyBase → BattleEffectManager 會呼叫
    // ======================
    public override void OnDamaged(int damage, bool isHeavyAttack)
    {
        base.OnDamaged(damage, isHeavyAttack);

        if (isCharging && !isStunned)
        {
            damageAccumDuringCharge += damage;

            if (chargeBarUI != null)
            {
                float remain = 1f - (float)damageAccumDuringCharge / chargeInterruptDamageThreshold;
                chargeBarUI.SetValue(remain);
            }

            if (damageAccumDuringCharge >= chargeInterruptDamageThreshold)
            {
                InterruptChargeAndStun();
            }
        }
    }

    


    // ======================
    // Attack 動畫啟動
    // ======================
    private void DoAttack()
    {
        if (IsFeverLocked()) return;

        if (anim != null)
            anim.Play("Attack", true);
    }

    // ======================
    // Animation Event
    // ======================
    private void HandleAnimEvent(BeatSpriteFrame frame)
    {
        if (IsFeverLocked()) return;
        if (isStunned) return;

        // Warning 特效
        if (frame.triggerWarning && warningPrefab != null)
        {
            Instantiate(
                warningPrefab,
                transform.position + warningOffset,
                Quaternion.identity
            );
        }

        // Attack 特效 & 衝刺
        if (frame.triggerAttack)
        {
            SpawnAttackSkill();
            StartCoroutine(AttackMovementFlow());
        }
    }

    // ======================
    // 產生技能
    // ======================
    private void SpawnAttackSkill()
    {
        if (attackPrefab == null) return;

        GameObject go = Instantiate(
            attackPrefab,
            transform.position,
            Quaternion.identity
        );

        go.transform.SetParent(this.transform, true);
        go.transform.localPosition = attackOffset;

        EnemySkillAttack skill = go.GetComponent<EnemySkillAttack>();
        if (skill == null)
        {
            Debug.LogError($"技能 {attackPrefab.name} 缺少 EnemySkillAttack！");
            return;
        }

        int atk = (charData != null) ? charData.Atk : 1;
        var target = BattleManager.Instance.CTeamInfo[0];
        var attackerSlot = thisSlotInfo != null ? thisSlotInfo : selfSlot;

        skill.Init(
            attacker: attackerSlot,
            target: target,
            damage: atk,
            travelTime: 0.12f,
            isHeavyAttack: false,
            spawnExplosion: true,
            buffAction: null
        );
    }

    // ======================
    // 衝刺 → 等待 → 回歸
    // ======================
    private IEnumerator AttackMovementFlow()
    {
        if (isMoving) yield break;
        if (IsFeverLocked()) yield break;
        if (isStunned) yield break;

        isMoving = true;

        var target = BattleManager.Instance.CTeamInfo[0];
        if (target == null || target.Actor == null)
        {
            isMoving = false;
            yield break;
        }

        originalPos = thisSlotInfo.SlotTransform.position;
        Vector3 targetPos = target.Actor.transform.position + attackPositionOffset;

        // Step 1: 衝刺
        yield return MoveToPosition(targetPos, dashTime);

        // Step 2: 等待
        yield return new WaitForSeconds(waitTime);

        // Step 3: 回歸
        yield return MoveToPosition(originalPos, returnTime);

        isMoving = false;
    }

    // ======================
    // Lerp 移動
    // ======================
    private IEnumerator MoveToPosition(Vector3 targetPos, float duration)
    {
        Vector3 start = transform.position;
        float t = 0f;

        while (t < duration)
        {
            if (IsFeverLocked()) yield break;
            if (isStunned) yield break;

            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / duration);
            transform.position = Vector3.Lerp(start, targetPos, lerp);
            yield return null;
        }

        transform.position = targetPos;
    }

    // ======================
    // 死亡處理（覆寫 EnemyBase）
    // ======================
    public override void OnDeath()
    {
        base.OnDeath();

        // 移除 ChargeBarUI
        if (chargeBarUI != null)
        {
            Destroy(chargeBarUI.gameObject);
        }

        // 移除 ChargeVFX（如果還在）
        if (currentChargeVfx != null)
        {
            Destroy(currentChargeVfx);
        }

        // 保險：停止 Charging 狀態
        isCharging = false;
    }

}
