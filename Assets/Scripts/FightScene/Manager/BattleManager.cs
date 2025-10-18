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
            actionAttackP1.action.started += OnAttackP1;
            actionAttackP1.action.Enable();
        }
        if (actionRotateLeft != null)
        {
            actionRotateLeft.action.started += OnRotateLeft;
            actionRotateLeft.action.Enable();
        }
        if (actionRotateRight != null)
        {
            actionRotateRight.action.started += OnRotateRight;
            actionRotateRight.action.Enable();
        }
    }

    void OnDisable()
    {
        if (actionAttackP1 != null)
            actionAttackP1.action.started -= OnAttackP1;
        if (actionRotateLeft != null)
            actionRotateLeft.action.started -= OnRotateLeft;
        if (actionRotateRight != null)
            actionRotateRight.action.started -= OnRotateRight;
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


    //private void CreateHealthBars(TeamSlotInfo[] team)
    //{
    //    foreach (var slot in team)
    //    {
    //        if (slot != null && slot.Actor != null)
    //        {
    //            Transform headPoint = slot.Actor.transform.Find("HeadPoint");
    //            if (headPoint != null)
    //            {
    //                GameObject hb = Instantiate(healthBarPrefab, uiCanvas.transform);
    //                hb.GetComponent<HealthBarUI>().Init(slot, headPoint, uiCanvas.worldCamera);
    //            }
    //        }
    //    }
    //}

    // ================= 攻擊邏輯 =================
    private void OnAttackP1(InputAction.CallbackContext ctx)
    {
        if (_isActionLocked) return;
        if (CTeamInfo[0] == null) return;

        bool perfect = BeatJudge.Instance.IsOnBeat();
        var attacker = CTeamInfo[0];
        var target = FindEnemyByClass(attacker.ClassType);
        if (target == null) return;

        StartCoroutine(LockAction(actionLockDuration));

        StartCoroutine(AttackSequence(attacker, target, target.SlotTransform.position, perfect));

        if (perfect)
        {
            for (int i = 1; i < CTeamInfo.Length; i++)
            {
                var ally = CTeamInfo[i];
                if (ally != null && ally.Actor != null)
                {
                    StartCoroutine(AttackSequence(ally, target, target.SlotTransform.position, true));
                }
            }
        }
    }

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
    private IEnumerator AttackSequence(TeamSlotInfo attacker, TeamSlotInfo target, Vector3 targetPoint, bool perfect)
    {
        var actor = attacker.Actor.transform;
        Vector3 origin = actor.position;

        bool isFront = (CTeamInfo.Length > 0 && CTeamInfo[0] == attacker);

        if (!isFront)
        {
            // ★ 後排 → 統一使用 Dash 攻擊
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
            yield break;
        }

        // ★ 前排角色使用其專屬技能
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

                    var skill = Instantiate(rangedVfxPrefab, actor.position, Quaternion.identity);
                    var fireball = skill.GetComponent<FireBallSkill>();
                    if (fireball != null)
                    {
                        fireball.attacker = attacker;
                        fireball.target = target;
                        fireball.isPerfect = perfect;
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
                    var skill = Instantiate(rangedVfxPrefab, actor.position, Quaternion.identity);
                    var arrow = skill.GetComponent<FireBallSkill>();
                    if (arrow != null)
                    {
                        arrow.attacker = attacker;
                        arrow.target = target;
                        arrow.isPerfect = perfect;
                    }
                    break;
                }
        }
    }

    private TeamSlotInfo FindEnemyByClass(UnitClass cls)
    {
        if (cls == UnitClass.Warrior)
        {
            for (int i = 0; i < ETeamInfo.Length; i++)
                if (ETeamInfo[i] != null && ETeamInfo[i].Actor != null)
                    return ETeamInfo[i];
        }
        else if (cls == UnitClass.Mage)
        {
            for (int i = ETeamInfo.Length - 1; i >= 0; i--)
                if (ETeamInfo[i] != null && ETeamInfo[i].Actor != null)
                    return ETeamInfo[i];
        }
        return FindNextValidEnemy(0);
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
