using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem; // �s Input System

public class BattleManager : MonoBehaviour
{
    // ���
    public static BattleManager Instance { get; private set; }

    [System.Serializable]
    public enum UnitClass { Melee, Ranged }

    [System.Serializable]
    public class TeamSlotInfo
    {
        [Header("���W���p")]
        public string UnitName;
        public GameObject Actor;
        public Transform SlotTransform;
        public UnitClass ClassType = UnitClass.Melee;

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
    public float vfxLifetime = 1.5f;

    private bool _isActionLocked;
    private readonly WaitForEndOfFrame _endOfFrame = new WaitForEndOfFrame();

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
        //// �}���T�w�GP1=E(0), P2=W(1), P3=Q(2)
        //for (int i = 0; i < CTeamInfo.Length; i++)
        //{
        //    if (CTeamInfo[i] != null)
        //        CTeamInfo[i].AssignedKeyIndex = i;
        //}
    }

    private void OnAttackPos1(InputAction.CallbackContext ctx)
    {
        // Q ���� P1
        TryStartAttack(0);
    }

    private void OnAttackPos2(InputAction.CallbackContext ctx)
    {
        // W ���� P2
        TryStartAttack(1);
    }

    private void OnAttackPos3(InputAction.CallbackContext ctx)
    {
        // E ���� P3
        TryStartAttack(2);
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

        // P1 �� E1, P2 �� E2, P3 �� E3
        int enemyIndex = (ETeamInfo.Length - 1) - slotIndex;
        var target = ETeamInfo[enemyIndex];

        if (attacker == null || attacker.Actor == null || attacker.SlotTransform == null) return;

        var targetPoint = (target != null && target.SlotTransform != null)
            ? target.SlotTransform.position
            : GetFallbackEnemyPoint(slotIndex);

        StartCoroutine(AttackSequence(attacker, target, targetPoint));

        Debug.Log($"�^�� P{slotIndex + 1} ���� �� �ĤH E{ETeamInfo.Length - enemyIndex}");
    }


    private IEnumerator AttackSequence(BattleManager.TeamSlotInfo attacker, BattleManager.TeamSlotInfo target, Vector3 targetPoint)
    {
        _isActionLocked = true;
        float startTime = Time.time;

        var actor = attacker.Actor.transform;
        Vector3 origin = actor.position;

        if (attacker.ClassType == UnitClass.Melee)
        {
            // �p���Ա�Ĳ�I�]�ĤH��m�e��@�I�^
            Vector3 contactPoint = targetPoint + meleeContactOffset;

            // Dash ���e
            yield return Dash(actor, origin, contactPoint, dashDuration);

            // �b�ĤH��m�ͦ���ԧޯ�
            var skill = Instantiate(meleeVfxPrefab, targetPoint, Quaternion.identity);
            var sword = skill.GetComponent<SwordHitSkill>();
            if (sword != null)
            {
                sword.attacker = attacker;
                sword.target = target;
            }

            // ���@�V�]�T�O�I���˴����@�Ρ^
            yield return _endOfFrame;

            // �^���
            actor.position = origin;
        }
        else // Ranged
        {
            // ���{�G�ͦ��ޯ�Prefab�b���a��m�A�ۤv���ʨ�ĤH
            var skill = Instantiate(rangedVfxPrefab, actor.position, Quaternion.identity);
            var fireball = skill.GetComponent<FireBallSkill>();
            if (fireball != null)
            {
                fireball.attacker = attacker;
                fireball.target = target;
            }
        }

        // ��w�ʧ@�ɶ�
        float remain = actionLockDuration - (Time.time - startTime);
        if (remain > 0f) yield return new WaitForSeconds(remain);

        _isActionLocked = false;
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
