using UnityEngine;
using System.Collections;

public class EnemySkillAttack : MonoBehaviour
{
    [Header("命中後是否自動銷毀")]
    public bool hitToDestroy = false;   // 預設 false

    [Header("命中後是否返回原位 (投擲武器等用)")]
    public bool returnToOrigin = false; // ★ 新增：預設 false

    [Header("返回原位所需拍數 (ex: 1 拍)")]
    public float returnBeats = 1f;      // ★ 新增：以拍為單位

    protected BattleManager.TeamSlotInfo attacker;
    protected BattleManager.TeamSlotInfo target;

    protected int damage;
    protected bool isHeavyAttack;
    protected bool spawnExplosion;

    protected float travelTime = 0.1f;

    [Header("特效")]
    public GameObject explosionPrefab;

    protected System.Action<BattleManager.TeamSlotInfo> onHitBuffAction;

    private bool hasArrived = false;

    // ★記錄原始位置（用於回程）
    private Vector3 originPos;


    // ================================
    // Init
    // ================================
    public void Init(
        BattleManager.TeamSlotInfo attacker,
        BattleManager.TeamSlotInfo target,
        int damage,
        float travelTime,
        bool isHeavyAttack = false,
        bool spawnExplosion = true,
        System.Action<BattleManager.TeamSlotInfo> buffAction = null
    )
    {
        this.attacker = attacker;
        this.target = target;
        this.damage = damage;
        this.travelTime = travelTime;
        this.isHeavyAttack = isHeavyAttack;
        this.spawnExplosion = spawnExplosion;
        this.onHitBuffAction = buffAction;
    }


    private void Start()
    {
        if (target == null || target.SlotTransform == null)
        {
            Destroy(gameObject);
            return;
        }

        originPos = transform.position;

        if (travelTime > 0f)
            StartCoroutine(MoveToTarget(target.SlotTransform.position));
        else
            transform.position = target.SlotTransform.position;
    }


    private IEnumerator MoveToTarget(Vector3 targetPos)
    {
        Vector3 start = transform.position;
        float elapsed = 0f;

        while (elapsed < travelTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / travelTime);
            transform.position = Vector3.Lerp(start, targetPos, t);
            yield return null;
        }

        hasArrived = true;
        TriggerHit();
    }


    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!hasArrived) return;
        if (target == null || target.Actor == null) return;

        if (other.gameObject == target.Actor)
            TriggerHit();
    }


    // ================================
    // Trigger Hit
    // ================================
    private void TriggerHit()
    {
        if (target == null)
        {
            if (hitToDestroy) Destroy(gameObject);
            return;
        }

        Vector3 pos = target.SlotTransform.position;

        // 爆炸特效
        if (spawnExplosion && explosionPrefab != null)
            Instantiate(explosionPrefab, pos, Quaternion.identity);

        // 傷害
        BattleEffectManager.Instance.OnHit(attacker, target, true, isHeavyAttack, damage);

        // Buff
        onHitBuffAction?.Invoke(target);


        // ★ 若設定為自動 Destroy → 直接 Destroy（優先）
        if (hitToDestroy)
        {
            Destroy(gameObject);
            return;
        }

        // ★ 若需要「返回原位」
        if (returnToOrigin)
        {
            float returnTimeSeconds = returnBeats * FMODBeatListener2.Instance.SecondsPerBeat;
            StartCoroutine(ReturnToOriginRoutine(returnTimeSeconds));
            return;
        }

        // ★ 否則什麼都不做 → 保留特效物件
    }


    // ================================
    // Return to origin
    // ================================
    private IEnumerator ReturnToOriginRoutine(float timeSec)
    {
        Vector3 start = transform.position;
        Vector3 end = originPos;
        float elapsed = 0f;

        while (elapsed < timeSec)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / timeSec);
            transform.position = Vector3.Lerp(start, end, t);
            yield return null;
        }

        // 回到原位後可以選擇 Destroy 或停留
        Destroy(gameObject);  // ★ 投擲武器通常返回後消失
    }
}
