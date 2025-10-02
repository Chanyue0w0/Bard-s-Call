using UnityEngine;

public class SwordHitSkill : MonoBehaviour
{
    public BattleManager.TeamSlotInfo attacker;
    public BattleManager.TeamSlotInfo target;
    public GameObject explosionPrefab;
    public float lifeTime = 0.3f;
    public bool isPerfect;

    [Header("UI �]�w")]
    public GameObject missTextPrefab;   // MissText UI Prefab
    public Canvas uiCanvas;             // �������� Canvas

    private void Awake()
    {
        // �p�G�S����ʫ��w�A�|�۰ʧ������ Canvas
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
                // Perfect�G�ͦ��z���S��
                if (explosionPrefab != null)
                {
                    Instantiate(explosionPrefab, transform.position, Quaternion.identity);
                }

                BattleEffectManager.Instance.OnHit(attacker, target, true);
            }
            else
            {
                // Miss�G�ͦ� UI ��r
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

        // �@�� �� �ù��y��
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

        // �ù� �� Canvas local �y��
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            uiCanvas.transform as RectTransform,
            screenPos,
            cam,
            out Vector2 localPos);

        // �ͦ� MissText UI
        GameObject missText = Instantiate(missTextPrefab, uiCanvas.transform);
        missText.GetComponent<RectTransform>().anchoredPosition = localPos;
    }
}
