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
    [Tooltip("對應 Position1 的攻擊鍵建議綁 Gamepad East(B)；若你用鍵盤，建議綁 E")]
    public InputActionReference actionAttackPos1; // Pos1（原先 B/East）
    [Tooltip("對應 Position2 的攻擊鍵建議綁 Gamepad North(Y)；鍵盤建議 W")]
    public InputActionReference actionAttackPos2; // Pos2（原先 Y/North）
    [Tooltip("對應 Position3 的攻擊鍵建議綁 Gamepad West(X)；鍵盤建議 Q")]
    public InputActionReference actionAttackPos3; // Pos3（原先 X/West）
    // 之後若要加入換位，可再加 InputActionReference

    [Header("時序與運動參數")]
    [Tooltip("全隊動作鎖時長（一次只允許一隻角色攻擊）")]
    public float actionLockDuration = 0.5f;    // 你原本的 AttackGap
    [Tooltip("近戰衝刺時間")]
    public float dashDuration = 0.05f;
    [Tooltip("近戰命中特效位置 = 敵方同列座標 + 此偏移")]
    public Vector3 meleeContactOffset = new Vector3(-1f, 0f, 0f);

    [Header("特效 Prefab（今日只做生成）")]
    public GameObject meleeVfxPrefab;
    public GameObject rangedVfxPrefab;
    [Tooltip("生成的特效幾秒後自動銷毀")]
    public float vfxLifetime = 1.5f;

    // 內部狀態
    private bool _isActionLocked;
    private readonly WaitForEndOfFrame _endOfFrame = new WaitForEndOfFrame();

    void OnEnable()
    {
        // 啟用並註冊事件
        EnableAndBind(actionAttackPos1, OnAttackPos1);
        EnableAndBind(actionAttackPos2, OnAttackPos2);
        EnableAndBind(actionAttackPos3, OnAttackPos3);
    }

    void OnDisable()
    {
        // 解除事件並停用
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
        a.performed -= ctx => handler(ctx); // 這行無法解除匿名委派，改用下方顯式方法
    }

    // 因為上方匿名綁定無法解除，提供顯式綁定版本
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
        // 不強制 Disable，避免與其他系統衝突；如需關閉可在此 a.Disable();
    }

    // 重新用顯式方法綁定，避免匿名事件移除問題
    void Reset()
    {
        // 僅用於編輯器 Reset，不做事
    }

    void Awake()
    {
        // 重新以顯式委派綁定（覆蓋上方版本）
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

    // 顯式處理函式
    private void OnAttackPos1_Impl() => TryStartAttack(0);
    private void OnAttackPos2_Impl() => TryStartAttack(1);
    private void OnAttackPos3_Impl() => TryStartAttack(2);

    // 舊介面保留（若你之後要切回匿名委派）
    private void OnAttackPos1(InputAction.CallbackContext ctx) => TryStartAttack(0);
    private void OnAttackPos2(InputAction.CallbackContext ctx) => TryStartAttack(1);
    private void OnAttackPos3(InputAction.CallbackContext ctx) => TryStartAttack(2);

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

    // 攻擊序列（先不做傷害）
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

