using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem; // 新 Input System
using System.Linq;

public class BattleManager : MonoBehaviour
{
    // 單例
    public static BattleManager Instance { get; private set; }

    [System.Serializable]
    public enum UnitClass
    {
        Warrior,  // 原本 Melee
        Mage,     // 原本 Ranged
        Shield,
        Priest,
        Ranger
    }

    [System.Serializable]
    public class TeamSlotInfo
    {
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
        public string SkillName = "Basic";

        [Header("輸入綁定")]
        public int AssignedKeyIndex; // 保留，但攻擊改由 P1 控制
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
    public InputActionReference actionAttackP1;   // Q / X
    public InputActionReference actionRotateLeft; // LeftArrow / LT
    public InputActionReference actionRotateRight; // RightArrow / RT

    [Header("時序與運動參數")]
    public float actionLockDuration = 0.5f;
    public float dashDuration = 0.05f;
    public Vector3 meleeContactOffset = new Vector3(-1f, 0f, 0f);

    [Header("特效 Prefab")]
    public GameObject meleeVfxPrefab;
    public GameObject rangedVfxPrefab;
    public GameObject shieldStrikeVfxPrefab;
    public GameObject missVfxPrefab;
    public float vfxLifetime = 1.5f;

    [Header("Shield 設定")]
    public float shieldBlockDuration = 2.0f;
    public int shieldDamage = 10;

    private bool _isActionLocked;
    private readonly WaitForEndOfFrame _endOfFrame = new WaitForEndOfFrame();

    [Header("血條 UI")]
    public GameObject healthBarPrefab;
    public Canvas uiCanvas;

    [Header("旋轉移動設定")]
    public float rotateMoveDuration = 0.2f; // 旋轉時移動過去的時間


    // ---------------- Singleton 設定 ----------------
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
        // 建立血條
        CreateHealthBars(CTeamInfo);
        CreateHealthBars(ETeamInfo);
    }

    private void CreateHealthBars(TeamSlotInfo[] team)
    {
        foreach (var slot in team)
        {
            if (slot != null && slot.Actor != null)
            {
                Transform headPoint = slot.Actor.transform.Find("HeadPoint");
                if (headPoint != null)
                {
                    GameObject hb = Instantiate(healthBarPrefab, uiCanvas.transform);
                    hb.GetComponent<HealthBarUI>().Init(slot, headPoint, uiCanvas.worldCamera);
                }
            }
        }
    }

    // ================= 攻擊邏輯 =================
    private void OnAttackP1(InputAction.CallbackContext ctx)
    {
        if (CTeamInfo[0] == null) return;

        bool perfect = BeatJudge.Instance.IsOnBeat();
        var attacker = CTeamInfo[0];
        var target = FindEnemyByClass(attacker.ClassType);

        if (target == null) return;

        // P1 攻擊
        StartCoroutine(AttackSequence(attacker, target, target.SlotTransform.position, perfect));

        // 如果 perfect → 後排也攻擊同一目標
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


    // 根據職業選擇攻擊目標
    private TeamSlotInfo FindEnemyByClass(UnitClass cls)
    {
        if (cls == UnitClass.Warrior)
        {
            // 從前往後找第一個活著的
            for (int i = 0; i < ETeamInfo.Length; i++)
            {
                if (ETeamInfo[i] != null && ETeamInfo[i].Actor != null)
                    return ETeamInfo[i];
            }
        }
        else if (cls == UnitClass.Mage)
        {
            // 從後往前找第一個活著的
            for (int i = ETeamInfo.Length - 1; i >= 0; i--)
            {
                if (ETeamInfo[i] != null && ETeamInfo[i].Actor != null)
                    return ETeamInfo[i];
            }
        }
        else
        {
            // 預設：找第一個活著的
            return FindNextValidEnemy(0);
        }
        return null;
    }

    // ================= 旋轉邏輯 =================
    private void OnRotateLeft(InputAction.CallbackContext ctx)
    {
        RotateTeamCounterClockwise();
    }

    private void OnRotateRight(InputAction.CallbackContext ctx)
    {
        RotateTeamClockwise();
    }

    private void RotateTeamClockwise()
    {
        // P1->P2, P2->P3, P3->P1
        var temp = CTeamInfo[2];
        CTeamInfo[2] = CTeamInfo[1];
        CTeamInfo[1] = CTeamInfo[0];
        CTeamInfo[0] = temp;
        UpdatePositions();
        Debug.Log("隊伍順時針旋轉");
    }

    private void RotateTeamCounterClockwise()
    {
        // P1->P3, P3->P2, P2->P1
        var temp = CTeamInfo[0];
        CTeamInfo[0] = CTeamInfo[1];
        CTeamInfo[1] = CTeamInfo[2];
        CTeamInfo[2] = temp;
        UpdatePositions();
        Debug.Log("隊伍逆時針旋轉");
    }

    private void UpdatePositions()
    {
        for (int i = 0; i < CTeamInfo.Length; i++)
        {
            if (CTeamInfo[i] != null && CTeamInfo[i].Actor != null)
            {
                // 不再瞬移，而是用協程滑動過去
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
            if (t > 1f) t = 1f;
            actor.position = Vector3.Lerp(start, targetPos, t);
            yield return null;
        }
    }

    // ================= 保留原始攻擊流程 =================
    private IEnumerator AttackSequence(TeamSlotInfo attacker, TeamSlotInfo target, Vector3 targetPoint, bool perfect)
    {
        _isActionLocked = true;
        float startTime = Time.time;

        var actor = attacker.Actor.transform;
        Vector3 origin = actor.position;

        if (attacker.ClassType == UnitClass.Warrior)
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

            yield return _endOfFrame;
            actor.position = origin;
        }
        else if (attacker.ClassType == UnitClass.Mage)
        {
            var skill = Instantiate(rangedVfxPrefab, actor.position, Quaternion.identity);
            var fireball = skill.GetComponent<FireBallSkill>();
            if (fireball != null)
            {
                fireball.attacker = attacker;
                fireball.target = target;
                fireball.isPerfect = perfect;
            }
        }
        else if (attacker.ClassType == UnitClass.Shield)
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
            else
            {
                if (missVfxPrefab != null)
                {
                    Instantiate(missVfxPrefab, actor.position, Quaternion.identity);
                }
            }
        }

        float remain = actionLockDuration - (Time.time - startTime);
        if (remain > 0f) yield return new WaitForSeconds(remain);

        _isActionLocked = false;
    }

    private TeamSlotInfo FindNextValidEnemy(int startIndex)
    {
        for (int i = startIndex; i < ETeamInfo.Length; i++)
        {
            if (ETeamInfo[i] != null && ETeamInfo[i].Actor != null)
            {
                return ETeamInfo[i];
            }
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
            if (t > 1f) t = 1f;
            actor.position = Vector3.Lerp(from, to, t);
            yield return null;
        }
    }
}
