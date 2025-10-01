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
        // �T�O���]�w target�A�åB�R������H�N�O target.Actor
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
