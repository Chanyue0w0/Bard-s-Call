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
        Destroy(gameObject, 2f); // «O©³¾P·´¡AÁ×§K¥d¦í
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
    }

    private void OnTriggerEnter(Collider other)
    {
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
