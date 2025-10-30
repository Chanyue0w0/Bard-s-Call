using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonsterInstManager : MonoBehaviour
{
    public static MonsterInstManager Instance { get; private set; }

    [Header("怪物 Prefabs")]
    public GameObject slimePrefab;
    public GameObject shieldGoblinPrefab;
    public GameObject darkKnightPrefab;

    [Header("關聯組件")]
    public BattleTeamManager teamManager;

    [Header("生成設定")]
    public float checkInterval = 1.0f; // 每秒檢查是否清場
    public float spawnDelay = 1.5f;    // 清場後延遲生成下一波
    private int currentLevel = 1;

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

    private IEnumerator CheckEnemyClearLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(checkInterval);

            if (IsAllEnemyCleared())
            {
                yield return new WaitForSeconds(spawnDelay);
                SpawnNextWave();
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

    private void SpawnNextWave()
    {
        // Level 循環
        currentLevel++;
        if (currentLevel > 4)
            currentLevel = 1;

        Debug.Log("=== Spawn Next Wave: Level " + currentLevel + " ===");

        // 清空舊敵人資料
        for (int i = 0; i < teamManager.EnemyTeamInfo.Length; i++)
        {
            teamManager.EnemyTeamInfo[i] = new BattleManager.TeamSlotInfo();
        }

        // 根據 Level 生成敵人
        if (currentLevel < 4)
        {
            int enemyCount = Random.Range(1, 4); // 1~3 隻
            List<int> availableSlots = new List<int>() { 0, 1, 2 };

            for (int i = 0; i < enemyCount; i++)
            {
                if (availableSlots.Count == 0) break;

                int slotIndex = availableSlots[Random.Range(0, availableSlots.Count)];
                availableSlots.Remove(slotIndex);

                GameObject prefab = (Random.value > 0.5f) ? slimePrefab : shieldGoblinPrefab;
                teamManager.EnemyTeamInfo[slotIndex].PrefabToSpawn = prefab;
            }
        }
        else
        {
            // Level 4 生成 DarkLongSwordKnight
            int bossSlot = Random.Range(0, 3);
            teamManager.EnemyTeamInfo[bossSlot].PrefabToSpawn = darkKnightPrefab;
        }

        // 使用原有流程生成
        teamManager.SetupEnemyTeam();
    }
}
