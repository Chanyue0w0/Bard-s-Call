using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonsterInstManager : MonoBehaviour
{
    public static MonsterInstManager Instance { get; private set; }

    [Header("怪物 Prefabs")]
    //public GameObject slimePrefab;
    public GameObject axeGoblinPrefab;
    public GameObject shieldGoblinPrefab;
    public GameObject mageGoblinPrefab;
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

        // 定義可用怪物池
        List<GameObject> monsterPool = new List<GameObject>()
        {
            //slimePrefab,
            shieldGoblinPrefab,
            axeGoblinPrefab,
            mageGoblinPrefab
        };

        if (currentLevel < 4)
        {
            // 第 1~3 關，從普通怪物中隨機生成 1~3 隻
            int enemyCount = Random.Range(1, 4); // 1~3 隻
            List<int> availableSlots = new List<int>() { 0, 1, 2 };

            for (int i = 0; i < enemyCount; i++)
            {
                if (availableSlots.Count == 0) break;

                int slotIndex = availableSlots[Random.Range(0, availableSlots.Count)];
                availableSlots.Remove(slotIndex);

                GameObject prefab = monsterPool[Random.Range(0, monsterPool.Count)];
                teamManager.EnemyTeamInfo[slotIndex].PrefabToSpawn = prefab;
            }
        }
        else
        {
            // 第 4 關，Boss 與普通怪物機率相同
            List<GameObject> fullPool = new List<GameObject>(monsterPool);
            fullPool.Add(darkKnightPrefab);

            int enemyCount = Random.Range(1, 4);
            List<int> availableSlots = new List<int>() { 0, 1, 2 };

            for (int i = 0; i < enemyCount; i++)
            {
                if (availableSlots.Count == 0) break;

                int slotIndex = availableSlots[Random.Range(0, availableSlots.Count)];
                availableSlots.Remove(slotIndex);

                GameObject prefab = fullPool[Random.Range(0, fullPool.Count)];
                teamManager.EnemyTeamInfo[slotIndex].PrefabToSpawn = prefab;
            }
        }

        // 生成敵人
        teamManager.SetupEnemyTeam();
    }
}
