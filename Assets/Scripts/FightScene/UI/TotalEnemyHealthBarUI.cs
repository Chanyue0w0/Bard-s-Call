using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TotalEnemyHealthBarUI : MonoBehaviour
{
    [Header("UI 元件")]
    [SerializeField] private Slider slider;
    [SerializeField] private Text hpText; // 若不需要可在 Inspector 移除

    private int currentHP = 0;
    private int maxHP = 1;

    private void Awake()
    {
        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
        }
    }

    // ================================================================
    //  對外 API（與 TotalPlayerHealthBarUI 保持一致）
    // ================================================================

    // 設定 HP 數值
    public void SetHP(int current, int max)
    {
        if (max <= 0) max = 1;

        currentHP = Mathf.Clamp(current, 0, max);
        maxHP = max;

        UpdateUI();
    }

    // 強制更新 UI（給外部呼叫）
    public void ForceUpdate()
    {
        UpdateUI();
    }

    // ================================================================
    //  UI 更新邏輯
    // ================================================================
    private void UpdateUI()
    {
        if (slider != null)
            slider.value = Mathf.Clamp01((float)currentHP / maxHP);

        if (hpText != null)
            hpText.text = currentHP.ToString();
    }

}
