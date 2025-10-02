using UnityEngine;

public class SwordHitSkill : MonoBehaviour
{
    public BattleManager.TeamSlotInfo attacker;
    public BattleManager.TeamSlotInfo target;
    public GameObject explosionPrefab;
    public float lifeTime = 0.3f;
    public bool isPerfect; // ★ 新增

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (target != null && target.Actor != null && other.gameObject == target.Actor)
        {
           

            if (explosionPrefab != null)
            {
                Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            }

            if (isPerfect)
            {
                // Perfect 傷害加成
                BattleEffectManager.Instance.OnHit(attacker, target, true);
            }
            else
            {
                // 普通傷害
                BattleEffectManager.Instance.OnHit(attacker, target, false);
            }
            Destroy(gameObject, lifeTime);
        }
    }

}
