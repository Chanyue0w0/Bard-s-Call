using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonsterInstManager : MonoBehaviour
{
    public static MonsterInstManager Instance { get; private set; }

    [Header("�Ǫ� Prefabs")]
    public GameObject slimePrefab;
    public GameObject shieldGoblinPrefab;
    public GameObject darkKnightPrefab;

    [Header("���p�ե�")]
    public BattleTeamManager teamManager;

    [Header("�ͦ��]�w")]
    public float checkInterval = 1.0f; // �C���ˬd�O�_�M��
    public float spawnDelay = 1.5f;    // �M���᩵��ͦ��U�@�i
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
        // Level �`��
        currentLevel++;
        if (currentLevel > 4)
            currentLevel = 1;

        Debug.Log("=== Spawn Next Wave: Level " + currentLevel + " ===");

        // �M���¼ĤH���
        for (int i = 0; i < teamManager.EnemyTeamInfo.Length; i++)
        {
            teamManager.EnemyTeamInfo[i] = new BattleManager.TeamSlotInfo();
        }

        // �ھ� Level �ͦ��ĤH
        if (currentLevel < 4)
        {
            int enemyCount = Random.Range(1, 4); // 1~3 ��
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
            // Level 4 �ͦ� DarkLongSwordKnight
            int bossSlot = Random.Range(0, 3);
            teamManager.EnemyTeamInfo[bossSlot].PrefabToSpawn = darkKnightPrefab;
        }

        // �ϥέ즳�y�{�ͦ�
        teamManager.SetupEnemyTeam();
    }
}
