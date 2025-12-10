using System.Collections.Generic;
using UnityEngine;

public class GlobalIndex : MonoBehaviour
{
    public static GlobalIndex Instance { get; private set; }

    // -------------------------------
    // 全域遊戲變數
    // -------------------------------

    // 關卡資訊（★修改：改為三層結構）
    public static int CurrentChapterIndex = 1; // 章節
    public static int CurrentLevelIndex = 0;   // 關卡
    public static int CurrentStageIndex = 0;   // 小關卡波數
    public static string CurrentStageName = "Forest01";
    public static string NextSceneName = "FightScene";

    // -------------------------------
    // 玩家統計資料（★新增）
    // -------------------------------

    public static int MaxTotalHP = 200; // 開場總血量
    public static int CurrentTotalHP = 200; // 當前總血量

    public static int RythmResonanceBuff = 0; // 對拍共鳴臨時加乘

    public static float TotalBattleTime = 0f; // 本場戰鬥累積秒數
    public static int MaxCombo = 0;           // 玩家最高連擊數

    public static bool GameOver = false;           // 遊戲是否結束
    public static bool isTutorial = true;           // 遊戲是否正在教學
    public static bool isTutorialPanelOpened = true;           // 遊戲是否正在教學圖片畫面

    // 隊伍資訊
    public static List<GameObject> PlayerTeamPrefabs = new List<GameObject>();
    public static List<GameObject> EnemyTeamPrefabs = new List<GameObject>();

    // 系統設定
    public static int DifficultyLevel = 1; // 1=Normal, 2=Hard, 3=Extreme
    public static bool IsTutorialCleared = false;

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

    //public static void LoadStage(string sceneName, int stageIndex = -1)
    //{
    //    if (stageIndex >= 0) CurrentStageIndex = stageIndex;
    //    NextSceneName = sceneName;
    //    UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    //}
}
