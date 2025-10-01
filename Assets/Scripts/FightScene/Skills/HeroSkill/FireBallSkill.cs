using UnityEngine;
using System.Collections;

public class FireBallSkill : MonoBehaviour
{
    public BattleManager.TeamSlotInfo attacker;
    public BattleManager.TeamSlotInfo target;
    public GameObject explosionPrefab;
    public float travelTime = 0.05f;

    private void Start()
    {
        if (target != null && target.SlotTransform != null)
        {
            StartCoroutine(MoveToTarget(target.SlotTransform.position));
        }
        else
        {
            Destroy(gameObject); // 沒有目標直接刪除
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

        // 抵達目標後生成爆炸
        if (explosionPrefab != null)
        {
            Instantiate(explosionPrefab, targetPos, Quaternion.identity);
        }

        // 回傳傷害
        if (attacker != null && target != null)
        {
            BattleEffectManager.Instance.OnHit(attacker, target);
        }

        Destroy(gameObject);
    }
}
