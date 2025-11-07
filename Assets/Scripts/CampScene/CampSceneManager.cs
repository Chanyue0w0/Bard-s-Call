using UnityEngine;
using UnityEngine.SceneManagement;

public class CampSceneManager : MonoBehaviour
{
    [Header("戰鬥場景名稱")]
    public string fightSceneName = "FightScene";

    // Start 按鈕呼叫這個函式
    public void OnStartButtonPressed()
    {
        Debug.Log("StartButton 被按下，準備切換至戰鬥場景...");
        LoadFightScene();
    }

    private void LoadFightScene()
    {
        // 切換至指定場景
        SceneManager.LoadScene(fightSceneName);
    }
}
