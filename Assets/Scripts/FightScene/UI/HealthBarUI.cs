using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private Text hpText; // ★ 新增：顯示血量的文字

    private Transform target;                // 角色的頭部位置
    private Camera uiCamera;
    private RectTransform rectTransform;
    private BattleManager.TeamSlotInfo info; // 關聯角色資訊

    public void Init(BattleManager.TeamSlotInfo slotInfo, Transform headPoint, Camera canvasCamera = null)
    {
        info = slotInfo;
        target = headPoint;
        uiCamera = canvasCamera != null ? canvasCamera : Camera.main;
        rectTransform = GetComponent<RectTransform>();

        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
        }

        UpdateUI();
    }

    public void ForceUpdate()
    {
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (info == null) return;

        // 更新血條數值
        if (slider != null)
            slider.value = Mathf.Clamp01((float)info.HP / info.MaxHP);

        // ★ 更新文字顯示
        if (hpText != null)
            hpText.text = info.HP + ""; //+" / " + info.MaxHP
    }

    void Update()
    {
        if (info == null || info.Actor == null)
        {
            Destroy(gameObject);
            return;
        }

        if (target != null && uiCamera != null)
        {
            // 世界座標轉螢幕座標
            Vector3 screenPos = uiCamera.WorldToScreenPoint(target.position);

            // 螢幕座標轉 Canvas 的本地座標
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform.parent as RectTransform,
                screenPos,
                uiCamera,
                out Vector2 localPos);

            rectTransform.localPosition = localPos;
        }

        UpdateUI();
    }
}
