using UnityEngine;
using System.Collections;

public class EnemySkillAttack : MonoBehaviour
{
    // ===========================
    // 新增可調整的 Hit 後是否 Destroy
    // ===========================
    [Header("命中後是否自動銷毀")]
    public bool hitToDestroy = false;   // 預設 false

    protected BattleManager.TeamSlotInfo attacker;
    protected BattleManager.TeamSlotInfo target;

    protected int damage;
    protected bool isHeavyAttack;
    protected bool spawnExplosion;

    protected float travelTime = 0.1f;   // 可用於遠程/近戰控制

    [Header("特效")]
    public GameObject explosionPrefab;

    protected System.Action<BattleManager.TeamSlotInfo> onHitBuffAction;

    private bool hasArrived = false;


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
            //if (hitToDestroy) Destroy(gameObject);
            return;
        }

        Vector3 pos = target.SlotTransform.position;

        // 爆炸
        if (spawnExplosion && explosionPrefab != null)
            Instantiate(explosionPrefab, pos, Quaternion.identity);

        // 傷害
        BattleEffectManager.Instance.OnHit(attacker, target, true, isHeavyAttack, damage);

        // Buff
        onHitBuffAction?.Invoke(target);

        // ===========================
        // ★ 新增：是否命中後 destroy ★
        // ===========================
        if (hitToDestroy)
        {
            Destroy(gameObject);
        }
    }
}
