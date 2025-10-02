using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem; // 新 Input System

public class BattleManager : MonoBehaviour
{
    // 單例
    public static BattleManager Instance { get; private set; }

    [System.Serializable]
    public enum UnitClass
    {
        Warrior,  // 原本 Melee
        Mage,     // 原本 Ranged
        Shield,
        Priest,
        Ranger
    }

    [System.Serializable]
    public class TeamSlotInfo
    {
        [Header("場上關聯")]
        public string UnitName;
        public GameObject Actor;
        public Transform SlotTransform;
        public UnitClass ClassType = UnitClass.Warrior;

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
    public GameObject shieldStrikeVfxPrefab;
    public GameObject missVfxPrefab;   // ★ 新增：Miss 特效
    public float vfxLifetime = 1.5f;

    [Header("Shield 設定")]
    public float shieldBlockDuration = 2.0f;  // ★ Shield 格檔持續時間（秒數，可依 2 拍調整）
    public int shieldDamage = 10;             // ★ Shield 格檔時造成的傷害


    private bool _isActionLocked;
    private readonly WaitForEndOfFrame _endOfFrame = new WaitForEndOfFrame();

    [Header("血條 UI")]
    public GameObject healthBarPrefab;   // 指定血條 Prefab
    public Canvas uiCanvas;              // UI Canvas (Screen Space - Camera)



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
        // 建立我方血條
        CreateHealthBars(CTeamInfo);
        // 建立敵方血條
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
        // Q → P1
        TryStartAttack(2);
    }

    private void OnAttackPos2(InputAction.CallbackContext ctx)
    {
        // W → P2
        TryStartAttack(1);
    }

    private void OnAttackPos3(InputAction.CallbackContext ctx)
    {
        // E → P3
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

    // 嘗試從指定我方槽位發動攻擊（對位攻擊）
    private void TryStartAttack(int slotIndex)
    {
        if (_isActionLocked) return;
        if (slotIndex < 0 || slotIndex >= CTeamInfo.Length) return;

        var attacker = CTeamInfo[slotIndex];
        var target = ETeamInfo[slotIndex];

        // ★ 先做節奏判定
        bool perfect = BeatJudge.Instance.IsOnBeat();

        if (target == null || target.Actor == null)
        {
            Debug.Log($"英雄 {attacker.UnitName} 攻擊（沒有敵人） Perfect={perfect}");
            return;
        }

        var targetPoint = target.SlotTransform != null
            ? target.SlotTransform.position
            : GetFallbackEnemyPoint(slotIndex);

        // ★ 把判定結果傳進去
        StartCoroutine(AttackSequence(attacker, target, targetPoint, perfect));

        Debug.Log($"英雄 P{slotIndex + 1} 攻擊 → 敵人 E{slotIndex + 1} Perfect={perfect}");
    }


    private IEnumerator AttackSequence(TeamSlotInfo attacker, TeamSlotInfo target, Vector3 targetPoint, bool perfect)
    {
        _isActionLocked = true;
        float startTime = Time.time;

        var actor = attacker.Actor.transform;
        Vector3 origin = actor.position;

        if (attacker.ClassType == UnitClass.Warrior)
        {
            // Warrior → 近戰邏輯
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
            // Mage → 遠程邏輯
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
            // Shield → 格檔邏輯
            if (perfect)
            {
                Debug.Log("Shield Perfect 格檔：全隊獲得免傷狀態 + 生成反擊 Fireball");

                // ★ 給予全隊格檔 Buff
                BattleEffectManager.Instance.ActivateShield(shieldBlockDuration);

                // ★ 找到第一個可攻擊的敵人（優先 slotIndex，若空則往後找）
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
                        strike.overrideDamage = shieldDamage; // 固定 10 點傷害
                    }

                    Debug.Log($"Shield 反擊 Fireball → {shieldTarget.UnitName}，固定 {shieldDamage} 傷害");
                }
            }
            else
            {
                Debug.Log("Shield Miss，生成 Miss 特效");
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
    //    Debug.Log("全隊進入免傷狀態");
    //    bool isShielding = true;

    //    // 這裡你可以做 UI 特效 or 狀態標記
    //    // e.g., BattleEffectManager.Instance.SetInvincible(CTeamInfo, true);

    //    yield return new WaitForSeconds(shieldBlockDuration);

    //    // 結束免傷
    //    isShielding = false;
    //    Debug.Log("全隊免傷狀態結束");
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
