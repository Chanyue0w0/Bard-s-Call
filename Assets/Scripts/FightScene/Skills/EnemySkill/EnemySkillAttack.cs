using UnityEngine;
using System.Collections;

public class EnemySkillAttack : MonoBehaviour
{
    // 運作所需資訊（由敵人呼叫 Init()）
    protected BattleManager.TeamSlotInfo attacker;
    protected BattleManager.TeamSlotInfo target;

    protected int damage;
    protected bool isHeavyAttack;
    protected bool spawnExplosion;

    protected float travelTime = 0.1f;   // 可用於遠程/近戰控制

    [Header("特效")]
    public GameObject explosionPrefab;

    // 你未來用於 Buff / Debuff 的資料結構（可擴充）
    protected System.Action<BattleManager.TeamSlotInfo> onHitBuffAction;

    private bool hasArrived = false;  // 避免重複觸發


    // ================================
    // 對外初始化（敵人呼叫）
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

        if (travelTime > 0f)
        {
            StartCoroutine(MoveToTarget(target.SlotTransform.position));
        }
        else
        {
            // 近戰：瞬間移動到目標
            transform.position = target.SlotTransform.position;
        }
    }


    // ================================
    // 投射物移動（遠程技能）
    // ================================
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


    // ================================
    // Trigger 判定傷害（近戰或遠程都可用）
    // ================================
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasArrived == false) return;  // 確保不是飛途中誤撞
        if (target == null || target.Actor == null) return;

        if (other.gameObject == target.Actor)
        {
            TriggerHit();
        }
    }


    // ================================
    // 統一處理命中事件
    // ================================
    private void TriggerHit()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 pos = target.SlotTransform.position;

        // 生成爆炸
        if (spawnExplosion && explosionPrefab != null)
        {
            Instantiate(explosionPrefab, pos, Quaternion.identity);
        }

        // 傷害流程
        BattleEffectManager.Instance.OnHit(attacker, target, true, isHeavyAttack, damage);

        // Buff / Debuff
        onHitBuffAction?.Invoke(target);

        Destroy(gameObject);
    }
}
