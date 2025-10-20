using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem; // 新 Input System
using System.Linq;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    [System.Serializable]
    public enum UnitClass
    {
        Warrior,
        Mage,
        Shield,
        Priest,
        Ranger,
        Enemy
    }

    [System.Serializable]
    public class TeamSlotInfo
    {

        [Header("Prefab 設定")]
        public GameObject PrefabToSpawn; // 若未指定 Actor，將自動生成此 Prefab

        [Header("場上關聯")]
        public string UnitName;
        public GameObject Actor;
        public Transform SlotTransform;
        public UnitClass ClassType = UnitClass.Warrior;

        [Header("戰鬥數值")]
        public int MaxHP = 100;
        public int HP = 100;
        public int MaxMP = 100;
        public int MP = 0;
        public int OriginAtk = 10;
        public int Atk = 10;

        [Header("技能")]
        public string[] SkillNames;
        public GameObject[] SkillPrefabs;

        [Header("普通攻擊")]
        public string[] NormalAttackNames;
        public GameObject[] NormalAttackPrefabs;
        
        [Header("輸入綁定")]
        public int AssignedKeyIndex;

    }

    [Header("我方固定座標（自動在 Start 記錄）")]
    [SerializeField] private Transform[] playerPositions = new Transform[3];

    [Header("敵方固定座標（自動在 Start 記錄）")]
    [SerializeField] private Transform[] enemyPositions = new Transform[3];

    [Header("我方三格資料（右側）")]
    public TeamSlotInfo[] CTeamInfo = new TeamSlotInfo[3];

    [Header("敵方三格資料（左側）")]
    public TeamSlotInfo[] ETeamInfo = new TeamSlotInfo[3];

    [Header("輸入（用 InputActionReference 綁定）")]
    public InputActionReference actionAttackP1;
    public InputActionReference actionAttackP2;
    public InputActionReference actionAttackP3;
    public InputActionReference actionRotateLeft;
    public InputActionReference actionRotateRight;

    [Header("時序與運動參數")]
    public float actionLockDuration = 0.5f;
    public float dashDuration = 0.05f;
    public Vector3 meleeContactOffset = new Vector3(-1f, 0f, 0f);

    [Header("特效 Prefab")]
    public GameObject meleeVfxPrefab;
    public GameObject rangedVfxPrefab;
    public GameObject shieldStrikeVfxPrefab;
    public GameObject missVfxPrefab;
    public GameObject magicUseAuraPrefab;
    public float vfxLifetime = 1.5f;

    [Header("Shield 設定")]
    public float shieldBlockDuration = 2.0f;
    public int shieldDamage = 10;

    private bool _isActionLocked;

    [Header("血條 UI")]
    public GameObject healthBarPrefab;
    public Canvas uiCanvas;

    [Header("旋轉移動設定")]
    public float rotateMoveDuration = 0.2f;

    [Header("近戰攻擊設定")]
    public float dashStayDuration = 0.15f;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnEnable()
    {
        if (actionAttackP1 != null)
        {
            actionAttackP1.action.started += ctx => OnAttackKey(0);
            actionAttackP1.action.Enable();
        }
        if (actionAttackP2 != null)
        {
            actionAttackP2.action.started += ctx => OnAttackKey(1);
            actionAttackP2.action.Enable();
        }
        if (actionAttackP3 != null)
        {
            actionAttackP3.action.started += ctx => OnAttackKey(2);
            actionAttackP3.action.Enable();
        }
        // 其他旋轉輸入照舊
    }

    void OnDisable()
    {
        if (actionAttackP1 != null) actionAttackP1.action.started -= ctx => OnAttackKey(0);
        if (actionAttackP2 != null) actionAttackP2.action.started -= ctx => OnAttackKey(1);
        if (actionAttackP3 != null) actionAttackP3.action.started -= ctx => OnAttackKey(2);
    }


    void Start()
    {
        //CreateHealthBars(CTeamInfo);
        //CreateHealthBars(ETeamInfo);
    }

    public void LoadTeamData(BattleTeamManager teamMgr)
    {
        if (teamMgr == null) return;

        // 複製隊伍資料
        this.CTeamInfo = teamMgr.CTeamInfo;
        this.ETeamInfo = teamMgr.ETeamInfo;

        Debug.Log("載入隊伍成功，玩家角色：" +
        string.Join(", ", CTeamInfo.Where(x => x != null).Select(x => x.UnitName)));
    }

    // ================= 攻擊邏輯 =================
    private void OnAttackKey(int index)
    {
        if (_isActionLocked) return;
        if (index < 0 || index >= CTeamInfo.Length) return;
        if (CTeamInfo[index] == null) return;

        bool perfect = BeatJudge.Instance.IsOnBeat();
        if (!perfect)
        {
            Debug.Log("Miss！未在節拍上，不觸發攻擊。");
            return;
        }

        var attacker = CTeamInfo[index];
        var target = FindEnemyByClass(attacker.ClassType);
        if (target == null) return;

        StartCoroutine(LockAction(actionLockDuration));

        // ★ 根據拍數判斷攻擊種類
        //int beatInCycle = BeatManager.Instance.currentBeatInCycle;
        int beatInCycle = BeatManager.Instance.predictedNextBeat;


        if (attacker.ClassType == UnitClass.Warrior)
        {
            // 呼叫新函式處理多段攻擊
            StartCoroutine(HandleWarriorAttack(attacker, target, beatInCycle, perfect));
        }
        else
        {
            // 其他職業（尚未擴充）
            StartCoroutine(AttackSequence(attacker, target, target.SlotTransform.position, perfect));
        }
    }

    // ============================================================
    // Warrior 3 段普攻 + 第4拍重攻
    // ============================================================
    // ============================================================
    // Warrior 3 段普攻 + 第4拍重攻
    // ============================================================
    // ============================================================
    // Warrior 3 段普攻 + 第4拍重攻（自動記憶段數）
    // ============================================================
    private IEnumerator HandleWarriorAttack(TeamSlotInfo attacker, TeamSlotInfo target, int beatInCycle, bool perfect)
    {
        var actor = attacker.Actor.transform;
        Vector3 origin = actor.position;
        Vector3 targetPoint = target.SlotTransform.position + meleeContactOffset;

        var charData = attacker.Actor.GetComponent<CharacterData>();
        if (charData == null)
        {
            Debug.LogWarning("角色沒有 CharacterData，使用預設攻擊。");
            yield return AttackSequence(attacker, target, targetPoint, perfect);
            yield break;
        }

        // 取得 combo 狀態
        var comboData = actor.GetComponent<CharacterComboState>();
        if (comboData == null)
            comboData = actor.gameObject.AddComponent<CharacterComboState>();

        // 若長時間沒攻擊則重置 combo
        if (Time.time - comboData.lastAttackTime > 2f)
            comboData.currentPhase = 1;

        SkillInfo chosenSkill = null;
        GameObject attackPrefab = null;

        // ★ 第四拍固定重攻，其餘三拍為普攻
        if (beatInCycle == 4)
        {
            chosenSkill = charData.HeavyAttack;
            attackPrefab = chosenSkill?.SkillPrefab;
            comboData.currentPhase = 1; // ★ 重製普攻段數
            Debug.Log($"Warrior 第四拍重攻擊：{chosenSkill?.SkillName ?? "未設定"}");
        }
        else
        {
            int phase = comboData.currentPhase;
            int attackIndex = Mathf.Clamp(phase - 1, 0, charData.NormalAttacks.Count - 1);
            chosenSkill = charData.NormalAttacks[attackIndex];
            attackPrefab = chosenSkill?.SkillPrefab;
            Debug.Log($"Warrior 普攻段 {phase}：{chosenSkill?.SkillName ?? "未設定"}");

            // 更新 combo 狀態（1→2→3 循環）
            comboData.currentPhase = (phase % 3) + 1;
        }

        // 預設特效（若找不到 prefab）
        if (attackPrefab == null && meleeVfxPrefab != null)
            attackPrefab = meleeVfxPrefab;

        yield return Dash(actor, origin, targetPoint, dashDuration);

        if (attackPrefab != null)
        {
            var skillObj = Instantiate(attackPrefab, targetPoint, Quaternion.identity);
            var sword = skillObj.GetComponent<SwordHitSkill>();
            if (sword != null)
            {
                sword.attacker = attacker;
                sword.target = target;
                sword.isPerfect = perfect;
            }
        }

        comboData.lastAttackTime = Time.time;

        yield return new WaitForSeconds(dashStayDuration);
        yield return Dash(actor, targetPoint, origin, dashDuration);
    }




    //private void OnAttackP1(InputAction.CallbackContext ctx)
    //{
    //    if (_isActionLocked) return;
    //    if (CTeamInfo[0] == null) return;

    //    bool perfect = BeatJudge.Instance.IsOnBeat();
    //    var attacker = CTeamInfo[0];
    //    var target = FindEnemyByClass(attacker.ClassType);
    //    if (target == null) return;

    //    StartCoroutine(LockAction(actionLockDuration));

    //    StartCoroutine(AttackSequence(attacker, target, target.SlotTransform.position, perfect));

    //    if (perfect)
    //    {
    //        for (int i = 1; i < CTeamInfo.Length; i++)
    //        {
    //            var ally = CTeamInfo[i];
    //            if (ally != null && ally.Actor != null)
    //            {
    //                StartCoroutine(AttackSequence(ally, target, target.SlotTransform.position, true));
    //            }
    //        }
    //    }
    //}

    // ================= 旋轉邏輯 =================
    private void OnRotateLeft(InputAction.CallbackContext ctx)
    {
        if (_isActionLocked) return;
        StartCoroutine(LockAction(actionLockDuration));
        RotateTeamCounterClockwise();
    }

    private void OnRotateRight(InputAction.CallbackContext ctx)
    {
        if (_isActionLocked) return;
        StartCoroutine(LockAction(actionLockDuration));
        RotateTeamClockwise();
    }

    private IEnumerator LockAction(float duration)
    {
        _isActionLocked = true;
        yield return new WaitForSeconds(duration);
        _isActionLocked = false;
    }

    private void RotateTeamClockwise()
    {
        var temp = CTeamInfo[2];
        CTeamInfo[2] = CTeamInfo[1];
        CTeamInfo[1] = CTeamInfo[0];
        CTeamInfo[0] = temp;
        UpdatePositions();
    }

    private void RotateTeamCounterClockwise()
    {
        var temp = CTeamInfo[0];
        CTeamInfo[0] = CTeamInfo[1];
        CTeamInfo[1] = CTeamInfo[2];
        CTeamInfo[2] = temp;
        UpdatePositions();
    }

    private void UpdatePositions()
    {
        for (int i = 0; i < CTeamInfo.Length; i++)
        {
            if (CTeamInfo[i] != null && CTeamInfo[i].Actor != null)
            {
                StartCoroutine(MoveToPosition(CTeamInfo[i].Actor.transform, playerPositions[i].position, rotateMoveDuration));
                CTeamInfo[i].SlotTransform = playerPositions[i];
            }
        }
    }

    private IEnumerator MoveToPosition(Transform actor, Vector3 targetPos, float duration)
    {
        if (duration <= 0f)
        {
            actor.position = targetPos;
            yield break;
        }

        Vector3 start = actor.position;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            actor.position = Vector3.Lerp(start, targetPos, t);
            yield return null;
        }
    }

    // ================= 攻擊序列 =================
    // ================= 攻擊序列 =================
    private IEnumerator AttackSequence(TeamSlotInfo attacker, TeamSlotInfo target, Vector3 targetPoint, bool perfect)
    {
        var actor = attacker.Actor.transform;
        Vector3 origin = actor.position;

        switch (attacker.ClassType)
        {
            case UnitClass.Warrior:
                {
                    Vector3 contactPoint = targetPoint + meleeContactOffset;
                    yield return Dash(actor, origin, contactPoint, dashDuration);

                    var skill = Instantiate(meleeVfxPrefab, targetPoint, Quaternion.identity);
                    var sword = skill.GetComponent<SwordHitSkill>();
                    if (sword != null)
                    {
                        sword.attacker = attacker;
                        sword.target = target;
                        sword.isPerfect = perfect;
                    }

                    yield return new WaitForSeconds(dashStayDuration);
                    yield return Dash(actor, contactPoint, origin, dashDuration);
                    break;
                }

            case UnitClass.Mage:
                {
                    if (magicUseAuraPrefab != null)
                    {
                        var aura = Instantiate(magicUseAuraPrefab, actor.position, Quaternion.identity);
                        if (vfxLifetime > 0f) Destroy(aura, vfxLifetime);
                    }

                    // 全體攻擊：每個敵人都生成 FireBall
                    foreach (var enemy in ETeamInfo)
                    {
                        if (enemy != null && enemy.Actor != null)
                        {
                            var fireball = Instantiate(rangedVfxPrefab, actor.position, Quaternion.identity)
                                .GetComponent<FireBallSkill>();
                            if (fireball != null)
                            {
                                fireball.attacker = attacker;
                                fireball.target = enemy;
                                fireball.isPerfect = perfect;
                            }
                        }
                    }
                    break;
                }

            case UnitClass.Shield:
                {
                    if (perfect)
                    {
                        BattleEffectManager.Instance.ActivateShield(shieldBlockDuration);
                        var strikeObj = Instantiate(shieldStrikeVfxPrefab, actor.position, Quaternion.identity);
                        var strike = strikeObj.GetComponent<ShieldStrike>();
                        if (strike != null)
                        {
                            strike.attacker = attacker;
                            strike.target = target;
                            strike.overrideDamage = shieldDamage;
                        }
                    }
                    else if (missVfxPrefab != null)
                    {
                        Instantiate(missVfxPrefab, actor.position, Quaternion.identity);
                    }
                    break;
                }

            case UnitClass.Priest:
                {
                    if (perfect)
                    {
                        BattleEffectManager.Instance.HealTeam(10);
                        for (int i = 0; i < playerPositions.Length; i++)
                        {
                            var pos = playerPositions[i].position;
                            if (BattleEffectManager.Instance.healVfxPrefab != null)
                            {
                                var heal = Instantiate(BattleEffectManager.Instance.healVfxPrefab, pos, Quaternion.identity);
                                if (vfxLifetime > 0f) Destroy(heal, vfxLifetime);
                            }
                        }
                    }
                    else if (missVfxPrefab != null)
                    {
                        Instantiate(missVfxPrefab, actor.position, Quaternion.identity);
                    }
                    break;
                }

            case UnitClass.Ranger:
                {
                    // 尋找最後一位有效敵人（若空位則往前找）
                    TeamSlotInfo lastEnemy = FindLastValidEnemy();
                    if (lastEnemy != null)
                    {
                        var arrow = Instantiate(rangedVfxPrefab, actor.position, Quaternion.identity)
                            .GetComponent<FireBallSkill>();
                        if (arrow != null)
                        {
                            arrow.attacker = attacker;
                            arrow.target = lastEnemy;
                            arrow.isPerfect = perfect;
                        }
                    }
                    else if (missVfxPrefab != null)
                    {
                        Instantiate(missVfxPrefab, actor.position, Quaternion.identity);
                    }
                    break;
                }
        }
    }

    // ================= 攻擊目標搜尋 =================
    private TeamSlotInfo FindEnemyByClass(UnitClass cls)
    {
        // Warrior：攻擊前方第一位敵人
        if (cls == UnitClass.Warrior)
        {
            for (int i = 0; i < ETeamInfo.Length; i++)
                if (ETeamInfo[i] != null && ETeamInfo[i].Actor != null)
                    return ETeamInfo[i];
        }

        // Mage：不需要特定目標，全體攻擊時會用 ETeamInfo 迭代
        if (cls == UnitClass.Mage)
        {
            return ETeamInfo.FirstOrDefault(e => e != null && e.Actor != null);
        }

        // Ranger：攻擊最後一位有效敵人
        if (cls == UnitClass.Ranger)
        {
            return FindLastValidEnemy();
        }

        return FindNextValidEnemy(0);
    }

    // 尋找最後一位仍存在的敵人（由後往前找）
    private TeamSlotInfo FindLastValidEnemy()
    {
        for (int i = ETeamInfo.Length - 1; i >= 0; i--)
        {
            if (ETeamInfo[i] != null && ETeamInfo[i].Actor != null)
                return ETeamInfo[i];
        }
        return null;
    }


    private TeamSlotInfo FindNextValidEnemy(int startIndex)
    {
        for (int i = startIndex; i < ETeamInfo.Length; i++)
        {
            if (ETeamInfo[i] != null && ETeamInfo[i].Actor != null)
                return ETeamInfo[i];
        }
        return null;
    }

    private IEnumerator Dash(Transform actor, Vector3 from, Vector3 to, float duration)
    {
        if (duration <= 0f)
        {
            actor.position = to;
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            actor.position = Vector3.Lerp(from, to, t);
            yield return null;
        }
    }
}
