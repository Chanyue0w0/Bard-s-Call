using UnityEngine;
using System.Collections;

public class ShieldStrike : MonoBehaviour
{
    public BattleManager.TeamSlotInfo attacker;
    public BattleManager.TeamSlotInfo target;
    public GameObject explosionPrefab;
    public float travelTime = 0.05f;
    public int overrideDamage = -1; // -1 = 不覆蓋，照原本公式算

    private void Start()
    {
        if (target != null && target.SlotTransform != null)
        {
            StartCoroutine(MoveToTarget(target.SlotTransform.position));
        }
        else
        {
            Destroy(gameObject);
        }
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

        if (attacker != null && target != null)
        {
            // ★ 必定 Perfect → 生成爆炸
            if (explosionPrefab != null)
            {
                Instantiate(explosionPrefab, targetPos, Quaternion.identity);
            }

            if (overrideDamage > -1)
            {
                // 固定傷害模式
                target.HP -= overrideDamage;
                if (target.HP < 0) target.HP = 0;

                Debug.Log($"【ShieldStrike】{attacker.UnitName} 命中 {target.UnitName}，固定傷害={overrideDamage} 剩餘HP={target.HP}");

                // 更新血條
                var hb = target.Actor?.GetComponentInChildren<HealthBarUI>();
                if (hb != null) hb.ForceUpdate();

                if (target.HP <= 0)
                {
                    BattleEffectManager.Instance.OnHit(attacker, target, true);
                }
            }
            else
            {
                // 一般 OnHit 判定，但必定 Perfect
                BattleEffectManager.Instance.OnHit(attacker, target, true);
            }
        }

        Destroy(gameObject);
    }
}
