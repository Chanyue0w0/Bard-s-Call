using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    // ----------------------------------------
    // 1. 圖樣教學模式（4 頁教學 Panel）
    // ----------------------------------------
    [Header("圖樣教學 Panel（依順序排好，0~3 共 4 頁）")]
    public GameObject[] patternPanels;
    public GameObject patternBackGroundPanel;

    private int currentPatternIndex = 0;
    private bool patternPhaseFinished = false;

    // ----------------------------------------
    // 2. 操作教學：MissionHint
    // ----------------------------------------
    [Header("Mission Hint 相關")]
    public GameObject missionHintRoot;          // 整個 MissionHint 物件
    public Text missionHintText;            // 顯示文字的 TMP 元件
    [Header("Mission 完成特效")]
    public GameObject missionCompleteEffectPrefab;


    public enum TutorialCallType
    {
        Paladin,    // 呼叫聖騎士
        Bard,       // 呼叫吟遊詩人
        Mage        // 呼叫法師
    }

    // 顯示文字標題
    private readonly string[] missionTitles = new string[]
    {
        "使用聖騎士格擋",
        "使用吟遊詩人回血",
        "使用法師攻擊"
    };

    private int currentMissionIndex = -1;       // -1 表示還沒進入操作教學
    private int currentMissionCount = 0;
    public int missionTargetCount = 4;          // 0/4 ~ 4/4

    [Header("Input Actions")]
    public InputActionReference actionNextPage;
    public InputActionReference actionPreviousPage;

    // 用於安全解除 Input 綁定
    private System.Action<InputAction.CallbackContext> nextPageHandler;
    private System.Action<InputAction.CallbackContext> previousPageHandler;


    // ----------------------------------------
    // 3. 結束顯示：MissionFinishedHint
    // ----------------------------------------
    [Header("教學完成提示")]
    public GameObject missionFinishedHintRoot;  // MissionFinishedHint 物件
    private bool tutorialFinished = false;
    public GameObject closeTutorialButton; // UI 上的關閉教學按鈕

    [Header("離開教學事件（在 Inspector 綁定切換場景或開始戰鬥）")]
    public UnityEvent onExitTutorial;

    private void OnEnable()
    {
        if (!GlobalIndex.isTutorial)
            return;
        
        nextPageHandler = ctx => OnNextPage(ctx);
        previousPageHandler = ctx => OnPreviousPage(ctx);

        if (actionNextPage != null)
        {
            actionNextPage.action.performed += nextPageHandler;
            actionNextPage.action.Enable();
        }

        if (actionPreviousPage != null)
        {
            actionPreviousPage.action.performed += previousPageHandler;
            actionPreviousPage.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (!GlobalIndex.isTutorial)
            return;

        if (actionNextPage != null)
            actionNextPage.action.performed -= nextPageHandler;

        if (actionPreviousPage != null)
            actionPreviousPage.action.performed -= previousPageHandler;
    }


    // ----------------------------------------
    // 初始化
    // ----------------------------------------
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if(GlobalIndex.isTutorial && GlobalIndex.CurrentChapterIndex == 1 && GlobalIndex.CurrentLevelIndex == 0 && GlobalIndex.CurrentStageIndex == 0)
        {
            InitTutorial();
            //UpdateCloseButtonVisibility();
        }

    }

    private void InitTutorial()
    {
        GlobalIndex.isTutorialPanelOpened = true;

        // 圖樣教學：只開第一頁，其他關閉
        if (patternPanels != null && patternPanels.Length > 0)
        {
            for (int i = 0; i < patternPanels.Length; i++)
            {
                if (patternPanels[i] != null)
                    patternPanels[i].SetActive(i == 0);
            }
            currentPatternIndex = 0;
            patternPhaseFinished = false;
        }

        patternBackGroundPanel.SetActive(true);

        // 操作教學：一開始先關閉
        if (missionHintRoot != null)
            missionHintRoot.SetActive(false);

        currentMissionIndex = -1;
        currentMissionCount = 0;
        UpdateMissionHintText();

        // 結束提示：一開始關閉
        if (missionFinishedHintRoot != null)
            missionFinishedHintRoot.SetActive(false);

        tutorialFinished = false;
    }

    public void OnNextPage(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        NextPatternPage();
    }

    public void OnPreviousPage(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        PreviousPatternPage();
    }

    public void OnCloseTutorial(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        ClosePatternTutorial();
    }


    // ============================================================
    // 一、圖樣教學流程控制
    // ============================================================

    // 給按鈕或 Input System 呼叫：下一頁圖樣 Panel
    public void NextPatternPage()
    {
        if (tutorialFinished) return;
        if (patternPhaseFinished) return;

        if (patternPanels == null || patternPanels.Length == 0)
        {
            // 若沒有設定 Panel，直接進入操作教學
            StartMissionPhase();
            return;
        }

        // 關閉當前頁
        if (currentPatternIndex >= 0 &&
            currentPatternIndex < patternPanels.Length &&
            patternPanels[currentPatternIndex] != null)
        {
            patternPanels[currentPatternIndex].SetActive(false);
        }

        // 換下一頁
        currentPatternIndex++;

        if (currentPatternIndex >= patternPanels.Length)
        {
            // 全部看完，進入操作教學
            patternPhaseFinished = true;
            patternBackGroundPanel.SetActive(false);
            StartMissionPhase();
        }
        else
        {
            // 顯示下一頁
            if (patternPanels[currentPatternIndex] != null)
                patternPanels[currentPatternIndex].SetActive(true);
        }
        //UpdateCloseButtonVisibility();

    }

    public void PreviousPatternPage()
    {
        if (tutorialFinished) return;
        if (patternPhaseFinished) return;

        if (patternPanels == null || patternPanels.Length == 0)
            return;

        if (currentPatternIndex <= 0)
            return; // 已經是第一頁，不能再往前

        // 關閉當前頁
        if (patternPanels[currentPatternIndex] != null)
            patternPanels[currentPatternIndex].SetActive(false);

        // 回上一頁
        currentPatternIndex--;

        // 開啟上一頁
        if (patternPanels[currentPatternIndex] != null)
            patternPanels[currentPatternIndex].SetActive(true);

        //UpdateCloseButtonVisibility();
    }


    // 直接跳過圖樣教學，開始操作教學
    public void SkipPatternAndStartMission()
    {
        if (tutorialFinished) return;

        patternPhaseFinished = true;

        // 把圖樣教學全部關掉
        if (patternPanels != null)
        {
            foreach (var panel in patternPanels)
            {
                if (panel != null) panel.SetActive(false);
            }
        }

        patternBackGroundPanel.SetActive(false);
        StartMissionPhase();
    }

    public void ClosePatternTutorial()
    {
        if (tutorialFinished) return;

        // 只能在最後一頁按
        if (currentPatternIndex != patternPanels.Length - 1)
            return;

        SkipPatternAndStartMission();
    }

    //private void UpdateCloseButtonVisibility()
    //{
    //    if (closeTutorialButton == null) return;

    //    bool isLastPage = (currentPatternIndex == patternPanels.Length - 1);
    //    closeTutorialButton.SetActive(isLastPage);
    //}


    // ============================================================
    // 二、操作教學：MissionHint 流程
    // ============================================================

    private void StartMissionPhase()
    {
        if (missionHintRoot != null)
            missionHintRoot.SetActive(true);

        if (missionFinishedHintRoot != null)
            missionFinishedHintRoot.SetActive(false);

        GlobalIndex.isTutorialPanelOpened = false;

        currentMissionIndex = 0;
        currentMissionCount = 0;
        UpdateMissionHintText();
    }

    private void UpdateMissionHintText()
    {
        if (missionHintText == null) return;

        if (currentMissionIndex < 0 || currentMissionIndex >= missionTitles.Length)
        {
            // 還沒開始或已經超出範圍就不更新
            missionHintText.text = "";
            return;
        }

        string title = missionTitles[currentMissionIndex];
        int shownCount = Mathf.Clamp(currentMissionCount, 0, missionTargetCount);

        missionHintText.text = string.Format("{0}\n{1}/{2}", title, shownCount, missionTargetCount);
    }

    private void GoToNextMission()
    {
        currentMissionIndex++;

        if (currentMissionIndex >= missionTitles.Length)
        {
            // 全部教學任務完成
            FinishTutorial();
        }
        else
        {
            currentMissionCount = 0;
            UpdateMissionHintText();
        }
    }

    public void AddMissionProgress(TutorialCallType callType)
    {
        if (tutorialFinished) return;     // 教學已結束，不再計算
        if (currentMissionIndex < 0) return; // 還沒開始，不計算
        if (currentMissionIndex >= missionTitles.Length) return;

        // 檢查目前任務與收到的類型是否一致
        bool match = false;

        switch (currentMissionIndex)
        {
            case 0: match = (callType == TutorialCallType.Paladin); break;
            case 1: match = (callType == TutorialCallType.Bard); break;
            case 2: match = (callType == TutorialCallType.Mage); break;
        }

        if (!match) return;

        // 累加
        currentMissionCount++;
        if (currentMissionCount > missionTargetCount)
            currentMissionCount = missionTargetCount;

        // 更新 UI
        UpdateMissionHintText();

        // 完成當前階段
        if (currentMissionCount >= missionTargetCount)
        {
            PlayMissionCompleteEffect();  // ★ 這裡新增
            GoToNextMission();
        }

    }

    private void PlayMissionCompleteEffect()
    {
        if (missionCompleteEffectPrefab == null || missionHintRoot == null)
            return;

        // 產生特效在 UI 裡
        GameObject effect = Instantiate(missionCompleteEffectPrefab, missionHintRoot.transform);

        // 避免超大或遮到 UI
        RectTransform rt = effect.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
        }
    }


    // ============================================================
    // 三、教學結束：顯示 MissionFinishedHint，等待 Option 長按
    // ============================================================

    private void FinishTutorial()
    {
        tutorialFinished = true;

        if (missionHintRoot != null)
            missionHintRoot.SetActive(false);

        //if (missionFinishedHintRoot != null)
            //missionFinishedHintRoot.SetActive(true);

        // 之後等玩家長按 Option（InputSystem）呼叫 OnOptionExitTutorial
    }

    // 這個函式給 PlayerInput 的 Action（Option 長按）呼叫
    // Input Action 設定成「Hold」，在 performed 時就會觸發
    public void OnOptionExitTutorial(InputAction.CallbackContext context)
    {
        if (!tutorialFinished) return;  // 只有教學完成後才允許離開
        if (!context.performed) return;

        if (onExitTutorial != null)
            onExitTutorial.Invoke();
    }

}
