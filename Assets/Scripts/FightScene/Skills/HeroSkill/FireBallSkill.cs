using UnityEngine;
using System.Collections;

public class FireBallSkill : MonoBehaviour
{
    public BattleManager.TeamSlotInfo attacker;
    public BattleManager.TeamSlotInfo target;
    public GameObject explosionPrefab;
    public float travelTime = 0.05f;
    public bool isPerfect;


    [Header("UI 設定")]
    public GameObject missTextPrefab;   // UI 上的 MissText prefab
    public Canvas uiCanvas;             // 指定要生成的 UI Canvas

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
                // Perfect：爆炸特效 + 傷害
                if (explosionPrefab != null)
                {
                    Instantiate(explosionPrefab, targetPos, Quaternion.identity);
                }
                BattleEffectManager.Instance.OnHit(attacker, target, true);
            }
            else
            {
                // Miss：UI 提示
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

        // 世界轉螢幕座標
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

        // 螢幕轉 Canvas 座標
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            uiCanvas.transform as RectTransform,
            screenPos,
            uiCanvas.worldCamera,
            out Vector2 localPos);

        // 生成 UI
        GameObject missText = Instantiate(missTextPrefab, uiCanvas.transform);
        missText.GetComponent<RectTransform>().anchoredPosition = localPos;
    }
}
