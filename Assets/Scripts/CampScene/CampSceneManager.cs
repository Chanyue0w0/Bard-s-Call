using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class CampSceneManager : MonoBehaviour
{
    [Header("戰鬥場景名稱")]
    public string fightSceneName = "FightSceneFMOD";

    [Header("關卡選擇面板")]
    public GameObject levelChoosePanel;
    private bool levelChoosePanelAcive = false;

    [Header("可選關卡列表 (0=教學, 1=簡單, 2=困難)")]
    public List<int> levelList = new List<int>() { 0, 1, 2 };

    [Header("目前選中的關卡 Index")]
    public int currentLevelIndex = 0;

    // ------------------------------------------------------------
    // ★★★ 新增：對應每個關卡的提示圖片（按選擇關卡顯示）
    // ------------------------------------------------------------
    [Header("關卡提示圖片（依序放 0=教學 / 1=簡單 / 2=困難）")]
    public List<GameObject> levelHintImages = new List<GameObject>();


    // ------------------------------------------------------------
    // Input System — ActionReference
    // ------------------------------------------------------------
    [Header("輸入（新 Input System）")]
    public InputActionReference actionStartGame;
    public InputActionReference actionNextLevel;
    public InputActionReference actionLastLevel;
    public InputActionReference actionGoLevel;
    public InputActionReference actionCloseMap;

    // ------------------------------------------------------------
    // Handlers（安全解除用）
    // ------------------------------------------------------------
    private System.Action<InputAction.CallbackContext> startGameHandler;
    private System.Action<InputAction.CallbackContext> nextLevelHandler;
    private System.Action<InputAction.CallbackContext> lastLevelHandler;
    private System.Action<InputAction.CallbackContext> goLevelHandler;
    private System.Action<InputAction.CallbackContext> closeMapHandler;


    // ------------------------------------------------------------
    // Awake：預設選擇關卡 = 0
    // ------------------------------------------------------------
    private void Start()
    {
        currentLevelIndex = 0;
        UpdateLevelHintImage();
    }

    // ------------------------------------------------------------
    // OnEnable 綁定
    // ------------------------------------------------------------
    private void OnEnable()
    {
        startGameHandler = ctx => OnStartGame();
        nextLevelHandler = ctx => OnNextLevel();
        lastLevelHandler = ctx => OnLastLevel();
        goLevelHandler = ctx => OnGoLevel();
        closeMapHandler = ctx => OnCloseMap();

        if (actionStartGame != null)
        {
            actionStartGame.action.performed += startGameHandler;
            actionStartGame.action.Enable();
        }

        if (actionNextLevel != null)
        {
            actionNextLevel.action.performed += nextLevelHandler;
            actionNextLevel.action.Enable();
        }

        if (actionLastLevel != null)
        {
            actionLastLevel.action.performed += lastLevelHandler;
            actionLastLevel.action.Enable();
        }

        if (actionGoLevel != null)
        {
            actionGoLevel.action.performed += goLevelHandler;
            actionGoLevel.action.Enable();
        }

        if (actionCloseMap != null)
        {
            actionCloseMap.action.performed += closeMapHandler;
            actionCloseMap.action.Enable();
        }
    }

    // ------------------------------------------------------------
    // OnDisable
    // ------------------------------------------------------------
    private void OnDisable()
    {
        if (actionStartGame != null)
            actionStartGame.action.performed -= startGameHandler;

        if (actionNextLevel != null)
            actionNextLevel.action.performed -= nextLevelHandler;

        if (actionLastLevel != null)
            actionLastLevel.action.performed -= lastLevelHandler;

        if (actionGoLevel != null)
            actionGoLevel.action.performed -= goLevelHandler;

        if (actionCloseMap != null)
            actionCloseMap.action.performed -= closeMapHandler;
    }



    // ------------------------------------------------------------
    // 1. StartGame
    // ------------------------------------------------------------
    private void OnStartGame()
    {
        Debug.Log("按下 StartGame：開啟選擇面板");
        OpenLevelChoosePanel();
        UpdateLevelHintImage();
    }

    // ------------------------------------------------------------
    // 2. NextLevel
    // ------------------------------------------------------------
    private void OnNextLevel()
    {
        currentLevelIndex++;
        if (currentLevelIndex >= levelList.Count)
            currentLevelIndex = 0;

        Debug.Log("按下 NextLevel：切換到關卡 index = " + currentLevelIndex);
        UpdateLevelHintImage();
    }

    // ------------------------------------------------------------
    // 3. LastLevel
    // ------------------------------------------------------------
    private void OnLastLevel()
    {
        currentLevelIndex--;
        if (currentLevelIndex < 0)
            currentLevelIndex = levelList.Count - 1;

        Debug.Log("按下 LastLevel：切換到關卡 index = " + currentLevelIndex);
        UpdateLevelHintImage();
    }

    // ------------------------------------------------------------
    // 4. GoLevel
    // ------------------------------------------------------------
    private void OnGoLevel()
    {
        Debug.Log("按下 GoLevel：讀取關卡 Index = " + currentLevelIndex);

        int selected = levelList[currentLevelIndex];

        switch (selected)
        {
            case 0: TutorialModeStart(); break;
            case 1: EasyModeStart(); break;
            case 2: HardModeStart(); break;

            default:
                Debug.LogWarning("未知的關卡 ID: " + selected);
                break;
        }
    }

    // ------------------------------------------------------------
    // 5. CloseMap
    // ------------------------------------------------------------
    private void OnCloseMap()
    {
        Debug.Log("按下 CloseMap：關閉地圖面板");
        CloseLevelChoosePanel();
    }

    // ------------------------------------------------------------
    // ★★★ 關卡提示圖更新（你要的重點）
    // ------------------------------------------------------------
    private void UpdateLevelHintImage()
    {
        if (levelHintImages == null || levelHintImages.Count == 0)
            return;

        for (int i = 0; i < levelHintImages.Count; i++)
        {
            if (levelHintImages[i] != null)
                levelHintImages[i].SetActive(i == currentLevelIndex);
        }
    }

    // ------------------------------------------------------------
    // UI Panel 控制
    // ------------------------------------------------------------
    public void OpenLevelChoosePanel()
    {
        if (levelChoosePanel != null)
            levelChoosePanel.SetActive(true);
        else
            Debug.LogWarning("LevelChoosePanel 未指定");

        levelChoosePanelAcive = true;
    }

    public void CloseLevelChoosePanel()
    {
        if (levelChoosePanel != null)
            levelChoosePanel.SetActive(false);
        else
            Debug.LogWarning("LevelChoosePanel 未指定");

        levelChoosePanelAcive = false;
    }

    // ------------------------------------------------------------
    // Level Start
    // ------------------------------------------------------------
    public void TutorialModeStart()
    {
        GlobalIndex.CurrentChapterIndex = 1;
        GlobalIndex.CurrentLevelIndex = 0;
        GlobalIndex.CurrentStageIndex = 0;
        GlobalIndex.isTutorial = true;

        Debug.Log("啟動教學模式");
        LoadFightScene();
    }

    public void EasyModeStart()
    {
        GlobalIndex.CurrentChapterIndex = 1;
        GlobalIndex.CurrentLevelIndex = 1;
        GlobalIndex.CurrentStageIndex = 0;
        GlobalIndex.isTutorial = false;

        Debug.Log("啟動簡單模式");
        LoadFightScene();
    }

    public void HardModeStart()
    {
        GlobalIndex.CurrentChapterIndex = 1;
        GlobalIndex.CurrentLevelIndex = 2;
        GlobalIndex.CurrentStageIndex = 0;
        GlobalIndex.isTutorial = false;

        Debug.Log("啟動困難模式");
        LoadFightScene();
    }

    private void LoadFightScene()
    {
        GlobalIndex.CurrentTotalHP = 200;
        GlobalIndex.MaxTotalHP = 200;
        GlobalIndex.RythmResonanceBuff = 0;

        Debug.Log("切換至戰鬥場景：" + fightSceneName);
        SceneManager.LoadScene(fightSceneName);
    }
}
