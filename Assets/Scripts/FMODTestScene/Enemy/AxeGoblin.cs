using UnityEngine;

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

    private int lastAttackBeat = -999;

    // 角色數值資料（攻擊力）
    private CharacterData charData;

    protected override void Awake()
    {
        base.Awake();   // 一定要加，否則 EnemyBase 的初始化不會跑

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

    private void HandleBeat(int globalBeat)
    {
        if (IsFeverLocked()) return;   // ★ Fever 鎖定時不觸發攻擊

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

    private void HandleAnimEvent(BeatSpriteFrame frame)
    {
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
        // 攻擊技能本體
        // =====================
        if (frame.triggerAttack && attackPrefab != null)
        {
            // 生成技能物件
            GameObject go = Instantiate(
                attackPrefab,
                transform.position + attackOffset,
                Quaternion.identity
            );

            EnemySkillAttack skill = go.GetComponent<EnemySkillAttack>();
            if (skill == null)
            {
                Debug.LogError($"技能 {attackPrefab.name} 缺少 EnemySkillAttack script！");
                return;
            }

            // 取得敵人的攻擊力
            int atk = (charData != null) ? charData.Atk : 1;

            // 取得玩家目標（暫時固定攻擊 0 號玩家）
            var target = BattleManager.Instance.CTeamInfo[0];

            // ★ 正確：使用 EnemyBase 綁定的 Slot，而不是 slotInfo（不存在）
            var attackerSlot = thisSlotInfo != null ? thisSlotInfo : selfSlot;

            if (attackerSlot == null)
            {
                Debug.LogError($"{name} 找不到自身的 SlotInfo！技能無法初始化");
                return;
            }

            // 初始化技能
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
    }
}
