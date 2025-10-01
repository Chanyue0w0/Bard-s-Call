using UnityEngine;

public class SwordHitSkill : MonoBehaviour
{
    public BattleManager.TeamSlotInfo attacker;
    public BattleManager.TeamSlotInfo target;
    public GameObject explosionPrefab;
    public float lifeTime = 0.3f;

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        // 確保有設定 target，並且命中的對象就是 target.Actor
        if (target != null && target.Actor != null && other.gameObject == target.Actor)
        {
            if (explosionPrefab != null)
            {
                Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            }

            BattleEffectManager.Instance.OnHit(attacker, target);

            Destroy(gameObject);
        }
    }
}
