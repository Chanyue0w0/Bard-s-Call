using System.Collections.Generic;
using UnityEngine;

public class GlobalIndex : MonoBehaviour
{
    public static GlobalIndex Instance { get; private set; }

    // -------------------------------
    // ����C���ܼ�
    // -------------------------------

    // ���d��T
    public static int CurrentStageIndex = 0;
    public static string CurrentStageName = "Forest01";
    public static string NextSceneName = "BattleScene";

    // �����T
    public static List<GameObject> PlayerTeamPrefabs = new List<GameObject>(); // �ڤ訤�� Prefabs
    public static List<GameObject> EnemyTeamPrefabs = new List<GameObject>();  // �Ĥ訤�� Prefabs

    // �t�γ]�w
    public static int DifficultyLevel = 1; // 1=Normal, 2=Hard, 3=Extreme
    public static bool IsTutorialCleared = false;

    // -------------------------------
    // ��Ҫ�l��
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
    // ��~��k�G�]�w������
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
    // ��~��k�G�������d
    // -------------------------------
    public static void LoadStage(string sceneName, int stageIndex = -1)
    {
        if (stageIndex >= 0) CurrentStageIndex = stageIndex;
        NextSceneName = sceneName;
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }
}
