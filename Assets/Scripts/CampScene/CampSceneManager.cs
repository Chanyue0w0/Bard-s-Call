using UnityEngine;
using UnityEngine.SceneManagement;

public class CampSceneManager : MonoBehaviour
{
    [Header("戰鬥場景名稱")]
    public string fightSceneName = "FightScene";

    [Header("關卡選擇面板")]
    public GameObject levelChoosePanel;

    public void OnStartButtonPressed()
    {
        Debug.Log("StartButton 被按下，準備打開關卡選擇面板...");
        OpenLevelChoosePanel();
    }

    public void OpenLevelChoosePanel()
    {
        if (levelChoosePanel != null)
        {
            levelChoosePanel.SetActive(true);
            Debug.Log("LevelChoosePanel 已打開");
        }
        else
        {
            Debug.LogWarning("LevelChoosePanel 未指定");
        }
    }

    public void CloseLevelChoosePanel()
    {
        if (levelChoosePanel != null)
        {
            levelChoosePanel.SetActive(false);
            Debug.Log("LevelChoosePanel 已關閉");
        }
        else
        {
            Debug.LogWarning("LevelChoosePanel 未指定");
        }
    }

    public void EasyModeStart()
    {
        GlobalIndex.CurrentChapterIndex = 1;
        GlobalIndex.CurrentLevelIndex = 1;
        GlobalIndex.CurrentStageIndex = 0;

        Debug.Log("啟動簡單模式：Chapter=1, Level=1, Stage=0");
        LoadFightScene();
    }

    public void HardModeStart()
    {
        GlobalIndex.CurrentChapterIndex = 1;
        GlobalIndex.CurrentLevelIndex = 2;
        GlobalIndex.CurrentStageIndex = 0;

        Debug.Log("啟動困難模式：Chapter=1, Level=2, Stage=0");
        LoadFightScene();
    }

    private void LoadFightScene()
    {
        Debug.Log("切換至戰鬥場景：" + fightSceneName);
        SceneManager.LoadScene(fightSceneName);
    }
}
