using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class HeavyAttackBarUI : MonoBehaviour
{
    [Header("�ѼƳ]�w")]
    [Tooltip("���������̤j���� (�w�]4��)")]
    public int maxCount = 4;

    [Tooltip("�C�ϥ�Prefab (�ݬ� Image)")]
    public GameObject swordIconPrefab;

    [Tooltip("�C�ϥܤ��������Z (����)")]
    public float iconSpacing = 40f;

    [Tooltip("�ϥܫG�_���C��")]
    public Color activeColor = Color.white;

    [Tooltip("�ϥܥ��G�_���C��")]
    public Color inactiveColor = new Color(1, 1, 1, 0.25f);

    [Tooltip("�O�_��UI���H�����Y����m")]
    public bool followTarget = true;

    [Tooltip("�۹� HeadPoint ���e������")]
    public Vector2 screenOffset = new Vector2(-10f, 30f);

    private List<Image> swordIcons = new List<Image>();
    private CharacterComboState comboState;
    private Transform target;               // ���H�����Y��
    private Camera uiCamera;
    private RectTransform rectTransform;



    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    // ��l��
    public void Init(CharacterComboState state, Transform headPoint, Camera canvasCamera = null)
    {
        comboState = state;
        target = headPoint;
        uiCamera = canvasCamera != null ? canvasCamera : Camera.main;

        GenerateIcons();
        UpdateUI();
    }

    // �ͦ��C�ϥ�
    private void GenerateIcons()
    {
        // �M���¹ϥ�
        foreach (Transform child in transform)
            Destroy(child.gameObject);
        swordIcons.Clear();

        if (swordIconPrefab == null)
        {
            Debug.LogWarning("HeavyAttackBarUI�G�����w�C�ϥ�Prefab�C");
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

    // �ھ� comboCount ��s���
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

    // ���ѥ~����s�禡
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
