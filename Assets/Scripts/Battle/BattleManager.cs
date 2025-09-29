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
    [Header("�ڤ�T�w�y�С]�۰ʦb Start �O���^")]
    [SerializeField] private Transform[] playerPositions = new Transform[3];

    [Header("�Ĥ�T�w�y�С]�۰ʦb Start �O���^")]
    [SerializeField] private Transform[] enemyPositions = new Transform[3];

    [Header("�ڤ�T���ơ]�����^")]
    public TeamSlotInfo[] CTeamInfo = new TeamSlotInfo[3];

    [Header("�Ĥ�T���ơ]�k���^")]
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

    void OnEnable()
    {
        if (actionAttackPos1 != null)
        {
            actionAttackPos1.action.started -= OnAttackPos1; // �T�O�����Ƹj
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

        if (actionRotateTeam != null)
        {
            actionRotateTeam.action.performed -= OnRotateTeam;
            actionRotateTeam.action.performed += OnRotateTeam;
            actionRotateTeam.action.Enable();
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
        if (actionRotateTeam != null)
            actionRotateTeam.action.performed -= OnRotateTeam;
    }

    private void OnAttackPos1(InputAction.CallbackContext ctx)
    {
        Debug.Log("Character1Attack pressed (E) �� ���� Position1");
        TryStartAttack(0);
    }

    private void OnAttackPos2(InputAction.CallbackContext ctx)
    {
        Debug.Log("Character2Attack pressed (W) �� ���� Position2");
        TryStartAttack(1);
    }

    private void OnAttackPos3(InputAction.CallbackContext ctx)
    {
        Debug.Log("Character3Attack pressed (Q) �� ���� Position3");
        TryStartAttack(2);
    }

    private void OnRotateTeam(InputAction.CallbackContext ctx)
    {
        Debug.Log("Rotate Team (R/A) pressed");
        RotateTeam();
    }

    private void RotateTeam() //���ɰw
    {
        if (CTeamInfo.Length != 3) return;

        // ��ƥ洫�]���ɰw�G0->2, 1->0, 2->1�^
        var temp = CTeamInfo[0];
        CTeamInfo[0] = CTeamInfo[1];
        CTeamInfo[1] = CTeamInfo[2];
        CTeamInfo[2] = temp;

        // ���ʨ����T�w�y��
        for (int i = 0; i < 3; i++)
        {
            if (CTeamInfo[i].Actor != null)
            {
                Transform actor = CTeamInfo[i].Actor.transform;
                Vector3 targetPos = playerPositions[i].position;

                StartCoroutine(SmoothMove(actor, targetPos, 0.1f));
                actor.SetParent(playerPositions[i]); // �]���� Position ���l����
            }
        }

        Debug.Log("Team rotated clockwise");
    }


    private IEnumerator SmoothMove(Transform actor, Vector3 targetPos, float duration)
    {
        Vector3 startPos = actor.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            actor.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        actor.position = targetPos;
    }




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
