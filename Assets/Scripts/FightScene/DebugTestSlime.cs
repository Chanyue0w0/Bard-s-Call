using UnityEngine;

public class DebugTestSlime : MonoBehaviour
{
    [Header("左右微動參數")]
    public float amplitude = 0.05f;
    public float speed = 1.5f;
    public bool useLocalSpace = true;
    public bool randomizePhase = true;

    [Header("節拍縮放參數")]
    public Vector3 baseScale = new Vector3(0.15f, 0.15f, 0.15f);
    public float beatScaleMultiplier = 1.2f;
    public float scaleLerpSpeed = 6f;

    [Header("攻擊設定")]
    public int attackDamage = 20;           // 傷害數值
    public float minAttackInterval = 0f;    // 最小冷卻秒數
    public float maxAttackInterval = 10f;   // 最大冷卻秒數
    public int slotIndex = 0;               // 這個史萊姆屬於敵方的第幾格 (0,1,2)

    private float phase = 0f;
    private Vector3 basePosLocal;
    private Vector3 basePosWorld;
    private Vector3 targetScale;

    private float nextAttackTime;
    private bool readyToAttack = false;

    private BattleManager.TeamSlotInfo selfSlot => BattleManager.Instance.ETeamInfo[slotIndex];
    private BattleManager.TeamSlotInfo targetSlot => BattleManager.Instance.CTeamInfo[slotIndex];

    void OnEnable()
    {
        basePosLocal = transform.localPosition;
        basePosWorld = transform.position;
        phase = randomizePhase ? Random.Range(0f, Mathf.PI * 2f) : 0f;

        transform.localScale = baseScale;
        targetScale = baseScale;

        BeatManager.OnBeat += OnBeat;

        ScheduleNextAttack();
    }

    void OnDisable()
    {
        BeatManager.OnBeat -= OnBeat;
    }

    void Update()
    {
        // 左右擺動
        float offsetX = Mathf.Sin((Time.unscaledTime + phase) * speed) * amplitude;

        if (useLocalSpace)
        {
            Vector3 p = basePosLocal;
            p.x += offsetX;
            transform.localPosition = p;
        }
        else
        {
            Vector3 p = basePosWorld;
            p.x += offsetX;
            transform.position = p;
        }

        // 平滑縮放
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * scaleLerpSpeed);

        // 冷卻檢查
        if (Time.time >= nextAttackTime)
        {
            readyToAttack = true;
        }
    }

    private void OnBeat()
    {
        // Beat 縮放動畫
        targetScale = baseScale * beatScaleMultiplier;
        transform.localScale = baseScale;

        // 如果冷卻結束，就攻擊
        if (readyToAttack && targetSlot != null && targetSlot.Actor != null)
        {
            AttackTarget();
        }
    }

    private void AttackTarget()
    {
        // 使用 BattleManager 的流程進行攻擊
        Debug.Log($"史萊姆(slot {slotIndex}) 在Beat上發動攻擊 → {targetSlot.UnitName}");

        // 直接扣血（這裡是最簡單的作法）
        targetSlot.HP -= attackDamage;
        if (targetSlot.HP < 0) targetSlot.HP = 0;

        // 重新排程
        readyToAttack = false;
        ScheduleNextAttack();
    }

    private void ScheduleNextAttack()
    {
        float wait = Random.Range(minAttackInterval, maxAttackInterval);
        nextAttackTime = Time.time + wait;
    }

    void OnValidate()
    {
        amplitude = Mathf.Max(0f, amplitude);
        speed = Mathf.Max(0f, speed);
        minAttackInterval = Mathf.Max(0f, minAttackInterval);
        maxAttackInterval = Mathf.Max(minAttackInterval, maxAttackInterval);
    }
}
