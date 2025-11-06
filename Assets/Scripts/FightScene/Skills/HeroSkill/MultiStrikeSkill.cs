using System.Collections.Generic;
using UnityEngine;

public class MultiStrikeSkill : MonoBehaviour
{
    [Header("角色設定")]
    public BattleManager.TeamSlotInfo attacker;          // 攻擊者
    public List<BattleManager.TeamSlotInfo> targets;     // 群體攻擊目標

    [Header("特效設定")]
    public GameObject explosionPrefab;
    public float lifeTime = 0.5f;
    public bool isPerfect;
    public bool isHeavyAttack;
    public int damage = 0;

    [Header("UI 設定")]
    public GameObject missTextPrefab;
    public Canvas uiCanvas;

    private HashSet<GameObject> alreadyHit = new HashSet<GameObject>(); // 避免重複判定

    private void Awake()
    {
        if (uiCanvas == null)
            uiCanvas = FindObjectOfType<Canvas>();
    }

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 確認是否有targets清單
        if (targets == null || targets.Count == 0) return;

        // 檢查是否為其中一個目標
        foreach (var t in targets)
        {
            if (t == null || t.Actor == null) continue;
            if (other.gameObject != t.Actor) continue;
            if (alreadyHit.Contains(t.Actor)) continue; // 避免同一目標重複判定

            alreadyHit.Add(t.Actor);

            // Perfect命中邏輯
            if (isPerfect)
            {
                if (explosionPrefab != null)
                    Instantiate(explosionPrefab, transform.position, Quaternion.identity);

                BattleEffectManager.Instance.OnHit(attacker, t, true, isHeavyAttack, damage);
            }
            else
            {
                if (explosionPrefab != null)
                    Instantiate(explosionPrefab, transform.position, Quaternion.identity);

                BattleEffectManager.Instance.OnHit(attacker, t, false, isHeavyAttack, damage);
            }

            break; // 找到符合的目標後結束本回合檢查（或可移除此行讓可連擊多個敵人）
        }
    }

    private void ShowMissTextAtTarget(Vector3 worldPos)
    {
        if (missTextPrefab == null || uiCanvas == null) return;
        Camera cam = uiCanvas.worldCamera;
        if (cam == null) return;

        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            uiCanvas.transform as RectTransform,
            screenPos,
            cam,
            out Vector2 localPos);

        GameObject missText = Instantiate(missTextPrefab, uiCanvas.transform);
        missText.GetComponent<RectTransform>().anchoredPosition = localPos;
    }
}
