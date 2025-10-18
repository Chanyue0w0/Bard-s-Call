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

    void Start()
    {
        SetupTeam(CTeamInfo, playerPositions);
        SetupTeam(ETeamInfo, enemyPositions);

        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.LoadTeamData(this);
        }
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
}
