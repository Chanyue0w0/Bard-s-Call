using UnityEngine;
using System.Collections;

public class PoisonFrog : EnemyBase
{
    public BeatSpriteAnimator anim;

    [Header("警告特效")]
    public GameObject warningPrefab;
    public Vector3 warningOffset;

    [Header("毒泡攻擊 Prefab（EnemySkillAttack）")]
    public GameObject poisonBubblePrefab;
    public Vector3 poisonBubbleOffset;

    [Header("噴吐時額外爆炸 Prefab")]
    public GameObject explosionPrefab;
    public Vector3 explosionOffset;

    [Header("攻擊間隔（拍）")]
    public int attackIntervalBeats = 6;

    private int lastAttackBeat = -999;

    private CharacterData charData;


    // ======================
    // Awake
    // ======================
    protected override void Awake()
    {
        base.Awake(); // ★ 重要：綁定 thisSlotInfo
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
    // Beat Attack Timing
    // ======================
    private void HandleBeat(int globalBeat)
    {
        if (IsFeverLocked()) return;

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
    // Animation Frame Events
    // ======================
    private void HandleAnimEvent(BeatSpriteFrame frame)
    {
        if (IsFeverLocked()) return;

        // ---------- Warning ----------
        if (frame.triggerWarning && warningPrefab != null)
        {
            Instantiate(
                warningPrefab,
                transform.position + warningOffset,
                Quaternion.identity
            );
        }

        // ---------- 攻擊 Frame ----------
        if (frame.triggerAttack)
        {
            SpawnPoisonBubble();
            SpawnExplosion();   // ★ Frog 專屬行為：吐泡同時自身爆炸（小特效）
        }
    }



    // ======================
    // 產生毒泡（使用 EnemySkillAttack）
    // ======================
    private void SpawnPoisonBubble()
    {
        if (poisonBubblePrefab == null) return;

        GameObject go = Instantiate(
            poisonBubblePrefab,
            transform.position,
            Quaternion.identity
        );

        // ★ 不再成為子物件
        // go.transform.SetParent(this.transform, true);

        // ★ 直接偏移世界座標（世界座標偏移）
        go.transform.position += poisonBubbleOffset;

        EnemySkillAttack skill = go.GetComponent<EnemySkillAttack>();
        if (skill == null)
        {
            Debug.LogError($"技能 {poisonBubblePrefab.name} 缺少 EnemySkillAttack！");
            return;
        }

        int atk = (charData != null) ? charData.Atk : 1;
        var target = BattleManager.Instance.CTeamInfo[0];
        var attackerSlot = thisSlotInfo != null ? thisSlotInfo : selfSlot;

        skill.Init(
            attacker: attackerSlot,
            target: target,
            damage: atk,
            travelTime: 0.2f,
            isHeavyAttack: false,
            spawnExplosion: true,
            buffAction: (t) => BattleEffectManager.Instance.ApplyPoison(t, 5, 8)
        );
    }

    // ======================
    // 自身爆炸特效
    // ======================
    private void SpawnExplosion()
    {
        if (explosionPrefab == null) return;

        Instantiate(
            explosionPrefab,
            transform.position + explosionOffset,
            Quaternion.identity
        );
    }
}
