using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem; // �s Input System

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
        public int AssignedKeyIndex; // 0=E, 1=W, 2=Q
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
    public InputActionReference actionAttackPos1; // Pos1
    public InputActionReference actionAttackPos2; // Pos2
    public InputActionReference actionAttackPos3; // Pos3
    public InputActionReference actionRotateTeam; // RotateTeam

    [Header("�ɧǻP�B�ʰѼ�")]
    public float actionLockDuration = 0.5f;
    public float dashDuration = 0.05f;
    public Vector3 meleeContactOffset = new Vector3(-1f, 0f, 0f);

    [Header("�S�� Prefab�]����u���ͦ��^")]
    public GameObject meleeVfxPrefab;
    public GameObject rangedVfxPrefab;
    public GameObject shieldStrikeVfxPrefab;
    public GameObject missVfxPrefab;   // �� �s�W�GMiss �S��
    public float vfxLifetime = 1.5f;

    [Header("Shield �]�w")]
    public float shieldBlockDuration = 2.0f;  // �� Shield ���ɫ���ɶ��]��ơA�i�� 2 ��վ�^
    public int shieldDamage = 10;             // �� Shield ���ɮɳy�����ˮ`


    private bool _isActionLocked;
    private readonly WaitForEndOfFrame _endOfFrame = new WaitForEndOfFrame();

    [Header("��� UI")]
    public GameObject healthBarPrefab;   // ���w��� Prefab
    public Canvas uiCanvas;              // UI Canvas (Screen Space - Camera)



    // ---------------- Singleton �]�w ----------------
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // �O�ҥu���@�Ӧs�b
            return;
        }
        Instance = this;
        //DontDestroyOnLoad(gameObject); // �������]���|�Q�R��
    }

    void OnEnable()
    {
        if (actionAttackPos1 != null)
        {
            actionAttackPos1.action.started -= OnAttackPos1;
            actionAttackPos1.action.started += OnAttackPos1;
            actionAttackPos1.action.Enable();
        }
        if (actionAttackPos2 != null)
        {
            actionAttackPos2.action.started -= OnAttackPos2;
            actionAttackPos2.action.started += OnAttackPos2;
            actionAttackPos2.action.Enable();
        }
        if (actionAttackPos3 != null)
        {
            actionAttackPos3.action.started -= OnAttackPos3;
            actionAttackPos3.action.started += OnAttackPos3;
            actionAttackPos3.action.Enable();
        }
    }

    void OnDisable()
    {
        if (actionAttackPos1 != null)
            actionAttackPos1.action.started -= OnAttackPos1;
        if (actionAttackPos2 != null)
            actionAttackPos2.action.started -= OnAttackPos2;
        if (actionAttackPos3 != null)
            actionAttackPos3.action.started -= OnAttackPos3;
    }


    void Start()
    {
        // �إߧڤ���
        CreateHealthBars(CTeamInfo);
        // �إ߼Ĥ���
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

    private void OnAttackPos1(InputAction.CallbackContext ctx)
    {
        // Q �� P1
        TryStartAttack(2);
    }

    private void OnAttackPos2(InputAction.CallbackContext ctx)
    {
        // W �� P2
        TryStartAttack(1);
    }

    private void OnAttackPos3(InputAction.CallbackContext ctx)
    {
        // E �� P3
        TryStartAttack(0);
    }



    private void HandleAttackKey(int keyIndex)
    {
        for (int i = 0; i < CTeamInfo.Length; i++)
        {
            if (CTeamInfo[i].AssignedKeyIndex == keyIndex)
            {
                TryStartAttack(i);
                return;
            }
        }
    }

    // ���ձq���w�ڤ�Ѧ�o�ʧ����]�������^
    private void TryStartAttack(int slotIndex)
    {
        if (_isActionLocked) return;
        if (slotIndex < 0 || slotIndex >= CTeamInfo.Length) return;

        var attacker = CTeamInfo[slotIndex];
        var target = ETeamInfo[slotIndex];

        // �� �����`���P�w
        bool perfect = BeatJudge.Instance.IsOnBeat();

        if (target == null || target.Actor == null)
        {
            Debug.Log($"�^�� {attacker.UnitName} �����]�S���ĤH�^ Perfect={perfect}");
            return;
        }

        var targetPoint = target.SlotTransform != null
            ? target.SlotTransform.position
            : GetFallbackEnemyPoint(slotIndex);

        // �� ��P�w���G�Ƕi�h
        StartCoroutine(AttackSequence(attacker, target, targetPoint, perfect));

        Debug.Log($"�^�� P{slotIndex + 1} ���� �� �ĤH E{slotIndex + 1} Perfect={perfect}");
    }


    private IEnumerator AttackSequence(TeamSlotInfo attacker, TeamSlotInfo target, Vector3 targetPoint, bool perfect)
    {
        _isActionLocked = true;
        float startTime = Time.time;

        var actor = attacker.Actor.transform;
        Vector3 origin = actor.position;

        if (attacker.ClassType == UnitClass.Warrior)
        {
            // Warrior �� ����޿�
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
            // Mage �� ���{�޿�
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
            // Shield �� �����޿�
            if (perfect)
            {
                Debug.Log("Shield Perfect ���ɡG������o�K�˪��A + �ͦ����� Fireball");

                // �� ������������ Buff
                BattleEffectManager.Instance.ActivateShield(shieldBlockDuration);

                // �� ���Ĥ@�ӥi�������ĤH�]�u�� slotIndex�A�Y�ūh�����^
                TeamSlotInfo shieldTarget = null;
                for (int i = target.AssignedKeyIndex; i < ETeamInfo.Length; i++)
                {
                    if (ETeamInfo[i] != null && ETeamInfo[i].Actor != null)
                    {
                        shieldTarget = ETeamInfo[i];
                        break;
                    }
                }

                if (shieldTarget != null)
                {
                    var strikeObj = Instantiate(shieldStrikeVfxPrefab, actor.position, Quaternion.identity);
                    var strike = strikeObj.GetComponent<ShieldStrike>();
                    if (strike != null)
                    {
                        strike.attacker = attacker;
                        strike.target = shieldTarget;
                        strike.overrideDamage = shieldDamage; // �T�w 10 �I�ˮ`
                    }

                    Debug.Log($"Shield ���� Fireball �� {shieldTarget.UnitName}�A�T�w {shieldDamage} �ˮ`");
                }
            }
            else
            {
                Debug.Log("Shield Miss�A�ͦ� Miss �S��");
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


    //private void ApplyShieldBlock()
    //{
    //    StartCoroutine(ShieldBlockCoroutine());
    //}

    //private IEnumerator ShieldBlockCoroutine()
    //{
    //    Debug.Log("�����i�J�K�˪��A");
    //    bool isShielding = true;

    //    // �o�̧A�i�H�� UI �S�� or ���A�аO
    //    // e.g., BattleEffectManager.Instance.SetInvincible(CTeamInfo, true);

    //    yield return new WaitForSeconds(shieldBlockDuration);

    //    // �����K��
    //    isShielding = false;
    //    Debug.Log("�����K�˪��A����");
    //    // e.g., BattleEffectManager.Instance.SetInvincible(CTeamInfo, false);
    //}


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

    //private void SpawnVfx(GameObject prefab, Vector3 atWorldPos)
    //{
    //    if (prefab == null) return;
    //    var go = Instantiate(prefab, atWorldPos, prefab.transform.rotation);
    //    if (vfxLifetime > 0f) Destroy(go, vfxLifetime);
    //}

    private Vector3 GetFallbackEnemyPoint(int slotIndex)
    {
        var self = CTeamInfo[slotIndex];
        if (self != null && self.SlotTransform != null)
            return self.SlotTransform.position + new Vector3(3f, 0f, 0f);
        return transform.position;
    }
}
