using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem; // 新 Input System

public class BattleManager : MonoBehaviour
{
    // 單例
    public static BattleManager Instance { get; private set; }

    [System.Serializable]
    public enum UnitClass { Melee, Ranged }

    [System.Serializable]
    public class TeamSlotInfo
    {
        [Header("場上關聯")]
        public string UnitName;
        public GameObject Actor;
        public Transform SlotTransform;
        public UnitClass ClassType = UnitClass.Melee;

        [Header("戰鬥數值")]
        public int MaxHP = 100;
        public int HP = 100;
        public int MaxMP = 100;
        public int MP = 0;
        public int OriginAtk = 10;
        public int Atk = 10;
        public string SkillName = "Basic";

        [Header("輸入綁定")]
        public int AssignedKeyIndex; // 0=E, 1=W, 2=Q
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

    // ---------------- Singleton 設定 ----------------
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // 保證只有一個存在
            return;
        }
        Instance = this;
        //DontDestroyOnLoad(gameObject); // 切場景也不會被刪除
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
        //// 開場固定：P1=E(0), P2=W(1), P3=Q(2)
        //for (int i = 0; i < CTeamInfo.Length; i++)
        //{
        //    if (CTeamInfo[i] != null)
        //        CTeamInfo[i].AssignedKeyIndex = i;
        //}
    }

    private void OnAttackPos1(InputAction.CallbackContext ctx)
    {
        // Q 對應 P1
        TryStartAttack(0);
    }

    private void OnAttackPos2(InputAction.CallbackContext ctx)
    {
        // W 對應 P2
        TryStartAttack(1);
    }

    private void OnAttackPos3(InputAction.CallbackContext ctx)
    {
        // E 對應 P3
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

    // 嘗試從指定我方槽位發動攻擊（對位攻擊）
    private void TryStartAttack(int slotIndex)
    {
        if (_isActionLocked) return;
        if (slotIndex < 0 || slotIndex >= CTeamInfo.Length) return;

        var attacker = CTeamInfo[slotIndex];

        // P1 對 E1, P2 對 E2, P3 對 E3
        int enemyIndex = (ETeamInfo.Length - 1) - slotIndex;
        var target = ETeamInfo[enemyIndex];

        if (attacker == null || attacker.Actor == null || attacker.SlotTransform == null) return;

        var targetPoint = (target != null && target.SlotTransform != null)
            ? target.SlotTransform.position
            : GetFallbackEnemyPoint(slotIndex);

        StartCoroutine(AttackSequence(attacker, target, targetPoint));

        Debug.Log($"英雄 P{slotIndex + 1} 攻擊 → 敵人 E{ETeamInfo.Length - enemyIndex}");
    }


    private IEnumerator AttackSequence(BattleManager.TeamSlotInfo attacker, BattleManager.TeamSlotInfo target, Vector3 targetPoint)
    {
        _isActionLocked = true;
        float startTime = Time.time;

        var actor = attacker.Actor.transform;
        Vector3 origin = actor.position;

        if (attacker.ClassType == UnitClass.Melee)
        {
            // 計算近戰接觸點（敵人位置前方一點）
            Vector3 contactPoint = targetPoint + meleeContactOffset;

            // Dash 往前
            yield return Dash(actor, origin, contactPoint, dashDuration);

            // 在敵人位置生成近戰技能
            var skill = Instantiate(meleeVfxPrefab, targetPoint, Quaternion.identity);
            var sword = skill.GetComponent<SwordHitSkill>();
            if (sword != null)
            {
                sword.attacker = attacker;
                sword.target = target;
            }

            // 等一幀（確保碰撞檢測有作用）
            yield return _endOfFrame;

            // 回原位
            actor.position = origin;
        }
        else // Ranged
        {
            // 遠程：生成技能Prefab在玩家位置，自己移動到敵人
            var skill = Instantiate(rangedVfxPrefab, actor.position, Quaternion.identity);
            var fireball = skill.GetComponent<FireBallSkill>();
            if (fireball != null)
            {
                fireball.attacker = attacker;
                fireball.target = target;
            }
        }

        // 鎖定動作時間
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
