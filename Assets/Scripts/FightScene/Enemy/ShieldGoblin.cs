using UnityEngine;
using System.Collections;

public class ShieldGoblin : EnemyBase
{
    public BeatSpriteAnimator anim;

    [Header("警告特效 Prefab")]
    public GameObject warningPrefab;
    public Vector3 warningOffset;

    [Header("格擋特效 Prefab")]
    public GameObject blockVfxPrefab;
    public Vector3 blockVfxOffset;

    [Header("攻擊特效 Prefab（EnemySkillAttack）")]
    public GameObject attackPrefab;
    public Vector3 attackOffset;

    [Header("攻擊間隔（拍）8~10 隨機")]
    public int minIntervalBeats = 8;
    public int maxIntervalBeats = 10;

    private int nextAttackBeat = -999;
    public bool isBlocking = false;
    private int blockStartBeat = -1;

    private CharacterData charData;

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

    private void OnEnable()
    {
        FMODBeatListener2.OnGlobalBeat += HandleBeat;

        int now = FMODBeatListener2.Instance.GlobalBeatIndex;
        nextAttackBeat = now + Random.Range(minIntervalBeats, maxIntervalBeats + 1);

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
    // Beat Timing
    // ======================
    private void HandleBeat(int globalBeat)
    {
        if (IsFeverLocked()) return;

        // ----------- 若正在格檔中：維持兩拍後攻擊 -----------
        if (isBlocking)
        {
            if (globalBeat - blockStartBeat >= 4)
            {
                isBlocking = false;
                RemoveBlockEffect();
                DoAttack();
            }
            return;
        }

        // ----------- 攻擊倒數 -----------
        if (globalBeat >= nextAttackBeat)
        {
            StartBlock(globalBeat);
        }
    }

    // ======================
    // Start Block
    // ======================
    private void StartBlock(int globalBeat)
    {
        isBlocking = true;
        blockStartBeat = globalBeat;

        if (anim != null)
            anim.Play("Block", true);

        // BattleEffectManager 的敵人格擋
        BattleEffectManager.Instance.ActivateEnemyBlock(
            this.gameObject,
            charData,
            4
        );

        ShowBlockEffect();
    }

    private void ShowBlockEffect()
    {
        if (blockVfxPrefab != null)
        {
            Instantiate(
                blockVfxPrefab,
                transform.position + blockVfxOffset,
                Quaternion.identity
            );
        }
    }

    private void RemoveBlockEffect()
    {
        // 若使用 ActivateEnemyBlock → BattleEffectManager 已處理特效的移除
    }

    // ======================
    // Attack
    // ======================
    private void DoAttack()
    {
        if (anim != null)
            anim.Play("Attack", true);

        int now = FMODBeatListener2.Instance.GlobalBeatIndex;
        nextAttackBeat = now + Random.Range(minIntervalBeats, maxIntervalBeats + 1);
    }

    // ======================
    // Frame Events
    // ======================
    private void HandleAnimEvent(BeatSpriteFrame frame)
    {
        if (IsFeverLocked()) return;

        // ---------- Warning（與 MageGoblin 相同） ----------
        if (frame.triggerWarning && warningPrefab != null)
        {
            SpawnWarning();
        }

        // ---------- Attack ----------
        if (frame.triggerAttack)
        {
            SpawnAttackSkill();
        }
    }

    // ======================
    // Spawn Warning (MageGoblin 同版)
    // ======================
    private void SpawnWarning()
    {
        Instantiate(
            warningPrefab,
            transform.position + warningOffset,
            Quaternion.identity
        );
    }

    // ======================
    // Spawn Attack Skill
    // ======================
    private void SpawnAttackSkill()
    {
        if (attackPrefab == null) return;

        GameObject go = Instantiate(
            attackPrefab,
            transform.position,
            Quaternion.identity
        );

        go.transform.position += attackOffset;

        EnemySkillAttack skill = go.GetComponent<EnemySkillAttack>();
        if (skill == null)
        {
            Debug.LogError($"攻擊 Prefab {attackPrefab.name} 缺少 EnemySkillAttack！");
            return;
        }

        int damage = 20;

        var target = BattleManager.Instance.CTeamInfo[0];
        var attackerSlot = thisSlotInfo != null ? thisSlotInfo : selfSlot;

        skill.Init(
            attacker: attackerSlot,
            target: target,
            damage: damage,
            travelTime: 0.22f,
            isHeavyAttack: false,
            spawnExplosion: true,
            buffAction: null
        );
    }
}
