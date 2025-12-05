using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MonsterInstManager : MonoBehaviour
{
    public static MonsterInstManager Instance { get; private set; }

    [Header("怪物 Prefabs")]
    public GameObject axeGoblinPrefab;
    public GameObject shieldGoblinPrefab;
    public GameObject mageGoblinPrefab;
    public GameObject poisonFrogPrefab;
    public GameObject orcPrefab;
    //public GameObject darkKnightPrefab;

    [Header("關聯組件")]
    public BattleTeamManager teamManager;

    [Header("生成設定")]
    public float checkInterval = 1.0f;
    public float spawnDelay = 1.5f;
    private bool isStageCleared = false;

    [Header("UI 元件 (結算面板)")]
    public GameObject stageCompletePanel; // Inspector 指派，打完最後一關顯示
    // ★ 新增：Summary 結算顯示 Text
    public Text summaryTimeText;
    public Text summaryComboText;

    // ★ 新增：顯示當前關卡進度的 Text
    public Text stageProgressText;


    [Header("戰鬥計時器")] 
    private float battleTimer = 0f;
    private bool battleEnded = false;


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
        if (teamManager == null)
            teamManager = FindObjectOfType<BattleTeamManager>();

        StartCoroutine(CheckEnemyClearLoop());
    }
    private void Update()
    {
        // 若戰鬥尚未結束，累積時間
        if (!battleEnded)
            battleTimer += Time.deltaTime;
    }
    private IEnumerator CheckEnemyClearLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(checkInterval);

            if (IsAllEnemyCleared() && !isStageCleared)
            {
                isStageCleared = true;

                yield return new WaitForSeconds(spawnDelay);

                // 進入下一關卡流程
                SpawnNextStage();

                isStageCleared = false;
            }
        }
    }

    private bool IsAllEnemyCleared()
    {
        foreach (var enemy in teamManager.EnemyTeamInfo)
        {
            if (enemy != null && enemy.Actor != null)
                return false;
        }
        return true;
    }

    // =========================================================
    // ★ 關鍵函式：生成下一波 Stage（不切換場景）
    // =========================================================
    private void SpawnNextStage()
    {
        // 增加 Stage
        GlobalIndex.CurrentStageIndex++;

        int chapter = GlobalIndex.CurrentChapterIndex;
        int level = GlobalIndex.CurrentLevelIndex;
        int stage = GlobalIndex.CurrentStageIndex;

        Debug.Log($"[MonsterInstManager] 章節 {chapter} - 關卡 {level} - 當前 Stage {stage}");

        // 檢查 Stage 上限
        int maxStage = (level == 1) ? 5 : 6; // Level1打5關、Level2打6關

        // ★ 更新關卡顯示 UI
        if (stageProgressText != null)
            stageProgressText.text = $"Round: {stage}/{maxStage}";

        if (stage > maxStage)
        {
            Debug.Log("[MonsterInstManager] 關卡已通關，顯示結算面板。");

            // ★ 紀錄總戰鬥時間
            GlobalIndex.TotalBattleTime = battleTimer;
            battleEnded = true;

            // ★ 更新結算文字
            if (summaryTimeText != null)
                summaryTimeText.text = $"通關時間 {GlobalIndex.TotalBattleTime:F1} 秒";

            if (summaryComboText != null)
                summaryComboText.text = $"最高連擊數 {GlobalIndex.MaxCombo} Combo";

            // 顯示結算面板
            if (stageCompletePanel != null)
                stageCompletePanel.SetActive(true);
            else
                Debug.LogWarning("未指派 StageCompletePanel！");

            return;
        }




        // 清空上一波敵人資訊
        for (int i = 0; i < teamManager.EnemyTeamInfo.Length; i++)
        {
            teamManager.EnemyTeamInfo[i] = new BattleManager.TeamSlotInfo();
        }

        // 根據關卡內容生成
        SpawnByLevelAndStage(level, stage);

        // 呼叫原有敵隊生成邏輯
        teamManager.SetupEnemyTeam();
    }

    // =========================================================
    // ★ 新增：依據 Level / Stage 決定生成內容
    // =========================================================
    private void SpawnByLevelAndStage(int level, int stage)
    {
        List<GameObject> monsterPool = new List<GameObject>()
        {
            shieldGoblinPrefab,
            axeGoblinPrefab,
            mageGoblinPrefab,
            //poisonFrogPrefab,
            //orcPrefab
        };

        // 從第一個位置開始依序填入
        List<int> slots = new List<int>() { 0, 1, 2 };

        if (level == 1)
        {
            switch (stage)
            {
                case 1:
                    //teamManager.EnemyTeamInfo[0].PrefabToSpawn = shieldGoblinPrefab;
                    //teamManager.EnemyTeamInfo[1].PrefabToSpawn = axeGoblinPrefab;
                    //teamManager.EnemyTeamInfo[2].PrefabToSpawn = mageGoblinPrefab;
                    //teamManager.EnemyTeamInfo[0].PrefabToSpawn = axeGoblinPrefab;
                    //teamManager.EnemyTeamInfo[1].PrefabToSpawn = shieldGoblinPrefab;
                    //teamManager.EnemyTeamInfo[2].PrefabToSpawn = poisonFrogPrefab;
                    teamManager.EnemyTeamInfo[0].PrefabToSpawn = orcPrefab;
                    break;
                case 2:
                    //teamManager.EnemyTeamInfo[2].PrefabToSpawn = poisonFrogPrefab;
                    //teamManager.EnemyTeamInfo[0].PrefabToSpawn = orcPrefab;
                    teamManager.EnemyTeamInfo[2].PrefabToSpawn = mageGoblinPrefab;
                    //teamManager.EnemyTeamInfo[0].PrefabToSpawn = shieldGoblinPrefab;
                    break;
                case 3:
                    teamManager.EnemyTeamInfo[0].PrefabToSpawn = shieldGoblinPrefab;
                    teamManager.EnemyTeamInfo[1].PrefabToSpawn = axeGoblinPrefab;
                    teamManager.EnemyTeamInfo[2].PrefabToSpawn = mageGoblinPrefab;
                    break;
                case 4:
                    teamManager.EnemyTeamInfo[2].PrefabToSpawn = poisonFrogPrefab;
                    break;
                case 5:
                    teamManager.EnemyTeamInfo[0].PrefabToSpawn = orcPrefab;
                    break;
            }
        }
        else if (level == 2)
        {
            if (stage < 6)
            {
                int enemyCount = Random.Range(1, 4); // 1~3
                for (int i = 0; i < enemyCount && slots.Count > 0; i++)
                {
                    int slotIndex = slots[0];
                    slots.RemoveAt(0);
                    var prefab = monsterPool[Random.Range(0, monsterPool.Count)];
                    teamManager.EnemyTeamInfo[slotIndex].PrefabToSpawn = prefab;
                }
            }
            else if (stage == 6)
            {
                teamManager.EnemyTeamInfo[0].PrefabToSpawn = axeGoblinPrefab;
                teamManager.EnemyTeamInfo[1].PrefabToSpawn = mageGoblinPrefab;
                teamManager.EnemyTeamInfo[2].PrefabToSpawn = poisonFrogPrefab;
            }
        }
    }
}
