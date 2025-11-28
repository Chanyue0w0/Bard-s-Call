using UnityEngine;
using System.Collections;

public class AxeGoblin : EnemyBase
{
    public BeatSpriteAnimator anim;

    [Header("警告特效")]
    public GameObject warningPrefab;
    public Vector3 warningOffset;

    [Header("攻擊特效（技能 Prefab）")]
    public GameObject attackPrefab;
    public Vector3 attackOffset;

    [Header("攻擊間隔（拍）")]
    public int attackIntervalBeats = 8;

    [Header("衝刺 / 回歸設定")]
    public float dashTime = 0.05f;
    public float waitTime = 1.0f;
    public float returnTime = 0.05f;

    [Header("衝刺目標位置調整（避免重疊）")]
    public Vector3 attackPositionOffset = Vector3.zero;

    private int lastAttackBeat = -999;

    // 角色數值資料（攻擊力）
    private CharacterData charData;

    private Vector3 originalPos;  // 用於回歸初始位置
    private bool isMoving = false;


    // ======================
    // Awake
    // ======================
    protected override void Awake()
    {
        base.Awake(); // 必須呼叫，否則 Slot 不會被綁好
        charData = GetComponent<CharacterData>();

        if (charData == null)
            Debug.LogWarning($"{name} 找不到 CharacterData");
    }

    private void Reset()
    {
        if (anim == null)
            anim = GetComponent<BeatSpriteAnimator>();
    }

    private void OnEnable()
    {
        FMODBeatListener2.OnGlobalBeat += HandleBeat;

        lastAttackBeat = FMODBeatListener2.Instance.GlobalBeatIndex;

        if (anim != null)
            anim.OnFrameEvent += HandleAnimEvent;
    }

    private void OnDisable()
    {
        FMODBeatListener2.OnGlobalBeat -= HandleBeat;

        if (anim != null)
            anim.OnFrameEvent -= HandleAnimEvent;
    }

    // ======================
    // Beat
    // ======================
    private void HandleBeat(int globalBeat)
    {
        if (IsFeverLocked()) return;   // Fever 期間不攻擊
        if (isMoving) return;          // 正在衝刺/回歸中不可重複攻擊

        if (globalBeat - lastAttackBeat >= attackIntervalBeats)
        {
            lastAttackBeat = globalBeat;
            DoAttack();
        }
    }

    public void DoAttack()
    {
        if (anim != null)
            anim.Play("Attack", true);
    }


    // ======================
    // Animation Event (Attack Frame)
    // ======================
    private void HandleAnimEvent(BeatSpriteFrame frame)
    {
        // Fever 中禁止任何攻擊 FrameEvent
        if (IsFeverLocked()) return;

        // =====================
        // 警告特效
        // =====================
        if (frame.triggerWarning && warningPrefab != null)
        {
            Instantiate(
                warningPrefab,
                transform.position + warningOffset,
                Quaternion.identity
            );
        }

        // =====================
        // 攻擊特效 + 衝刺與回歸
        // =====================
        if (frame.triggerAttack)
        {
            if (IsFeverLocked()) return; // double check 保險

            // 1. 產生攻擊技能
            SpawnAttackSkill();

            // 2. 啟動攻擊移動流程（衝刺 → 等待 → 回歸）
            StartCoroutine(AttackMovementFlow());
        }
    }

    // ======================
    // 產生技能
    // ======================
    private void SpawnAttackSkill()
    {
        if (attackPrefab == null) return;

        // ★ 先生成在世界座標
        GameObject go = Instantiate(
            attackPrefab,
            transform.position,
            Quaternion.identity
        );

        // ★ 設為子物件
        go.transform.SetParent(this.transform, true);

        // ★ 正確偏移：使用 localPosition
        go.transform.localPosition = attackOffset;

        EnemySkillAttack skill = go.GetComponent<EnemySkillAttack>();
        if (skill == null)
        {
            Debug.LogError($"技能 {attackPrefab.name} 缺少 EnemySkillAttack script！");
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
    // 衝刺 → 等待 → 回歸的流程
    // ======================
    private IEnumerator AttackMovementFlow()
    {
        if (isMoving) yield break;
        if (IsFeverLocked()) yield break; // Fever 中禁止衝刺

        isMoving = true;

        var target = BattleManager.Instance.CTeamInfo[0];
        if (target == null || target.Actor == null)
        {
            isMoving = false;
            yield break;
        }

        // 記錄初始位置
        originalPos = transform.position;

        Vector3 targetPos = target.Actor.transform.position + attackPositionOffset;

        // ---------- Step 1: 衝刺 ----------
        yield return MoveToPosition(targetPos, dashTime);

        // ---------- Step 2: 等待 ----------
        yield return new WaitForSeconds(waitTime);

        // ---------- Step 3: 回歸 ----------
        yield return MoveToPosition(originalPos, returnTime);

        isMoving = false;
    }


    // ======================
    // 移動 Lerp
    // ======================
    private IEnumerator MoveToPosition(Vector3 targetPos, float duration)
    {
        Vector3 start = transform.position;
        float t = 0f;

        while (t < duration)
        {
            if (IsFeverLocked()) yield break; // 如果你希望 Fever 直接中止移動

            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / duration);
            transform.position = Vector3.Lerp(start, targetPos, lerp);
            yield return null;
        }

        transform.position = targetPos;
    }
}
