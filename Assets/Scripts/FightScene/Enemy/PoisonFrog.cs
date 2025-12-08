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

    [Header("攻擊頻率")]
    public int minAttackBeats = 8;
    public int maxAttackBeats = 10;

    private int nextAttackBeat = -999;


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

    protected override void OnEnable()
    {
        // 先讓 EnemyBase 做它的事情（Fever 鎖定訂閱、同步）
        base.OnEnable();

        FMODBeatListener2.OnGlobalBeat += HandleBeat;

        lastAttackBeat = FMODBeatListener2.Instance.GlobalBeatIndex;

        // ★ 新增：第一次攻擊時間
        nextAttackBeat = lastAttackBeat + Random.Range(minAttackBeats, maxAttackBeats + 1);

        if (anim != null)
            anim.OnFrameEvent += HandleAnimEvent;
    }


    protected override void OnDisable()
    {
        FMODBeatListener2.OnGlobalBeat -= HandleBeat;

        if (anim != null)
            anim.OnFrameEvent -= HandleAnimEvent;

        // 再讓 EnemyBase 收尾（解除 Fever 相關訂閱）
        base.OnDisable();
    }



    // ======================
    // Beat Attack Timing
    // ======================
    private void HandleBeat(int globalBeat)
    {
        if (IsFeverLocked()) return;

        // ★ 取代原本固定 attackIntervalBeats 的寫法
        if (globalBeat >= nextAttackBeat)
        {
            DoAttack();

            // ★ 攻擊後抽下一次攻擊拍數
            nextAttackBeat = globalBeat + Random.Range(minAttackBeats, maxAttackBeats + 1);
        }
    }


    public void DoAttack()
    {
        if (anim != null)
            anim.Play("Attack", true);
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



    // ======================
    // Animation Frame Events
    // ======================
    private void HandleAnimEvent(BeatSpriteFrame frame)
    {
        if (IsFeverLocked()) return;

        // ---------- Warning ----------
        if (frame.triggerWarning && warningPrefab != null)
        {
            FMODAudioPlayer.Instance.PlayAttackWarning(); //播放攻擊警告音效
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

        // ★★★ 核心修改：攻擊技能抵達時間為「0.5 拍」
        float halfBeatTime = FMODBeatListener2.Instance.SecondsPerBeat * 0.5f;

        skill.Init(
            attacker: attackerSlot,
            target: target,
            damage: atk,
            travelTime: halfBeatTime,
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
