using UnityEngine;
using System.Collections;

public class FireBallSkill : MonoBehaviour
{
    public BattleManager.TeamSlotInfo attacker;
    public BattleManager.TeamSlotInfo target;
    public GameObject explosionPrefab;
    public float travelTime = 0.05f;
    public bool isPerfect;


    [Header("UI �]�w")]
    public GameObject missTextPrefab;   // UI �W�� MissText prefab
    public Canvas uiCanvas;             // ���w�n�ͦ��� UI Canvas

    private void Awake()
    {
        if (uiCanvas == null)
        {
            uiCanvas = FindObjectOfType<Canvas>();
        }
    }


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
            if (isPerfect)
            {
                // Perfect�G�z���S�� + �ˮ`
                if (explosionPrefab != null)
                {
                    Instantiate(explosionPrefab, targetPos, Quaternion.identity);
                }
                BattleEffectManager.Instance.OnHit(attacker, target, true);
            }
            else
            {
                // Miss�GUI ����
                if (missTextPrefab != null && uiCanvas != null)
                {
                    ShowMissTextAtTarget(target.Actor.transform.position);
                }
                BattleEffectManager.Instance.OnHit(attacker, target, false);
            }
        }

        Destroy(gameObject);
    }

    private void ShowMissTextAtTarget(Vector3 worldPos)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        // �@����ù��y��
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

        // �ù��� Canvas �y��
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            uiCanvas.transform as RectTransform,
            screenPos,
            uiCanvas.worldCamera,
            out Vector2 localPos);

        // �ͦ� UI
        GameObject missText = Instantiate(missTextPrefab, uiCanvas.transform);
        missText.GetComponent<RectTransform>().anchoredPosition = localPos;
    }
}
