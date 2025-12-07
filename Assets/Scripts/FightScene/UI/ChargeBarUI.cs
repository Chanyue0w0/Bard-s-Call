using UnityEngine;
using UnityEngine.UI;

public class ChargeBarUI : MonoBehaviour
{
    [Header("World Follow")]
    public Transform worldFollowTarget;
    public Vector3 worldOffset;

    private RectTransform rect;
    private Canvas canvas;
    private Camera uiCamera;

    [Header("UI")]
    public Slider slider;     // ★ 直接使用 Slider

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();

        if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            uiCamera = canvas.worldCamera;
        else
            uiCamera = Camera.main;

        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
        }
    }

    void LateUpdate()
    {
        if (worldFollowTarget == null) return;

        // 世界轉螢幕
        Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(uiCamera, worldFollowTarget.position + worldOffset);

        // 螢幕轉 Canvas
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            screenPos,
            uiCamera,
            out Vector2 localPos);

        rect.anchoredPosition = localPos;
    }

    public void SetActive(bool b)
    {
        gameObject.SetActive(b);
    }

    public void SetValue(float v)
    {
        if (slider != null)
            slider.value = Mathf.Clamp01(v);
    }
}
