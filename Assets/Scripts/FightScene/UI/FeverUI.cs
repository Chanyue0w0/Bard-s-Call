using UnityEngine;
using UnityEngine.UI;

public class FeverUI : MonoBehaviour
{
    [Header("Fever Slider 組件")]
    [SerializeField] private Slider feverSlider;

    [Header("亮光特效物件")]
    [SerializeField] private GameObject feverGlow;   // 指向子物件中的亮光特效

    [Header("參數設定")]
    public float updateSpeed = 5f;  // 補間平滑速度

    private float targetValue = 0f; // 目標值 (0~1)
    private float currentValue = 0f;

    private void Awake()
    {
        if (feverSlider == null)
            feverSlider = GetComponentInChildren<Slider>();

        // 初始化 Slider 範圍
        if (feverSlider != null)
        {
            feverSlider.minValue = 0f;
            feverSlider.maxValue = 1f;
            feverSlider.wholeNumbers = false;
            feverSlider.value = 0f;
        }

        // 初始化亮光特效關閉
        if (feverGlow != null)
            feverGlow.SetActive(false);
    }

    private void Update()
    {
        // 線性補間更新條件
        if (Mathf.Abs(feverSlider.value - targetValue) > 0.001f)
        {
            currentValue = Mathf.Lerp(feverSlider.value, targetValue, Time.unscaledDeltaTime * updateSpeed);
            feverSlider.value = currentValue;
        }

        // 若為滿值，開啟亮光特效；否則關閉
        if (feverGlow != null)
        {
            bool isFull = feverSlider.value >= 0.999f;
            if (feverGlow.activeSelf != isFull)
                feverGlow.SetActive(isFull);
        }
    }

    // ------------------------------------------------------------
    // 外部呼叫介面（由 FeverManager 自動更新）
    // ------------------------------------------------------------
    public void SetFeverValue(float normalizedValue)
    {
        targetValue = Mathf.Clamp01(normalizedValue);
    }

    // 可用於手動重置（例如重新開場）
    public void ResetFeverBar()
    {
        targetValue = 0f;
        if (feverSlider != null)
            feverSlider.value = 0f;

        if (feverGlow != null)
            feverGlow.SetActive(false);
    }
}
