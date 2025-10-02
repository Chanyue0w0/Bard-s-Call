using UnityEngine;
using System.Collections;

public class FireBallSkill : MonoBehaviour
{
    public BattleManager.TeamSlotInfo attacker;
    public BattleManager.TeamSlotInfo target;
    public GameObject explosionPrefab;
    public float travelTime = 0.05f;
    public bool isPerfect; // �� �s�W

    private void Start()
    {
        if (target != null && target.SlotTransform != null)
        {
            StartCoroutine(MoveToTarget(target.SlotTransform.position));
        }
        else
        {
            Destroy(gameObject); // �S���ؼЪ����R��
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

        // ��F�ؼЫ�ͦ��z��
        if (explosionPrefab != null)
        {
            Instantiate(explosionPrefab, targetPos, Quaternion.identity);
        }

        // �^�Ƕˮ`
        if (attacker != null && target != null)
        {
            //BattleEffectManager.Instance.OnHit(attacker, target);
            if (isPerfect)
            {
                // Perfect �ˮ`�[��
                BattleEffectManager.Instance.OnHit(attacker, target, true);
            }
            else
            {
                // ���q�ˮ`
                BattleEffectManager.Instance.OnHit(attacker, target, false);
            }
        }

        Destroy(gameObject);
    }
}
