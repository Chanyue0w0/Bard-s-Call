using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem; // �s Input System
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

        [Header("Prefab �]�w")]
        public GameObject PrefabToSpawn; // �Y�����w Actor�A�N�۰ʥͦ��� Prefab

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

        [Header("�ޯ�")]
        public string[] SkillNames;
        public GameObject[] SkillPrefabs;

        [Header("���q����")]
        public string[] NormalAttackNames;
        public GameObject[] NormalAttackPrefabs;
        
        [Header("��J�j�w")]
        public int AssignedKeyIndex;

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
    public InputActionReference actionAttackP1;
    public InputActionReference actionAttackP2;
    public InputActionReference actionAttackP3;
    public InputActionReference actionRotateLeft;
    public InputActionReference actionRotateRight;

    [Header("�ɧǻP�B�ʰѼ�")]
    public float actionLockDuration = 0.5f;
    public float dashDuration = 0.05f;
    public Vector3 meleeContactOffset = new Vector3(-1f, 0f, 0f);

    [Header("�S�� Prefab")]
    public GameObject meleeVfxPrefab;
    public GameObject rangedVfxPrefab;
    public GameObject shieldStrikeVfxPrefab;
    public GameObject missVfxPrefab;
    public GameObject magicUseAuraPrefab;
    public float vfxLifetime = 1.5f;

    [Header("Shield �]�w")]
    public float shieldBlockDuration = 2.0f;
    public int shieldDamage = 10;

    private bool _isActionLocked;

    [Header("��� UI")]
    public GameObject healthBarPrefab;
    public Canvas uiCanvas;

    [Header("���ಾ�ʳ]�w")]
    public float rotateMoveDuration = 0.2f;

    [Header("��ԧ����]�w")]
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
        // ��L�����J����
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

        // �ƻs������
        this.CTeamInfo = teamMgr.CTeamInfo;
        this.ETeamInfo = teamMgr.ETeamInfo;

        Debug.Log("���J����\�A���a����G" +
        string.Join(", ", CTeamInfo.Where(x => x != null).Select(x => x.UnitName)));
    }

    // ================= �����޿� =================
    private void OnAttackKey(int index)
    {
        if (_isActionLocked) return;
        if (index < 0 || index >= CTeamInfo.Length) return;
        if (CTeamInfo[index] == null) return;

        bool perfect = BeatJudge.Instance.IsOnBeat();
        if (!perfect)
        {
            Debug.Log("Miss�I���b�`��W�A��Ĳ�o�����C");
            return;
        }

        var attacker = CTeamInfo[index];
        var target = FindEnemyByClass(attacker.ClassType);
        if (target == null) return;

        StartCoroutine(LockAction(actionLockDuration));

        // �� �ھک�ƧP�_��������
        //int beatInCycle = BeatManager.Instance.currentBeatInCycle;
        int beatInCycle = BeatManager.Instance.predictedNextBeat;


        if (attacker.ClassType == UnitClass.Warrior)
        {
            // �I�s�s�禡�B�z�h�q����
            StartCoroutine(HandleWarriorAttack(attacker, target, beatInCycle, perfect));
        }
        else
        {
            // ��L¾�~�]�|���X�R�^
            StartCoroutine(AttackSequence(attacker, target, target.SlotTransform.position, perfect));
        }
    }

    // ============================================================
    // Warrior 3 �q���� + ��4�筫��
    // ============================================================
    // ============================================================
    // Warrior 3 �q���� + ��4�筫��
    // ============================================================
    // ============================================================
    // Warrior 3 �q���� + ��4�筫��]�۰ʰO�Ьq�ơ^
    // ============================================================
    private IEnumerator HandleWarriorAttack(TeamSlotInfo attacker, TeamSlotInfo target, int beatInCycle, bool perfect)
    {
        var actor = attacker.Actor.transform;
        Vector3 origin = actor.position;
        Vector3 targetPoint = target.SlotTransform.position + meleeContactOffset;

        var charData = attacker.Actor.GetComponent<CharacterData>();
        if (charData == null)
        {
            Debug.LogWarning("����S�� CharacterData�A�ϥιw�]�����C");
            yield return AttackSequence(attacker, target, targetPoint, perfect);
            yield break;
        }

        // ���o combo ���A
        var comboData = actor.GetComponent<CharacterComboState>();
        if (comboData == null)
            comboData = actor.gameObject.AddComponent<CharacterComboState>();

        // �Y���ɶ��S�����h���m combo
        if (Time.time - comboData.lastAttackTime > 2f)
            comboData.currentPhase = 1;

        SkillInfo chosenSkill = null;
        GameObject attackPrefab = null;

        // �� �ĥ|��T�w����A��l�T�笰����
        if (beatInCycle == 4)
        {
            chosenSkill = charData.HeavyAttack;
            attackPrefab = chosenSkill?.SkillPrefab;
            comboData.currentPhase = 1; // �� ���s����q��
            Debug.Log($"Warrior �ĥ|�筫�����G{chosenSkill?.SkillName ?? "���]�w"}");
        }
        else
        {
            int phase = comboData.currentPhase;
            int attackIndex = Mathf.Clamp(phase - 1, 0, charData.NormalAttacks.Count - 1);
            chosenSkill = charData.NormalAttacks[attackIndex];
            attackPrefab = chosenSkill?.SkillPrefab;
            Debug.Log($"Warrior ����q {phase}�G{chosenSkill?.SkillName ?? "���]�w"}");

            // ��s combo ���A�]1��2��3 �`���^
            comboData.currentPhase = (phase % 3) + 1;
        }

        // �w�]�S�ġ]�Y�䤣�� prefab�^
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

    // ================= �����޿� =================
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

    // ================= �����ǦC =================
    // ================= �����ǦC =================
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

                    // ��������G�C�ӼĤH���ͦ� FireBall
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
                    // �M��̫�@�즳�ļĤH�]�Y�Ŧ�h���e��^
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

    // ================= �����ؼзj�M =================
    private TeamSlotInfo FindEnemyByClass(UnitClass cls)
    {
        // Warrior�G�����e��Ĥ@��ĤH
        if (cls == UnitClass.Warrior)
        {
            for (int i = 0; i < ETeamInfo.Length; i++)
                if (ETeamInfo[i] != null && ETeamInfo[i].Actor != null)
                    return ETeamInfo[i];
        }

        // Mage�G���ݭn�S�w�ؼСA��������ɷ|�� ETeamInfo ���N
        if (cls == UnitClass.Mage)
        {
            return ETeamInfo.FirstOrDefault(e => e != null && e.Actor != null);
        }

        // Ranger�G�����̫�@�즳�ļĤH
        if (cls == UnitClass.Ranger)
        {
            return FindLastValidEnemy();
        }

        return FindNextValidEnemy(0);
    }

    // �M��̫�@�줴�s�b���ĤH�]�ѫ᩹�e��^
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
