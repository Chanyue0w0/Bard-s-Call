using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class HeavyAttackBarUI : MonoBehaviour
{
    [Header("參數設定")]
    [Tooltip("重攻擊的最大次數 (預設4次)")]
    public int maxCount = 4;

    [Tooltip("劍圖示Prefab (需為 Image)")]
    public GameObject swordIconPrefab;

    [Tooltip("劍圖示之間的間距 (像素)")]
    public float iconSpacing = 40f;

    [Tooltip("圖示亮起時顏色")]
    public Color activeColor = Color.white;

    [Tooltip("圖示未亮起時顏色")]
    public Color inactiveColor = new Color(1, 1, 1, 0.25f);

    [Tooltip("是否讓UI跟隨角色頭頂位置")]
    public bool followTarget = true;

    [Tooltip("相對 HeadPoint 的畫面偏移")]
    public Vector2 screenOffset = new Vector2(-10f, 30f);

    private List<Image> swordIcons = new List<Image>();
    private CharacterComboState comboState;
    private Transform target;               // 跟隨角色頭部
    private Camera uiCamera;
    private RectTransform rectTransform;



    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    // 初始化
    public void Init(CharacterComboState state, Transform headPoint, Camera canvasCamera = null)
    {
        comboState = state;
        target = headPoint;
        uiCamera = canvasCamera != null ? canvasCamera : Camera.main;

        GenerateIcons();
        UpdateUI();
    }

    // 生成劍圖示
    private void GenerateIcons()
    {
        // 清空舊圖示
        foreach (Transform child in transform)
            Destroy(child.gameObject);
        swordIcons.Clear();

        if (swordIconPrefab == null)
        {
            Debug.LogWarning("HeavyAttackBarUI：未指定劍圖示Prefab。");
            return;
        }

        for (int i = 0; i < maxCount; i++)
        {
            GameObject icon = Instantiate(swordIconPrefab, transform);
            RectTransform iconRect = icon.GetComponent<RectTransform>();
            iconRect.anchoredPosition = new Vector2(i * iconSpacing, 0);

            Image img = icon.GetComponent<Image>();
            if (img != null)
            {
                img.color = inactiveColor;
                swordIcons.Add(img);
            }
        }
    }

    // 根據 comboCount 更新顯示
    public void UpdateUI()
    {
        if (comboState == null) return;

        int currentCount = Mathf.Clamp(comboState.comboCount, 0, maxCount);

        for (int i = 0; i < swordIcons.Count; i++)
        {
            if (swordIcons[i] == null) continue;
            swordIcons[i].color = i < currentCount ? activeColor : inactiveColor;
        }
    }

    // 提供外部更新函式
    public void UpdateComboCount(int count)
    {
        if (comboState != null)
            comboState.comboCount = Mathf.Clamp(count, 0, maxCount);
        UpdateUI();
    }

    void Update()
    {
        if (followTarget && target != null && uiCamera != null)
        {
            Vector3 screenPos = uiCamera.WorldToScreenPoint(target.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform.parent as RectTransform,
                screenPos,
                uiCamera,
                out Vector2 localPos);
            rectTransform.localPosition = localPos + screenOffset;

        }

        UpdateUI();
    }
}
