using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    public Slider slider;
    private Transform target;   // ���⪺�Y����m
    private Camera mainCamera;
    private BattleManager.TeamSlotInfo info; // ���p�����T

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

        // ��s��m�]�@�ɮy�� �� �ù��y�С^
        Vector3 screenPos = mainCamera.WorldToScreenPoint(target.position);
        transform.position = screenPos;

        // ��s��q (0 ~ 1)
        slider.value = (float)info.HP / info.MaxHP;
    }
}
