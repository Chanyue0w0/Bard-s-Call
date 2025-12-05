using UnityEngine;
using System.Collections;

public class MageGoblin : EnemyBase
{
    public BeatSpriteAnimator anim;

    [Header("警告特效")]
    public GameObject warningPrefab;
    public Vector3 warningOffset;

    [Header("魔法彈攻擊 Prefab（EnemySkillAttack）")]
    public GameObject magicBallPrefab;
    public Vector3 magicBallOffset;

    [Header("攻擊間隔（拍）")]
    public int attackIntervalBeats = 8;

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

        // ---------- Attack ----------
        if (frame.triggerAttack)
        {
            SpawnMagicBall();
        }
    }

    public override void OnDamaged(int dmg, bool isHeavyAttack)
    {
        base.OnDamaged(dmg, isHeavyAttack);

        if (anim == null) return;

        // 只有 Idle 狀態 且 heavy attack 才觸發噴淚與抖動
        if (anim.GetCurrentClipName() == "Idle") // && isHeavyAttack
        {
            anim.Play("HitCry", true);
            StartCoroutine(ShakeOneBeat());
        }
    }
    private IEnumerator ShakeOneBeat()
    {
        // 一拍 = FMODBeatListener2 的 beatDuration
        float beatDuration = FMODBeatListener2.Instance.SecondsPerBeat;
        float shakeTime = beatDuration; // 抖完整一拍

        Vector3 originalPos = transform.localPosition;

        float shakeMagnitude = 0.08f;  // 左右抖動強度（可調）
        float shakeSpeed = 60f;        // 越高越快（可調）

        float timer = 0f;

        while (timer < shakeTime)
        {
            timer += Time.deltaTime;

            float offset = Mathf.Sin(timer * shakeSpeed) * shakeMagnitude;
            transform.localPosition = originalPos + new Vector3(offset, 0, 0);

            yield return null;
        }

        // 抖完回到原位
        transform.localPosition = originalPos;
    }

    // ======================
    // 產生魔法彈（使用 EnemySkillAttack）
    // ======================
    private void SpawnMagicBall()
    {
        if (magicBallPrefab == null) return;

        GameObject go = Instantiate(
            magicBallPrefab,
            transform.position,
            Quaternion.identity
        );

        // ★ 直接偏移世界座標（世界座標偏移）
        go.transform.position += magicBallOffset;

        EnemySkillAttack skill = go.GetComponent<EnemySkillAttack>();
        if (skill == null)
        {
            Debug.LogError($"技能 {magicBallPrefab.name} 缺少 EnemySkillAttack！");
            return;
        }

        // 固定傷害 30（不吃 CharacterData）
        int damage = 30;

        // 攻擊目標固定為前排 CTeamInfo[0]
        var target = BattleManager.Instance.CTeamInfo[0];
        var attackerSlot = thisSlotInfo != null ? thisSlotInfo : selfSlot;

        // ★ 不附加 buffAction
        skill.Init(
            attacker: attackerSlot,
            target: target,
            damage: damage,
            travelTime: 0.22f,
            isHeavyAttack: false,
            spawnExplosion: true,
            buffAction: null   // ★ 不給毒、不給任何 buff
        );
    }
}
