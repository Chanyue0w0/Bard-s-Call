using System.Collections;
using UnityEngine;

public class BattleTeamManager : MonoBehaviour
{
    [Header("我方固定座標（右側）")]
    [SerializeField] private Transform[] playerPositions = new Transform[3];

    [Header("敵方固定座標（左側）")]
    [SerializeField] private Transform[] enemyPositions = new Transform[3];

    [Header("我方三格資料")]
    public BattleManager.TeamSlotInfo[] CTeamInfo = new BattleManager.TeamSlotInfo[3];

    [Header("敵方三格資料")]
    public BattleManager.TeamSlotInfo[] EnemyTeamInfo = new BattleManager.TeamSlotInfo[3]; // ★ 改名統一

    [Header("血條 UI")]
    public GameObject healthBarPrefab;
    public Canvas uiCanvas;

    [Header("重攻擊 UI")]
    public GameObject heavyAttackBarPrefab;

    private void Start()
    {
        // 先從 GlobalIndex 載入隊伍資料
        LoadTeamsFromGlobalIndex();

        // 原有流程
        SetupTeam(CTeamInfo, playerPositions);
        SetupTeam(EnemyTeamInfo, enemyPositions);

        if (BattleManager.Instance != null)
            BattleManager.Instance.LoadTeamData(this);

        CreateHeavyAttackBars(CTeamInfo);
    }

    private void LoadTeamsFromGlobalIndex()
    {
        // 玩家隊伍
        for (int i = 0; i < CTeamInfo.Length; i++)
        {
            if (i < GlobalIndex.PlayerTeamPrefabs.Count && GlobalIndex.PlayerTeamPrefabs[i] != null)
            {
                CTeamInfo[i] = new BattleManager.TeamSlotInfo();
                CTeamInfo[i].PrefabToSpawn = GlobalIndex.PlayerTeamPrefabs[i];
            }
        }

        // 敵方隊伍
        for (int i = 0; i < EnemyTeamInfo.Length; i++)
        {
            if (i < GlobalIndex.EnemyTeamPrefabs.Count && GlobalIndex.EnemyTeamPrefabs[i] != null)
            {
                EnemyTeamInfo[i] = new BattleManager.TeamSlotInfo();
                EnemyTeamInfo[i].PrefabToSpawn = GlobalIndex.EnemyTeamPrefabs[i];
            }
        }
    }


    private void SetupTeam(BattleManager.TeamSlotInfo[] team, Transform[] positions)
    {
        for (int i = 0; i < team.Length && i < positions.Length; i++)
        {
            if (team[i] == null)
                team[i] = new BattleManager.TeamSlotInfo();

            var info = team[i];
            if (info.PrefabToSpawn == null)
                continue;

            if (info.Actor == null)
            {
                info.Actor = Instantiate(info.PrefabToSpawn, positions[i].position, Quaternion.identity);
            }

            if (info.Actor == null)
                continue;

            info.SlotTransform = positions[i];

            // 敵人自動索引
            var enemyBase = info.Actor.GetComponent<EnemyBase>();
            if (enemyBase != null)
            {
                enemyBase.ETeam = BattleManager.ETeam.Enemy; // ★ 改用新 enum
                enemyBase.StartCoroutine("DelayAssignSlot");
            }

            var data = info.Actor.GetComponent<CharacterData>();
            if (data != null)
            {
                info.UnitName = data.CharacterName;
                info.ClassType = data.ClassType;
                info.MaxHP = data.MaxHP;
                info.HP = data.HP;
                info.MaxMP = data.MaxMP;
                info.MP = data.MP;
                info.OriginAtk = data.OriginAtk;
                info.Atk = data.Atk;
            }

            CreateHealthBar(info);
        }
    }

    private void CreateHealthBar(BattleManager.TeamSlotInfo slot)
    {
        if (slot.Actor == null || healthBarPrefab == null || uiCanvas == null)
            return;

        Transform headPoint = slot.Actor.transform.Find("HeadPoint");
        if (headPoint != null)
        {
            GameObject hb = Instantiate(healthBarPrefab, uiCanvas.transform);
            var hbUI = hb.GetComponent<HealthBarUI>();
            if (hbUI != null)
                hbUI.Init(slot, headPoint, uiCanvas.worldCamera);
        }
    }

    private void CreateHeavyAttackBars(BattleManager.TeamSlotInfo[] team)
    {
        if (heavyAttackBarPrefab == null || uiCanvas == null)
            return;

        foreach (var slot in team)
        {
            if (slot?.Actor == null) continue;

            var combo = slot.Actor.GetComponent<CharacterComboState>();
            if (combo == null) combo = slot.Actor.AddComponent<CharacterComboState>();

            Transform headPoint = slot.Actor.transform.Find("HeadPoint");
            if (headPoint == null)
                headPoint = slot.Actor.transform;

            var barObj = Instantiate(heavyAttackBarPrefab, uiCanvas.transform);
            var bar = barObj.GetComponent<HeavyAttackBarUI>();
            if (bar != null)
                bar.Init(combo, headPoint, Camera.main);
        }
    }
}
