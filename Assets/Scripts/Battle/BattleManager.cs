using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem; // 新 Input System

public class BattleManager : MonoBehaviour
{
    [System.Serializable]
    public enum UnitClass { Melee, Ranged }

    [System.Serializable]
    public class TeamSlotInfo
    {
        [Header("場上關聯")]
        public string UnitName;
        public GameObject Actor;           // 該格的角色物件
        public Transform SlotTransform;    // 該格定位點（PlayerTeam/PositionX 或 EnemyTeam/PositionX）
        public UnitClass ClassType = UnitClass.Melee;

        [Header("戰鬥數值（今日未用，先佔位）")]
        public int MaxHP = 100;
        public int HP = 100;
        public int MaxMP = 100;
        public int MP = 0;
        public int OriginAtk = 10;
        public int Atk = 10;
        public string SkillName = "Basic";
    }

    [Header("我方三格（左側）")]
    public TeamSlotInfo[] CTeamInfo = new TeamSlotInfo[3];

    [Header("敵方三格（右側）")]
    public TeamSlotInfo[] ETeamInfo = new TeamSlotInfo[3];

    [Header("輸入（用 InputActionReference 綁定）")]
    public InputActionReference actionAttackPos1; // Pos1
    public InputActionReference actionAttackPos2; // Pos2
    public InputActionReference actionAttackPos3; // Pos3
    public InputActionReference actionRotateTeam; // RotateTeam

    [Header("時序與運動參數")]
    public float actionLockDuration = 0.5f;
    public float dashDuration = 0.05f;
    public Vector3 meleeContactOffset = new Vector3(-1f, 0f, 0f);

    [Header("特效 Prefab（今日只做生成）")]
    public GameObject meleeVfxPrefab;
    public GameObject rangedVfxPrefab;
    public float vfxLifetime = 1.5f;

    


    private bool _isActionLocked;
    private readonly WaitForEndOfFrame _endOfFrame = new WaitForEndOfFrame();

    void OnEnable()
    {
        if (actionAttackPos1 != null)
        {
            actionAttackPos1.action.started -= OnAttackPos1; // 確保不重複綁
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
        Debug.Log("Character1Attack pressed (E) → 攻擊 Position1");
        TryStartAttack(0);
    }

    private void OnAttackPos2(InputAction.CallbackContext ctx)
    {
        Debug.Log("Character2Attack pressed (W) → 攻擊 Position2");
        TryStartAttack(1);
    }

    private void OnAttackPos3(InputAction.CallbackContext ctx)
    {
        Debug.Log("Character3Attack pressed (Q) → 攻擊 Position3");
        TryStartAttack(2);
    }

    private void OnRotateTeam(InputAction.CallbackContext ctx)
    {
        Debug.Log("Rotate Team (R/A) pressed");
        RotateTeam();
    }

    private void RotateTeam()
    {
        if (CTeamInfo.Length != 3) return;

        // 暫存最後一個
        var temp = CTeamInfo[2];

        // 位置移動
        CTeamInfo[2] = CTeamInfo[1];
        CTeamInfo[1] = CTeamInfo[0];
        CTeamInfo[0] = temp;

        // 更新角色位置（把 Actor 移到對應 SlotTransform）
        for (int i = 0; i < CTeamInfo.Length; i++)
        {
            if (CTeamInfo[i] != null && CTeamInfo[i].Actor != null && CTeamInfo[i].SlotTransform != null)
            {
                CTeamInfo[i].Actor.transform.position = CTeamInfo[i].SlotTransform.position;
            }
        }
    }


    // 嘗試從指定我方槽位發動攻擊（對位攻擊）
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
            actor.position = origin; // 瞬間回原位
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
