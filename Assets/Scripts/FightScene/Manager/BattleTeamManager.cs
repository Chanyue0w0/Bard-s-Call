using System.Collections;
using UnityEngine;

public class BattleTeamManager : MonoBehaviour
{
    [Header("�ڤ�T�w�y�С]�k���^")]
    [SerializeField] private Transform[] playerPositions = new Transform[3];

    [Header("�Ĥ�T�w�y�С]�����^")]
    [SerializeField] private Transform[] enemyPositions = new Transform[3];

    [Header("�ڤ�T����")]
    public BattleManager.TeamSlotInfo[] CTeamInfo = new BattleManager.TeamSlotInfo[3];

    [Header("�Ĥ�T����")]
    public BattleManager.TeamSlotInfo[] ETeamInfo = new BattleManager.TeamSlotInfo[3];

    [Header("��� UI")]
    public GameObject healthBarPrefab;
    public Canvas uiCanvas;

    [Header("������ UI")]
    public GameObject heavyAttackBarPrefab; // ���V HeavyAttackBarUI �� Prefab

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

            // �� �۰ʥͦ�����]�Y�|�����w Actor�^
            if (info.Actor == null && info.PrefabToSpawn != null)
            {
                info.Actor = Instantiate(info.PrefabToSpawn, positions[i].position, Quaternion.identity);

                // �� �ߨ����ĤH�۰ʯ��ްt��]�T�O slotIndex ���T�^
                var enemyBase = info.Actor.GetComponent<EnemyBase>();
                if (enemyBase != null)
                {
                    // �j��I�s�۰ʯ��ޡ]�O�I�_���A�קK Awake �ɧǶ]�Ӧ��^
                    var method = typeof(EnemyBase).GetMethod("AutoAssignSlotIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    method?.Invoke(enemyBase, null);
                }
            }


            if (info.Actor == null)
                continue;

            // �]�w��m
            info.Actor.transform.position = positions[i].position;
            info.SlotTransform = positions[i];

            // Ū��������
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

                // ���q�����]�i�h�ӡ^
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

                // �ޯ�]�i�h�ӡ^
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
            Debug.LogWarning("BattleTeamManager: heavyAttackBarPrefab �� uiCanvas �����w�C");
            return;
        }

        foreach (var slot in team)
        {
            if (slot == null || slot.Actor == null) continue;

            // ���o/�ɤW combo ���A
            var combo = slot.Actor.GetComponent<CharacterComboState>();
            if (combo == null) combo = slot.Actor.AddComponent<CharacterComboState>();

            // ���Y���w���I
            Transform headPoint = slot.Actor.transform.Find("HeadPoint");
            if (headPoint == null)
            {
                headPoint = slot.Actor.transform;
                Debug.LogWarning($"{slot.UnitName} �ʤ� HeadPoint�A�N��Ψ��� Transform�C");
            }

            // �ͦ� UI �ê�l��
            var barObj = Instantiate(heavyAttackBarPrefab, uiCanvas.transform);
            var bar = barObj.GetComponent<HeavyAttackBarUI>();
            if (bar == null)
            {
                Debug.LogError("heavyAttackBarPrefab �W�S�� HeavyAttackBarUI �ե�C");
                Destroy(barObj);
                continue;
            }

            bar.Init(combo, headPoint, Camera.main);
        }
    }

}
