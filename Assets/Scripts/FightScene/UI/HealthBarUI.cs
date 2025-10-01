using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    public Slider slider;
    private Transform target;   // 角色的頭部位置
    private Camera mainCamera;
    private BattleManager.TeamSlotInfo info; // 關聯角色資訊

    public void Init(BattleManager.TeamSlotInfo slotInfo, Transform headPoint)
    {
        info = slotInfo;
        target = headPoint;
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (info == null || info.Actor == null)
        {
            Destroy(gameObject);
            return;
        }

        // 更新位置（世界座標 → 螢幕座標）
        Vector3 screenPos = mainCamera.WorldToScreenPoint(target.position);
        transform.position = screenPos;

        // 更新血量 (0 ~ 1)
        slider.value = (float)info.HP / info.MaxHP;
    }
}
