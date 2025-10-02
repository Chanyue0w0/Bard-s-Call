using UnityEngine;
using System.Collections;

public class ShieldStrike : MonoBehaviour
{
    public BattleManager.TeamSlotInfo attacker;
    public BattleManager.TeamSlotInfo target;
    public GameObject explosionPrefab;
    public float travelTime = 0.05f;
    public int overrideDamage = -1; // -1 = ���л\�A�ӭ쥻������

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
            // �� ���w Perfect �� �ͦ��z��
            if (explosionPrefab != null)
            {
                Instantiate(explosionPrefab, targetPos, Quaternion.identity);
            }

            if (overrideDamage > -1)
            {
                // �T�w�ˮ`�Ҧ�
                target.HP -= overrideDamage;
                if (target.HP < 0) target.HP = 0;

                Debug.Log($"�iShieldStrike�j{attacker.UnitName} �R�� {target.UnitName}�A�T�w�ˮ`={overrideDamage} �ѾlHP={target.HP}");

                // ��s���
                var hb = target.Actor?.GetComponentInChildren<HealthBarUI>();
                if (hb != null) hb.ForceUpdate();

                if (target.HP <= 0)
                {
                    BattleEffectManager.Instance.OnHit(attacker, target, true);
                }
            }
            else
            {
                // �@�� OnHit �P�w�A�����w Perfect
                BattleEffectManager.Instance.OnHit(attacker, target, true);
            }
        }

        Destroy(gameObject);
    }
}
