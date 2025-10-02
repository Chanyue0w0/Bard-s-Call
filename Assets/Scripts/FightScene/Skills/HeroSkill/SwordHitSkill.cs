using UnityEngine;

public class SwordHitSkill : MonoBehaviour
{
    public BattleManager.TeamSlotInfo attacker;
    public BattleManager.TeamSlotInfo target;
    public GameObject explosionPrefab;
    public float lifeTime = 0.3f;
    public bool isPerfect;

    [Header("UI 設定")]
    public GameObject missTextPrefab;   // MissText UI Prefab
    public Canvas uiCanvas;             // 場景中的 Canvas

    private void Awake()
    {
        // 如果沒有手動指定，會自動找場景的 Canvas
        if (uiCanvas == null)
        {
            uiCanvas = FindObjectOfType<Canvas>();
        }
    }

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (target != null && target.Actor != null && other.gameObject == target.Actor)
        {
            if (isPerfect)
            {
                // Perfect：生成爆炸特效
                if (explosionPrefab != null)
                {
                    Instantiate(explosionPrefab, transform.position, Quaternion.identity);
                }

                BattleEffectManager.Instance.OnHit(attacker, target, true);
            }
            else
            {
                // Miss：生成 UI 文字
                if (missTextPrefab != null && uiCanvas != null)
                {
                    ShowMissTextAtTarget(target.Actor.transform.position);
                }

                BattleEffectManager.Instance.OnHit(attacker, target, false);
            }

            Destroy(gameObject, lifeTime);
        }
    }

    private void ShowMissTextAtTarget(Vector3 worldPos)
    {
        Camera cam = uiCanvas.worldCamera;
        if (cam == null) return;

        // 世界 → 螢幕座標
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

        // 螢幕 → Canvas local 座標
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            uiCanvas.transform as RectTransform,
            screenPos,
            cam,
            out Vector2 localPos);

        // 生成 MissText UI
        GameObject missText = Instantiate(missTextPrefab, uiCanvas.transform);
        missText.GetComponent<RectTransform>().anchoredPosition = localPos;
    }
}
