using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem; // �s Input System


public class BattleManager : MonoBehaviour
{
    [System.Serializable]
    public enum UnitClass { Melee, Ranged }

    [System.Serializable]
    public class TeamSlotInfo
    {
        [Header("���W���p")]
        public string UnitName;
        public GameObject Actor;           // �Ӯ檺���⪫��
        public Transform SlotTransform;    // �Ӯ�w���I�]PlayerTeam/PositionX �� EnemyTeam/PositionX�^
        public UnitClass ClassType = UnitClass.Melee;

        [Header("�԰��ƭȡ]���饼�ΡA������^")]
        public int MaxHP = 100;
        public int HP = 100;
        public int MaxMP = 100;
        public int MP = 0;
        public int OriginAtk = 10;
        public int Atk = 10;
        public string SkillName = "Basic";
    }

    [Header("�ڤ�T��]�����^")]
    public TeamSlotInfo[] CTeamInfo = new TeamSlotInfo[3];

    [Header("�Ĥ�T��]�k���^")]
    public TeamSlotInfo[] ETeamInfo = new TeamSlotInfo[3];

    [Header("��J�]�� InputActionReference �j�w�^")]
    [Tooltip("���� Position1 ���������ĳ�j Gamepad East(B)�F�Y�A����L�A��ĳ�j E")]
    public InputActionReference actionAttackPos1; // Pos1�]��� B/East�^
    [Tooltip("���� Position2 ���������ĳ�j Gamepad North(Y)�F��L��ĳ W")]
    public InputActionReference actionAttackPos2; // Pos2�]��� Y/North�^
    [Tooltip("���� Position3 ���������ĳ�j Gamepad West(X)�F��L��ĳ Q")]
    public InputActionReference actionAttackPos3; // Pos3�]��� X/West�^
    // ����Y�n�[�J����A�i�A�[ InputActionReference

    [Header("�ɧǻP�B�ʰѼ�")]
    [Tooltip("�����ʧ@��ɪ��]�@���u���\�@����������^")]
    public float actionLockDuration = 0.5f;    // �A�쥻�� AttackGap
    [Tooltip("��ԽĨ�ɶ�")]
    public float dashDuration = 0.05f;
    [Tooltip("��ԩR���S�Ħ�m = �Ĥ�P�C�y�� + ������")]
    public Vector3 meleeContactOffset = new Vector3(-1f, 0f, 0f);

    [Header("�S�� Prefab�]����u���ͦ��^")]
    public GameObject meleeVfxPrefab;
    public GameObject rangedVfxPrefab;
    [Tooltip("�ͦ����S�ĴX���۰ʾP��")]
    public float vfxLifetime = 1.5f;

    // �������A
    private bool _isActionLocked;
    private readonly WaitForEndOfFrame _endOfFrame = new WaitForEndOfFrame();

    void OnEnable()
    {
        // �ҥΨõ��U�ƥ�
        EnableAndBind(actionAttackPos1, OnAttackPos1);
        EnableAndBind(actionAttackPos2, OnAttackPos2);
        EnableAndBind(actionAttackPos3, OnAttackPos3);
    }

    void OnDisable()
    {
        // �Ѱ��ƥ�ð���
        UnbindAndDisable(actionAttackPos1, OnAttackPos1);
        UnbindAndDisable(actionAttackPos2, OnAttackPos2);
        UnbindAndDisable(actionAttackPos3, OnAttackPos3);
    }

    private void EnableAndBind(InputActionReference aref, System.Action<InputAction.CallbackContext> handler)
    {
        if (aref == null) return;
        var a = aref.action;
        if (a == null) return;
        a.performed += ctx => handler(ctx);
        if (!a.enabled) a.Enable();
    }

    private void UnbindAndDisable(InputActionReference aref, System.Action<InputAction.CallbackContext> handler)
    {
        if (aref == null) return;
        var a = aref.action;
        if (a == null) return;
        a.performed -= ctx => handler(ctx); // �o��L�k�Ѱ��ΦW�e���A��ΤU���㦡��k
    }

    // �]���W��ΦW�j�w�L�k�Ѱ��A�����㦡�j�w����
    private void EnableAndBind(InputActionReference aref, System.Action handler)
    {
        if (aref == null) return;
        var a = aref.action;
        if (a == null) return;
        a.performed += _ => handler();
        if (!a.enabled) a.Enable();
    }

    private void UnbindAndDisable(InputActionReference aref, System.Action handler)
    {
        if (aref == null) return;
        var a = aref.action;
        if (a == null) return;
        a.performed -= _ => handler();
        // ���j�� Disable�A�קK�P��L�t�νĬ�F�p�������i�b�� a.Disable();
    }

    // ���s���㦡��k�j�w�A�קK�ΦW�ƥ󲾰����D
    void Reset()
    {
        // �ȥΩ�s�边 Reset�A������
    }

    void Awake()
    {
        // ���s�H�㦡�e���j�w�]�л\�W�誩���^
        OnDisable();
        EnableAndBind(actionAttackPos1, OnAttackPos1_Impl);
        EnableAndBind(actionAttackPos2, OnAttackPos2_Impl);
        EnableAndBind(actionAttackPos3, OnAttackPos3_Impl);
    }

    void OnDestroy()
    {
        UnbindAndDisable(actionAttackPos1, OnAttackPos1_Impl);
        UnbindAndDisable(actionAttackPos2, OnAttackPos2_Impl);
        UnbindAndDisable(actionAttackPos3, OnAttackPos3_Impl);
    }

    // �㦡�B�z�禡
    private void OnAttackPos1_Impl() => TryStartAttack(0);
    private void OnAttackPos2_Impl() => TryStartAttack(1);
    private void OnAttackPos3_Impl() => TryStartAttack(2);

    // �¤����O�d�]�Y�A����n���^�ΦW�e���^
    private void OnAttackPos1(InputAction.CallbackContext ctx) => TryStartAttack(0);
    private void OnAttackPos2(InputAction.CallbackContext ctx) => TryStartAttack(1);
    private void OnAttackPos3(InputAction.CallbackContext ctx) => TryStartAttack(2);

    // ���ձq���w�ڤ�Ѧ�o�ʧ����]�������^
    private void TryStartAttack(int slotIndex)
    {
        if (_isActionLocked) return;
        if (slotIndex < 0 || slotIndex >= 3) return;

        var attacker = CTeamInfo[slotIndex];
        var target = ETeamInfo[slotIndex];

        if (attacker == null || attacker.Actor == null || attacker.SlotTransform == null) return;

        var targetPoint = (target != null && target.SlotTransform != null)
            ? target.SlotTransform.position
            : GetFallbackEnemyPoint(slotIndex);

        StartCoroutine(AttackSequence(attacker, targetPoint));
    }

    // �����ǦC�]�������ˮ`�^
    private IEnumerator AttackSequence(TeamSlotInfo attacker, Vector3 targetPoint)
    {
        _isActionLocked = true;
        float startTime = Time.time;

        var actor = attacker.Actor.transform;
        Vector3 origin = actor.position;

        if (attacker.ClassType == UnitClass.Melee)
        {
            Vector3 contactPoint = targetPoint + meleeContactOffset;
            yield return Dash(actor, origin, contactPoint, dashDuration);
            SpawnVfx(meleeVfxPrefab, targetPoint);
            actor.position = origin; // �����^���
            yield return _endOfFrame;
        }
        else // Ranged
        {
            yield return new WaitForSeconds(dashDuration);
            SpawnVfx(rangedVfxPrefab, targetPoint);
        }

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

    private void SpawnVfx(GameObject prefab, Vector3 atWorldPos)
    {
        if (prefab == null) return;
        var go = Instantiate(prefab, atWorldPos, Quaternion.identity);
        if (vfxLifetime > 0f) Destroy(go, vfxLifetime);
    }

    private Vector3 GetFallbackEnemyPoint(int slotIndex)
    {
        var self = CTeamInfo[slotIndex];
        if (self != null && self.SlotTransform != null)
            return self.SlotTransform.position + new Vector3(3f, 0f, 0f);
        return transform.position;
    }
}

