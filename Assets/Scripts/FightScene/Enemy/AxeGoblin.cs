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

    [Header("攻擊頻率")]
    private int nextAttackBeat = -999;
    public int minAttackBeats = 6;
    public int maxAttackBeats = 10;

    [Header("受擊噴淚")]
    private int damageTakenThisBeat = 0;
    private int lastProcessedBeat = -1;

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

        nextAttackBeat = lastAttackBeat + Random.Range(minAttackBeats, maxAttackBeats + 1);

        if (anim != null)
            anim.OnFrameEvent += HandleAnimEvent;
    }

    private void OnDisable()
    {
        FMODBeatListener2.OnGlobalBeat -= HandleBeat;

        if (anim != null)
            anim.OnFrameEvent -= HandleAnimEvent;
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
    // Beat
    // ======================
    private void HandleBeat(int globalBeat)
    {
        if (IsFeverLocked()) return;
        if (isMoving) return;

        if (isMoving) return;

        if (globalBeat >= nextAttackBeat)
        {
            DoAttack();

            // ★ 攻擊完抽下一次等待拍數
            nextAttackBeat = globalBeat + Random.Range(minAttackBeats, maxAttackBeats + 1);
        }
    }

    private void TryPlayHitCry(int beat)
    {
        // 必須在 Idle 狀態
        if (anim == null) return;
        if (anim.GetCurrentClipName() != "Idle") return;

        // 下一拍沒有要攻擊  globalBeat + 1 > nextAttackBeat
        if (beat + 1 >= nextAttackBeat) return;

        // 本拍受到 >= 50 傷害才觸發
        //if (damageTakenThisBeat < 50) return;

        // 播放 HitCry 動畫
        anim.Play("HitCry", true);
        // Debug.Log($"{name} 受到重擊 → 播放 HitCry");
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
            FMODAudioPlayer.Instance.PlayAttackWarning(); //播放攻擊警告音效
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

            FMODAudioPlayer.Instance.PlayAxeGoblinAttack(); //播放斧頭哥布林攻擊音效

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
        originalPos = thisSlotInfo.SlotTransform.position;

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
