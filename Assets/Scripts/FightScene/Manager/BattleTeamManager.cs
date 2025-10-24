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
    public BattleManager.TeamSlotInfo[] ETeamInfo = new BattleManager.TeamSlotInfo[3];

    [Header("血條 UI")]
    public GameObject healthBarPrefab;
    public Canvas uiCanvas;

    [Header("重攻擊 UI")]
    public GameObject heavyAttackBarPrefab; // 指向 HeavyAttackBarUI 的 Prefab

    void Start()
    {
        SetupTeam(CTeamInfo, playerPositions);
        SetupTeam(ETeamInfo, enemyPositions);

        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.LoadTeamData(this);
        }

        CreateHeavyAttackBars(CTeamInfo);
        //CreateHeavyAttackBars(ETeamInfo);

    }

    private void SetupTeam(BattleManager.TeamSlotInfo[] team, Transform[] positions)
    {
        for (int i = 0; i < team.Length && i < positions.Length; i++)
        {
            var info = team[i];
            if (info == null)
                continue;

            // ★ 自動生成角色（若尚未指定 Actor）
            if (info.Actor == null && info.PrefabToSpawn != null)
            {
                info.Actor = Instantiate(info.PrefabToSpawn, positions[i].position, Quaternion.identity);

                // ★ 立刻讓敵人自動索引配對（確保 slotIndex 正確）
                var enemyBase = info.Actor.GetComponent<EnemyBase>();
                if (enemyBase != null)
                {
                    // 強制呼叫自動索引（保險起見，避免 Awake 時序跑太早）
                    var method = typeof(EnemyBase).GetMethod("AutoAssignSlotIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    method?.Invoke(enemyBase, null);
                }
            }


            if (info.Actor == null)
                continue;

            // 設定位置
            info.Actor.transform.position = positions[i].position;
            info.SlotTransform = positions[i];

            // 讀取角色資料
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

                // 普通攻擊（可多個）
                if (data.NormalAttacks != null && data.NormalAttacks.Count > 0)
                {
                    info.NormalAttackNames = new string[data.NormalAttacks.Count];
                    info.NormalAttackPrefabs = new GameObject[data.NormalAttacks.Count];
                    for (int j = 0; j < data.NormalAttacks.Count; j++)
                    {
                        info.NormalAttackNames[j] = data.NormalAttacks[j].SkillName;
                        info.NormalAttackPrefabs[j] = data.NormalAttacks[j].SkillPrefab;
                    }
                }

                // 技能（可多個）
                if (data.Skills != null && data.Skills.Count > 0)
                {
                    info.SkillNames = new string[data.Skills.Count];
                    info.SkillPrefabs = new GameObject[data.Skills.Count];
                    for (int j = 0; j < data.Skills.Count; j++)
                    {
                        info.SkillNames[j] = data.Skills[j].SkillName;
                        info.SkillPrefabs[j] = data.Skills[j].SkillPrefab;
                    }
                }
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
        {
            Debug.LogWarning("BattleTeamManager: heavyAttackBarPrefab 或 uiCanvas 未指定。");
            return;
        }

        foreach (var slot in team)
        {
            if (slot == null || slot.Actor == null) continue;

            // 取得/補上 combo 狀態
            var combo = slot.Actor.GetComponent<CharacterComboState>();
            if (combo == null) combo = slot.Actor.AddComponent<CharacterComboState>();

            // 找頭頂定位點
            Transform headPoint = slot.Actor.transform.Find("HeadPoint");
            if (headPoint == null)
            {
                headPoint = slot.Actor.transform;
                Debug.LogWarning($"{slot.UnitName} 缺少 HeadPoint，將改用角色 Transform。");
            }

            // 生成 UI 並初始化
            var barObj = Instantiate(heavyAttackBarPrefab, uiCanvas.transform);
            var bar = barObj.GetComponent<HeavyAttackBarUI>();
            if (bar == null)
            {
                Debug.LogError("heavyAttackBarPrefab 上沒有 HeavyAttackBarUI 組件。");
                Destroy(barObj);
                continue;
            }

            bar.Init(combo, headPoint, Camera.main);
        }
    }

}
