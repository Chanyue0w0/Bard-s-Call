using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    [System.Serializable]
    public enum ETeam
    {
        None,
        Player,
        Enemy
    }

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
        public GameObject PrefabToSpawn;

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

    [Header("�ڤ�T�w�y�С]�k���^")]
    [SerializeField] private Transform[] playerPositions = new Transform[3];

    [Header("�Ĥ�T�w�y�С]�����^")]
    [SerializeField] private Transform[] enemyPositions = new Transform[3];

    [Header("�ڤ�T����")]
    public TeamSlotInfo[] CTeamInfo = new TeamSlotInfo[3];

    [Header("�Ĥ�T����")]
    public TeamSlotInfo[] EnemyTeamInfo = new TeamSlotInfo[3];  // �� ��W�קK enum �Ĭ�

    [Header("��J�]�s Input System�^")]
    public InputActionReference actionAttackP1;
    public InputActionReference actionAttackP2;
    public InputActionReference actionAttackP3;
    public InputActionReference actionRotateLeft;
    public InputActionReference actionRotateRight;
    public InputActionReference actionBlockP1;
    public InputActionReference actionBlockP2;
    public InputActionReference actionBlockP3;

    [Header("�ɧǻP�B�ʰѼ�")]
    public float actionLockDuration = 0.5f;
    public float dashDuration = 0.05f;
    public float dashStayDuration = 0.15f;
    public float rotateMoveDuration = 0.2f;
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

    [Header("��� UI")]
    public GameObject healthBarPrefab;
    public Canvas uiCanvas;

    private bool _isActionLocked;
    private bool _isBlockingActive = false;
    private GameObject lastSuccessfulAttacker = null;

    // �Ω�w���Ѱ� Input �j�w
    private System.Action<InputAction.CallbackContext> attackP1Handler;
    private System.Action<InputAction.CallbackContext> attackP2Handler;
    private System.Action<InputAction.CallbackContext> attackP3Handler;
    private System.Action<InputAction.CallbackContext> blockP1Handler;
    private System.Action<InputAction.CallbackContext> blockP2Handler;
    private System.Action<InputAction.CallbackContext> blockP3Handler;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // �T�O�}�C��l��
        if (CTeamInfo == null || CTeamInfo.Length == 0)
            CTeamInfo = new TeamSlotInfo[3];
        if (EnemyTeamInfo == null || EnemyTeamInfo.Length == 0)
            EnemyTeamInfo = new TeamSlotInfo[3];
    }

    private void OnEnable()
    {
        attackP1Handler = ctx => OnAttackKey(0);
        attackP2Handler = ctx => OnAttackKey(1);
        attackP3Handler = ctx => OnAttackKey(2);
        blockP1Handler = ctx => OnBlockKey(0);
        blockP2Handler = ctx => OnBlockKey(1);
        blockP3Handler = ctx => OnBlockKey(2);

        if (actionAttackP1 != null) { actionAttackP1.action.started += attackP1Handler; actionAttackP1.action.Enable(); }
        if (actionAttackP2 != null) { actionAttackP2.action.started += attackP2Handler; actionAttackP2.action.Enable(); }
        if (actionAttackP3 != null) { actionAttackP3.action.started += attackP3Handler; actionAttackP3.action.Enable(); }
        if (actionBlockP1 != null) { actionBlockP1.action.started += blockP1Handler; actionBlockP1.action.Enable(); }
        if (actionBlockP2 != null) { actionBlockP2.action.started += blockP2Handler; actionBlockP2.action.Enable(); }
        if (actionBlockP3 != null) { actionBlockP3.action.started += blockP3Handler; actionBlockP3.action.Enable(); }
    }

    private void OnDisable()
    {
        if (actionAttackP1 != null) actionAttackP1.action.started -= attackP1Handler;
        if (actionAttackP2 != null) actionAttackP2.action.started -= attackP2Handler;
        if (actionAttackP3 != null) actionAttackP3.action.started -= attackP3Handler;
        if (actionBlockP1 != null) actionBlockP1.action.started -= blockP1Handler;
        if (actionBlockP2 != null) actionBlockP2.action.started -= blockP2Handler;
        if (actionBlockP3 != null) actionBlockP3.action.started -= blockP3Handler;
    }

    // --------------------------------------------------
    // �����Ƹ��J
    // --------------------------------------------------
    public void LoadTeamData(BattleTeamManager teamMgr)
    {
        if (teamMgr == null) return;

        CTeamInfo = teamMgr.CTeamInfo.ToArray();        // �`�����קK�@�ΰѦ�
        EnemyTeamInfo = teamMgr.EnemyTeamInfo.ToArray();

        Debug.Log("���J����\�A���a����G" +
            string.Join(", ", CTeamInfo.Where(x => x != null).Select(x => x.UnitName)));
    }

    // --------------------------------------------------
    // �����޿�
    // --------------------------------------------------
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

        // �����⭫�m combo
        //if (lastSuccessfulAttacker != null && lastSuccessfulAttacker != attacker.Actor)
        //    ResetAllComboStates();

        lastSuccessfulAttacker = attacker.Actor;
        StartCoroutine(LockAction(actionLockDuration));

        int beatInCycle = BeatManager.Instance.predictedNextBeat;

        if (attacker.ClassType == UnitClass.Warrior)
            StartCoroutine(HandleWarriorAttack(attacker, target, beatInCycle, perfect));
        else if (attacker.ClassType == UnitClass.Mage)
            StartCoroutine(HandleMageAttack(attacker, target, beatInCycle, perfect));
        else
            StartCoroutine(AttackSequence(attacker, target, target.SlotTransform.position, perfect));

    }

    private IEnumerator HandleWarriorAttack(TeamSlotInfo attacker, TeamSlotInfo target, int beatInCycle, bool perfect)
    {
        var actor = attacker.Actor.transform;
        Vector3 origin = actor.position;
        Vector3 targetPoint = target.SlotTransform.position + meleeContactOffset;

        var charData = attacker.Actor.GetComponent<CharacterData>();
        if (charData == null)
        {
            yield return AttackSequence(attacker, target, targetPoint, perfect);
            yield break;
        }

        SkillInfo chosenSkill = null;
        GameObject attackPrefab = null;

        // �� �s�޿�G�u�n�O�ĥ|��NĲ�o������
        if (beatInCycle == BeatManager.Instance.beatsPerMeasure)
        {
            chosenSkill = charData.HeavyAttack;
            attackPrefab = chosenSkill?.SkillPrefab;
            Debug.Log($"[�Ԥh������] �� {beatInCycle} ��Ĳ�o�������I");
        }
        else
        {
            // �̩�ƴ`�����q�����]�Ҧp��1~3��Τ@������`���^
            int phase = ((beatInCycle - 1) % 3);
            chosenSkill = charData.NormalAttacks[phase];
            attackPrefab = chosenSkill?.SkillPrefab;
            Debug.Log($"[�Ԥh����] �� {beatInCycle} ��A�ϥβ� {phase + 1} ������C");
        }

        if (attackPrefab == null && meleeVfxPrefab != null)
            attackPrefab = meleeVfxPrefab;

        // �e�i����
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
                sword.isHeavyAttack = (beatInCycle == BeatManager.Instance.beatsPerMeasure);
            }
        }

        yield return new WaitForSeconds(dashStayDuration);
        yield return Dash(actor, targetPoint, origin, dashDuration);
    }

    private IEnumerator HandleMageAttack(TeamSlotInfo attacker, TeamSlotInfo target, int beatInCycle, bool perfect)
    {
        var actor = attacker.Actor.transform;
        var charData = attacker.Actor.GetComponent<CharacterData>();
        if (charData == null) yield break;

        // ���o�ثe�R�q�h��
        int chargeStacks = BattleEffectManager.Instance.GetChargeStacks(attacker);

        // === �ĥ|��G���񭫧��� ===
        if (beatInCycle == BeatManager.Instance.beatsPerMeasure)
        {
            if (chargeStacks <= 0)
            {
                Debug.Log("[�k�v������] �L�R��h�A�����L�ġC");
                yield break;
            }

            Debug.Log($"[�k�v������] �I�񭫧����A���� {chargeStacks} �h�R�q�C");
            BattleEffectManager.Instance.ResetChargeStacks(attacker); // �M���R�q�h�P�S��


            // �ͦ��������S��
            if (charData.HeavyAttack != null && charData.HeavyAttack.SkillPrefab != null)
            {
                var heavy = Instantiate(charData.HeavyAttack.SkillPrefab, target.SlotTransform.position, Quaternion.identity);
                var skill = heavy.GetComponent<FireBallSkill>();
                if (skill != null)
                {
                    skill.attacker = attacker;
                    skill.target = target;
                    skill.isPerfect = perfect;
                    skill.isHeavyAttack = true;
                    skill.damage = chargeStacks * 30;
                }
            }

            // �p��ˮ` = chargeStacks * 30
            int damage = chargeStacks * 30;
            target.HP -= damage;
            if (target.HP < 0) target.HP = 0;

            Debug.Log($"[�k�v������] {attacker.UnitName} �� {target.UnitName} �y�� {damage} �ˮ`�]�R�q�h�G{chargeStacks}�^");

            // ���m�R�q�h
            BattleEffectManager.Instance.ResetChargeStacks(attacker);

            // ��s���
            var hb = target.Actor?.GetComponentInChildren<HealthBarUI>();
            if (hb != null) hb.ForceUpdate();

            //if (target.HP <= 0)
            //    BattleEffectManager.Instance.HandleUnitDefeated(target);
        }
        else
        {
            // === ���q�����G0 �ˮ`�A��o�@�h�R�� ===
            Debug.Log($"[�k�v����] �� {beatInCycle} ��R�� +1 �h�C");

            // �ͦ��R��G���S�ġ]�L�׬O�_�w���h�^
            if (charData.NormalAttacks != null && charData.NormalAttacks.Count > 0)
            {
                var chargeEffect = charData.NormalAttacks[0].SkillPrefab;
                if (chargeEffect != null)
                {
                    Vector3 spawnPos = actor.position;
                    Instantiate(chargeEffect, spawnPos, Quaternion.identity);
                    Debug.Log($"[�k�v����S��] {attacker.UnitName} �ͦ��G���S�ĩ� {spawnPos}");
                }
            }

            // �q�� BattleEffectManager �B�z�R��h�P�S��
            BattleEffectManager.Instance.AddChargeStack(attacker);

        }

        yield return null;
    }


    // --------------------------------------------------
    // ����
    // --------------------------------------------------
    private void OnBlockKey(int index)
    {
        if (_isActionLocked) return;
        if (index < 0 || index >= CTeamInfo.Length) return;
        var slot = CTeamInfo[index];
        if (slot == null || slot.Actor == null) return;

        bool perfect = BeatJudge.Instance.IsOnBeat();
        if (!perfect)
        {
            Debug.Log("Miss�I���ɥ��b�`��W�C");
            return;
        }

        var charData = slot.Actor.GetComponent<CharacterData>();
        if (charData == null) return;

        BattleEffectManager.Instance.ActivateBlock(index, BeatManager.Instance.beatTravelTime, charData, slot.Actor);
    }

    private void ResetAllComboStates()
    {
        foreach (var slot in CTeamInfo)
        {
            if (slot?.Actor == null) continue;
            var combo = slot.Actor.GetComponent<CharacterComboState>();
            if (combo != null)
            {
                combo.comboCount = 0;
                combo.currentPhase = 1;
            }
        }
        lastSuccessfulAttacker = null;
    }

    // --------------------------------------------------
    // �����޿�
    // --------------------------------------------------
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
            if (CTeamInfo[i]?.Actor == null) continue;
            StartCoroutine(MoveToPosition(CTeamInfo[i].Actor.transform, playerPositions[i].position, rotateMoveDuration));
            CTeamInfo[i].SlotTransform = playerPositions[i];
        }
    }

    private IEnumerator MoveToPosition(Transform actor, Vector3 targetPos, float duration)
    {
        if (actor == null) yield break;
        Vector3 start = actor.position;
        float t = 0f;
        while (t < 1f)
        {
            if (actor == null) yield break;
            t += Time.deltaTime / duration;
            actor.position = Vector3.Lerp(start, targetPos, t);
            yield return null;
        }
    }

    private IEnumerator LockAction(float duration)
    {
        _isActionLocked = true;
        yield return new WaitForSeconds(duration);
        _isActionLocked = false;
    }

    // --------------------------------------------------
    // �����ǦC�P�ĤH�j�M
    // --------------------------------------------------
    private IEnumerator AttackSequence(TeamSlotInfo attacker, TeamSlotInfo target, Vector3 targetPoint, bool perfect)
    {
        if (attacker == null || target == null) yield break;

        var actor = attacker.Actor.transform;
        Vector3 origin = actor.position;

        switch (attacker.ClassType)
        {
            case UnitClass.Mage:
                if (magicUseAuraPrefab != null)
                {
                    var aura = Instantiate(magicUseAuraPrefab, actor.position, Quaternion.identity);
                    if (vfxLifetime > 0f) Destroy(aura, vfxLifetime);
                }
                foreach (var enemy in EnemyTeamInfo)
                {
                    if (enemy?.Actor == null) continue;
                    var fireball = Instantiate(rangedVfxPrefab, actor.position, Quaternion.identity)
                        .GetComponent<FireBallSkill>();
                    if (fireball != null)
                    {
                        fireball.attacker = attacker;
                        fireball.target = enemy;
                        fireball.isPerfect = perfect;
                    }
                }
                break;

            default:
                Vector3 contact = targetPoint + meleeContactOffset;
                yield return Dash(actor, origin, contact, dashDuration);

                var vfx = Instantiate(meleeVfxPrefab, targetPoint, Quaternion.identity);
                var sword = vfx.GetComponent<SwordHitSkill>();
                if (sword != null)
                {
                    sword.attacker = attacker;
                    sword.target = target;
                    sword.isPerfect = perfect;
                }

                yield return new WaitForSeconds(dashStayDuration);
                yield return Dash(actor, contact, origin, dashDuration);
                break;
        }
    }

    private TeamSlotInfo FindEnemyByClass(UnitClass cls)
    {
        if (cls == UnitClass.Warrior)
            return EnemyTeamInfo.FirstOrDefault(e => e != null && e.Actor != null);

        if (cls == UnitClass.Mage)
            return EnemyTeamInfo.FirstOrDefault(e => e != null && e.Actor != null);

        if (cls == UnitClass.Ranger)
            return FindLastValidEnemy();

        return FindNextValidEnemy(0);
    }

    private TeamSlotInfo FindLastValidEnemy()
    {
        for (int i = EnemyTeamInfo.Length - 1; i >= 0; i--)
            if (EnemyTeamInfo[i]?.Actor != null)
                return EnemyTeamInfo[i];
        return null;
    }

    private TeamSlotInfo FindNextValidEnemy(int startIndex)
    {
        for (int i = startIndex; i < EnemyTeamInfo.Length; i++)
            if (EnemyTeamInfo[i]?.Actor != null)
                return EnemyTeamInfo[i];
        return null;
    }

    private IEnumerator Dash(Transform actor, Vector3 from, Vector3 to, float duration)
    {
        if (actor == null) yield break;
        float t = 0f;
        while (t < 1f)
        {
            if (actor == null) yield break;
            t += Time.deltaTime / duration;
            actor.position = Vector3.Lerp(from, to, t);
            yield return null;
        }
    }

    // --------------------------------------------------
    // �ĤH���`�P�e��
    // --------------------------------------------------
    public void OnEnemyDeath(int deadIndex)
    {
        if (deadIndex < 0 || deadIndex >= EnemyTeamInfo.Length)
            return;

        var deadSlot = EnemyTeamInfo[deadIndex];
        if (deadSlot?.Actor != null)
            Destroy(deadSlot.Actor);
        if (deadSlot != null)
            deadSlot.Actor = null;

        //ShiftEnemiesForward();
    }

    //private void ShiftEnemiesForward()
    //{
    //    StartCoroutine(ShiftEnemiesForwardRoutine());
    //}

    //private IEnumerator ShiftEnemiesForwardRoutine()
    //{
    //    bool moved = false;

    //    for (int i = 1; i < EnemyTeamInfo.Length; i++)
    //    {
    //        if ((EnemyTeamInfo[i - 1] == null || EnemyTeamInfo[i - 1].Actor == null) &&
    //            (EnemyTeamInfo[i]?.Actor != null))
    //        {
    //            var enemy = EnemyTeamInfo[i].Actor;
    //            if (enemy == null) continue;

    //            Vector3 targetPos = enemyPositions[i - 1].position;

    //            var enemyBase = enemy.GetComponent<EnemyBase>();
    //            if (enemyBase != null) enemyBase.SetForceMove(true);

    //            float t = 0f;
    //            Vector3 start = enemy.transform.position;
    //            float moveTime = 0.25f;
    //            while (t < 1f)
    //            {
    //                if (enemy == null) yield break;
    //                t += Time.deltaTime / moveTime;
    //                enemy.transform.position = Vector3.Lerp(start, targetPos, t);
    //                yield return null;
    //            }

    //            EnemyTeamInfo[i - 1] = EnemyTeamInfo[i];
    //            EnemyTeamInfo[i - 1].SlotTransform = enemyPositions[i - 1];
    //            EnemyTeamInfo[i] = null;

    //            if (enemyBase != null)
    //            {
    //                enemyBase.SetForceMove(false);
    //                enemyBase.SendMessage("RefreshBasePosition", SendMessageOptions.DontRequireReceiver);
    //            }

    //            moved = true;
    //            yield return new WaitForSeconds(0.05f);
    //        }
    //    }

    //    if (moved)
    //        Debug.Log("�ĤH�㶤�e������");
    //}
}
