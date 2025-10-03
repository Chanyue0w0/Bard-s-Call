using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem; // �s Input System
using System.Linq;

public class BattleManager : MonoBehaviour
{
    // ���
    public static BattleManager Instance { get; private set; }

    [System.Serializable]
    public enum UnitClass
    {
        Warrior,  // �쥻 Melee
        Mage,     // �쥻 Ranged
        Shield,
        Priest,
        Ranger
    }

    [System.Serializable]
    public class TeamSlotInfo
    {
        [Header("���W���p")]
        public string UnitName;
        public GameObject Actor;
        public Transform SlotTransform;
        public UnitClass ClassType = UnitClass.Warrior;

        [Header("�԰��ƭ�")]
        public int MaxHP = 100;
        public int HP = 100;
        public int MaxMP = 100;
        public int MP = 0;
        public int OriginAtk = 10;
        public int Atk = 10;
        public string SkillName = "Basic";

        [Header("��J�j�w")]
        public int AssignedKeyIndex; // �O�d�A��������� P1 ����
    }

    [Header("�ڤ�T�w�y�С]�۰ʦb Start �O���^")]
    [SerializeField] private Transform[] playerPositions = new Transform[3];

    [Header("�Ĥ�T�w�y�С]�۰ʦb Start �O���^")]
    [SerializeField] private Transform[] enemyPositions = new Transform[3];

    [Header("�ڤ�T���ơ]�k���^")]
    public TeamSlotInfo[] CTeamInfo = new TeamSlotInfo[3];

    [Header("�Ĥ�T���ơ]�����^")]
    public TeamSlotInfo[] ETeamInfo = new TeamSlotInfo[3];

    [Header("��J�]�� InputActionReference �j�w�^")]
    public InputActionReference actionAttackP1;   // Q / X
    public InputActionReference actionRotateLeft; // LeftArrow / LT
    public InputActionReference actionRotateRight; // RightArrow / RT

    [Header("�ɧǻP�B�ʰѼ�")]
    public float actionLockDuration = 0.5f;
    public float dashDuration = 0.05f;
    public Vector3 meleeContactOffset = new Vector3(-1f, 0f, 0f);

    [Header("�S�� Prefab")]
    public GameObject meleeVfxPrefab;
    public GameObject rangedVfxPrefab;
    public GameObject shieldStrikeVfxPrefab;
    public GameObject missVfxPrefab;
    public float vfxLifetime = 1.5f;

    [Header("Shield �]�w")]
    public float shieldBlockDuration = 2.0f;
    public int shieldDamage = 10;

    private bool _isActionLocked;
    private readonly WaitForEndOfFrame _endOfFrame = new WaitForEndOfFrame();

    [Header("��� UI")]
    public GameObject healthBarPrefab;
    public Canvas uiCanvas;

    [Header("���ಾ�ʳ]�w")]
    public float rotateMoveDuration = 0.2f; // ����ɲ��ʹL�h���ɶ�


    // ---------------- Singleton �]�w ----------------
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
        // �إߦ��
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

    // ================= �����޿� =================
    private void OnAttackP1(InputAction.CallbackContext ctx)
    {
        if (CTeamInfo[0] == null) return;

        bool perfect = BeatJudge.Instance.IsOnBeat();
        var attacker = CTeamInfo[0];
        var target = FindEnemyByClass(attacker.ClassType);

        if (target == null) return;

        // P1 ����
        StartCoroutine(AttackSequence(attacker, target, target.SlotTransform.position, perfect));

        // �p�G perfect �� ��Ƥ]�����P�@�ؼ�
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


    // �ھ�¾�~��ܧ����ؼ�
    private TeamSlotInfo FindEnemyByClass(UnitClass cls)
    {
        if (cls == UnitClass.Warrior)
        {
            // �q�e�����Ĥ@�Ӭ��۪�
            for (int i = 0; i < ETeamInfo.Length; i++)
            {
                if (ETeamInfo[i] != null && ETeamInfo[i].Actor != null)
                    return ETeamInfo[i];
            }
        }
        else if (cls == UnitClass.Mage)
        {
            // �q�᩹�e��Ĥ@�Ӭ��۪�
            for (int i = ETeamInfo.Length - 1; i >= 0; i--)
            {
                if (ETeamInfo[i] != null && ETeamInfo[i].Actor != null)
                    return ETeamInfo[i];
            }
        }
        else
        {
            // �w�]�G��Ĥ@�Ӭ��۪�
            return FindNextValidEnemy(0);
        }
        return null;
    }

    // ================= �����޿� =================
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
        Debug.Log("����ɰw����");
    }

    private void RotateTeamCounterClockwise()
    {
        // P1->P3, P3->P2, P2->P1
        var temp = CTeamInfo[0];
        CTeamInfo[0] = CTeamInfo[1];
        CTeamInfo[1] = CTeamInfo[2];
        CTeamInfo[2] = temp;
        UpdatePositions();
        Debug.Log("����f�ɰw����");
    }

    private void UpdatePositions()
    {
        for (int i = 0; i < CTeamInfo.Length; i++)
        {
            if (CTeamInfo[i] != null && CTeamInfo[i].Actor != null)
            {
                // ���A�����A�ӬO�Ψ�{�ưʹL�h
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

    // ================= �O�d��l�����y�{ =================
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
