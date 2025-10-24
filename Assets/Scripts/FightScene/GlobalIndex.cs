using System.Collections.Generic;
using UnityEngine;

public class GlobalIndex : MonoBehaviour
{
    public static GlobalIndex Instance { get; private set; }

    // -------------------------------
    // 全域遊戲變數
    // -------------------------------

    // 關卡資訊
    public static int CurrentStageIndex = 0;
    public static string CurrentStageName = "Forest01";
    public static string NextSceneName = "BattleScene";

    // 隊伍資訊
    public static List<GameObject> PlayerTeamPrefabs = new List<GameObject>(); // 我方角色 Prefabs
    public static List<GameObject> EnemyTeamPrefabs = new List<GameObject>();  // 敵方角色 Prefabs

    // 系統設定
    public static int DifficultyLevel = 1; // 1=Normal, 2=Hard, 3=Extreme
    public static bool IsTutorialCleared = false;

    // -------------------------------
    // 單例初始化
    // -------------------------------
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // -------------------------------
    // 對外方法：設定隊伍資料
    // -------------------------------
    public static void SetPlayerTeam(params GameObject[] prefabs)
    {
        PlayerTeamPrefabs.Clear();
        PlayerTeamPrefabs.AddRange(prefabs);
    }

    public static void SetEnemyTeam(params GameObject[] prefabs)
    {
        EnemyTeamPrefabs.Clear();
        EnemyTeamPrefabs.AddRange(prefabs);
    }

    // -------------------------------
    // 對外方法：切換關卡
    // -------------------------------
    public static void LoadStage(string sceneName, int stageIndex = -1)
    {
        if (stageIndex >= 0) CurrentStageIndex = stageIndex;
        NextSceneName = sceneName;
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }
}
