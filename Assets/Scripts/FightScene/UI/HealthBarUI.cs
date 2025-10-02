using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private Slider slider;
    private Transform target;                // ���⪺�Y����m
    private Camera uiCamera;
    private RectTransform rectTransform;
    private BattleManager.TeamSlotInfo info; // ���p�����T

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
        if (info == null || slider == null) return;
        slider.value = Mathf.Clamp01((float)info.HP / info.MaxHP);
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
            // �@�ɮy����ù��y��
            Vector3 screenPos = uiCamera.WorldToScreenPoint(target.position);

            // �ù��y���� Canvas �����a�y��
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
