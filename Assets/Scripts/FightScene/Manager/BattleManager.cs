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
        Bard,
        Ranger,
        Paladin,
        Enemy
    }

    [System.Serializable]
    public class TeamSlotInfo
    {
        [Header("Prefab 設定")]
        public GameObject PrefabToSpawn;

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

        [Header("技能")]
        public string[] SkillNames;
        public GameObject[] SkillPrefabs;

        [Header("普通攻擊")]
        public string[] NormalAttackNames;
        public GameObject[] NormalAttackPrefabs;

        [Header("輸入綁定")]
        public int AssignedKeyIndex;
    }

    [Header("我方固定座標（右側）")]
    [SerializeField] private Transform[] playerPositions = new Transform[3];

    [Header("敵方固定座標（左側）")]
    [SerializeField] private Transform[] enemyPositions = new Transform[3];

    [Header("我方三格資料")]
    public TeamSlotInfo[] CTeamInfo = new TeamSlotInfo[3];

    [Header("敵方三格資料")]
    public TeamSlotInfo[] EnemyTeamInfo = new TeamSlotInfo[3];  // ★ 改名避免 enum 衝突

    [Header("輸入（新 Input System）")]
    public InputActionReference actionAttackP1;
    public InputActionReference actionAttackP2;
    public InputActionReference actionAttackP3;
    public InputActionReference actionRotateLeft;
    public InputActionReference actionRotateRight;
    public InputActionReference actionBlockP1;
    public InputActionReference actionBlockP2;
    public InputActionReference actionBlockP3;
    [Header("Fever 大招輸入")]
    public InputActionReference actionFeverUltimate;  // 新增輸入引用 (在 Inspector 綁定)
    private System.Action<InputAction.CallbackContext> feverUltHandler;

    [Header("時序與運動參數")]
    public float actionLockDuration = 0.5f;
    public float dashDuration = 0.05f;
    public float dashStayDuration = 0.15f;
    public float rotateMoveDuration = 0.2f;
    public Vector3 meleeContactOffset = new Vector3(-1f, 0f, 0f);

    [Header("特效 Prefab")]
    public GameObject meleeVfxPrefab;
    public GameObject rangedVfxPrefab;
    public GameObject shieldStrikeVfxPrefab;
    public GameObject missVfxPrefab;
    public GameObject magicUseAuraPrefab;
    public float vfxLifetime = 1.5f;

    [Header("Shield 設定")]
    public float shieldBlockDuration = 2.0f;
    public int shieldDamage = 10;

    [Header("血條 UI")]
    public GameObject healthBarPrefab;
    public Canvas uiCanvas;

    private bool _isActionLocked;
    private bool _isBlockingActive = false;
    private GameObject lastSuccessfulAttacker = null;

    // 用於安全解除 Input 綁定
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

        // 確保陣列初始化
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
        feverUltHandler = ctx => OnFeverUltimate();
        

        if (actionAttackP1 != null) { actionAttackP1.action.started += attackP1Handler; actionAttackP1.action.Enable(); }
        if (actionAttackP2 != null) { actionAttackP2.action.started += attackP2Handler; actionAttackP2.action.Enable(); }
        if (actionAttackP3 != null) { actionAttackP3.action.started += attackP3Handler; actionAttackP3.action.Enable(); }
        if (actionBlockP1 != null) { actionBlockP1.action.started += blockP1Handler; actionBlockP1.action.Enable(); }
        if (actionBlockP2 != null) { actionBlockP2.action.started += blockP2Handler; actionBlockP2.action.Enable(); }
        if (actionBlockP3 != null) { actionBlockP3.action.started += blockP3Handler; actionBlockP3.action.Enable(); }
        if (actionFeverUltimate != null) { actionFeverUltimate.action.started += feverUltHandler; actionFeverUltimate.action.Enable(); }
    }

    private void OnDisable()
    {
        if (actionAttackP1 != null) actionAttackP1.action.started -= attackP1Handler;
        if (actionAttackP2 != null) actionAttackP2.action.started -= attackP2Handler;
        if (actionAttackP3 != null) actionAttackP3.action.started -= attackP3Handler;
        if (actionBlockP1 != null) actionBlockP1.action.started -= blockP1Handler;
        if (actionBlockP2 != null) actionBlockP2.action.started -= blockP2Handler;
        if (actionBlockP3 != null) actionBlockP3.action.started -= blockP3Handler;
        if (actionFeverUltimate != null) { actionFeverUltimate.action.started -= feverUltHandler;}
    }

    // --------------------------------------------------
    // 隊伍資料載入
    // --------------------------------------------------
    public void LoadTeamData(BattleTeamManager teamMgr)
    {
        if (teamMgr == null) return;

        CTeamInfo = teamMgr.CTeamInfo.ToArray();        // 深拷貝避免共用參考
        EnemyTeamInfo = teamMgr.EnemyTeamInfo.ToArray();

        Debug.Log("載入隊伍成功，玩家角色：" +
            string.Join(", ", CTeamInfo.Where(x => x != null).Select(x => x.UnitName)));
    }

    // --------------------------------------------------
    // Fever 大招輸入邏輯（已整合拍點判定）
    // --------------------------------------------------
    private void OnFeverUltimate()
    {
        if (FeverManager.Instance == null) return;

        // 檢查是否在拍上（Perfect）
        bool perfect = BeatJudge.Instance.IsOnBeat();
        if (!perfect)
        {
            Debug.Log("[BattleManager] Fever大招 Miss，未在節拍上，不觸發。");
            return;
        }

        // 檢查是否滿值
        if (FeverManager.Instance.currentFever < FeverManager.Instance.feverMax)
        {
            Debug.Log("[BattleManager] Fever未滿，無法啟動大招。");
            return;
        }

        Debug.Log("[BattleManager] 對拍成功且Fever滿值，啟動全隊大招！");
        StartCoroutine(FeverManager.Instance.HandleFeverUltimateSequence());
    }


    // --------------------------------------------------
    // 攻擊邏輯
    // --------------------------------------------------
    private void OnAttackKey(int index)
    {
        if (_isActionLocked) return;
        if (index < 0 || index >= CTeamInfo.Length) return;
        if (CTeamInfo[index] == null) return;

        var attacker = CTeamInfo[index];

        if (attacker == null || attacker.Actor == null) return;
        if (attacker.HP <= 0 || attacker.Actor == null)
        {
            Debug.Log($"[{attacker.UnitName}] 已死亡，僅打節拍但不觸發攻擊。");
            return;
        }

        bool perfect = BeatJudge.Instance.IsOnBeat();
        if (!perfect)
        {
            Debug.Log("Miss！未在節拍上，不觸發攻擊。");
            return;
        }

        var target = FindEnemyByClass(attacker.ClassType);
        int beatInCycle = BeatManager.Instance.predictedNextBeat;

        // ------------------------------------------------------------
        // 特例處理：即使沒有敵人也能發動的技能
        // ------------------------------------------------------------
        if (target == null)
        {
            // 法師普攻：沒敵人仍可充能
            if (attacker.ClassType == UnitClass.Mage && beatInCycle != BeatManager.Instance.beatsPerMeasure)
            {
                Debug.Log("[特例] 法師普攻在無敵人時仍可充能");
                StartCoroutine(HandleMageAttack(attacker, null, beatInCycle, perfect));
                return;
            }

            // 吟遊詩人重攻擊：沒敵人仍可治癒全隊
            if (attacker.ClassType == UnitClass.Bard && beatInCycle == BeatManager.Instance.beatsPerMeasure)
            {
                Debug.Log("[特例] 吟遊詩人重攻擊在無敵人時仍可施放治癒");
                StartCoroutine(HandleBardAttack(attacker, null, beatInCycle, perfect));
                return;
            }

            // 其他角色若沒目標就不攻擊
            return;
        }

        // ------------------------------------------------------------
        // 正常攻擊流程
        // ------------------------------------------------------------
        lastSuccessfulAttacker = attacker.Actor;
        StartCoroutine(LockAction(actionLockDuration));

        if (attacker.ClassType == UnitClass.Warrior)
            StartCoroutine(HandleWarriorAttack(attacker, target, beatInCycle, perfect));
        else if (attacker.ClassType == UnitClass.Mage)
            StartCoroutine(HandleMageAttack(attacker, target, beatInCycle, perfect));
        else if (attacker.ClassType == UnitClass.Ranger)
            StartCoroutine(HandleRangerAttack(attacker, beatInCycle, perfect));
        else if (attacker.ClassType == UnitClass.Bard)
            StartCoroutine(HandleBardAttack(attacker, target, beatInCycle, perfect));
        else if (attacker.ClassType == UnitClass.Paladin)
            StartCoroutine(HandlePaladinAttack(attacker, target, beatInCycle, perfect));
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

        // ★ 新邏輯：只要是第四拍就觸發重攻擊
        if (beatInCycle == BeatManager.Instance.beatsPerMeasure)
        {
            chosenSkill = charData.HeavyAttack;
            attackPrefab = chosenSkill?.SkillPrefab;
            Debug.Log($"[戰士重攻擊] 第 {beatInCycle} 拍觸發重攻擊！");
        }
        else
        {
            // 依拍數循環普通攻擊（例如第1~3拍用一般攻擊循環）
            int phase = ((beatInCycle - 1) % 3);
            chosenSkill = charData.NormalAttacks[phase];
            attackPrefab = chosenSkill?.SkillPrefab;
            Debug.Log($"[戰士普攻] 第 {beatInCycle} 拍，使用第 {phase + 1} 階普攻。");
        }

        if (attackPrefab == null && meleeVfxPrefab != null)
            attackPrefab = meleeVfxPrefab;

        // 前進揮擊
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

        int chargeStacks = BattleEffectManager.Instance.GetChargeStacks(attacker);

        // === 第四拍：重攻擊 ===
        if (beatInCycle == BeatManager.Instance.beatsPerMeasure)
        {
            if (chargeStacks <= 0)
            {
                Debug.Log("[法師重攻擊] 無充能層，攻擊無效。");
                yield break;
            }

            Debug.Log($"[法師重攻擊] 消耗 {chargeStacks} 層充電。");

            // 若仍有敵人，正常生成重攻擊特效
            if (target != null && charData.HeavyAttack?.SkillPrefab != null)
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

                // 計算傷害
                int damage = chargeStacks * 30;
                target.HP -= damage;
                if (target.HP < 0) target.HP = 0;

                var hb = target.Actor?.GetComponentInChildren<HealthBarUI>();
                if (hb != null) hb.ForceUpdate();
            }
            else
            {
                Debug.Log("[法師重攻擊] 無敵人存在，僅釋放能量特效。");
            }

            // 清除充能層
            BattleEffectManager.Instance.ResetChargeStacks(attacker);
            yield break;
        }

        // === 普通攻擊：無論有無敵人都能充能 ===
        Debug.Log($"[法師普攻] 第 {beatInCycle} 拍充能 +1 層。");

        // 生成亮光特效
        if (charData.NormalAttacks != null && charData.NormalAttacks.Count > 0)
        {
            var chargeEffect = charData.NormalAttacks[0].SkillPrefab;
            if (chargeEffect != null)
            {
                Vector3 spawnPos = actor.position;
                Instantiate(chargeEffect, spawnPos, Quaternion.identity);
            }
        }

        BattleEffectManager.Instance.AddChargeStack(attacker);
        yield return null;
    }

    // --------------------------------------------------
    // Ranger 攻擊邏輯
    // --------------------------------------------------
    private IEnumerator HandleRangerAttack(TeamSlotInfo attacker, int beatInCycle, bool perfect)
    {
        var actor = attacker.Actor.transform;
        var charData = attacker.Actor.GetComponent<CharacterData>();
        if (charData == null) yield break;

        // 取得攻擊Prefab（弓箭或投射物）
        GameObject arrowPrefab = null;
        if (charData.NormalAttacks != null && charData.NormalAttacks.Count > 0)
            arrowPrefab = charData.NormalAttacks[0].SkillPrefab;

        if (arrowPrefab == null)
        {
            Debug.LogWarning($"[Ranger] {attacker.UnitName} 沒有設定攻擊 Prefab。");
            yield break;
        }

        // 判定攻擊目標
        BattleManager.TeamSlotInfo target = null;
        bool isHeavy = false;
        if (beatInCycle == BeatManager.Instance.beatsPerMeasure)
        {
            target = FindLastValidEnemy();
            isHeavy = true;
            Debug.Log($"[弓箭手重攻擊] {attacker.UnitName} 攻擊最後一位敵人 {target?.UnitName}");
        }
        else
        {
            target = FindNextValidEnemy(0);
            Debug.Log($"[弓箭手普攻] {attacker.UnitName} 攻擊首位敵人 {target?.UnitName}");
        }

        if (target == null || target.Actor == null)
        {
            Debug.Log("[Ranger] 找不到目標，取消攻擊。");
            yield break;
        }

        // **調整生成位置：右上偏移**
        Vector3 spawnOffset = new Vector3(0.8f, 2f, 0f);
        Vector3 spawnPos = actor.position + spawnOffset;

        // 生成投射物（FireBallSkill）
        GameObject proj = Instantiate(arrowPrefab, spawnPos, Quaternion.identity);
        FireBallSkill skill = proj.GetComponent<FireBallSkill>();
        if (skill != null)
        {
            skill.attacker = attacker;
            skill.target = target;
            skill.isPerfect = perfect;
            skill.isHeavyAttack = isHeavy;
            skill.damage = isHeavy ? attacker.Atk * 2 : attacker.Atk;
        }

        Debug.Log($"[Ranger Attack] {attacker.UnitName} → {target.UnitName} ({(isHeavy ? "重攻擊" : "普攻")}) 傷害 {skill.damage}");
        yield return null;
    }

    // --------------------------------------------------
    // Bard 攻擊邏輯
    // --------------------------------------------------
    private IEnumerator HandleBardAttack(TeamSlotInfo attacker, TeamSlotInfo target, int beatInCycle, bool perfect)
    {
        var actor = attacker.Actor.transform;
        var charData = attacker.Actor.GetComponent<CharacterData>();
        if (charData == null) yield break;

        // **普通攻擊：需要敵人存在**
        if (beatInCycle != BeatManager.Instance.beatsPerMeasure)
        {
            if (target == null) yield break; // 沒敵人就不揮擊
            Vector3 origin = actor.position;
            Vector3 targetPoint = target.SlotTransform.position + meleeContactOffset;

            // Dash 前進
            yield return Dash(actor, origin, targetPoint, dashDuration);

            // 生成普攻特效
            GameObject attackPrefab = null;
            if (charData.NormalAttacks != null && charData.NormalAttacks.Count > 0)
                attackPrefab = charData.NormalAttacks[0].SkillPrefab;
            if (attackPrefab == null) attackPrefab = meleeVfxPrefab;

            if (attackPrefab != null)
            {
                var skillObj = Instantiate(attackPrefab, targetPoint, Quaternion.identity);
                var sword = skillObj.GetComponent<SwordHitSkill>();
                if (sword != null)
                {
                    sword.attacker = attacker;
                    sword.target = target;
                    sword.isPerfect = perfect;
                    sword.isHeavyAttack = false;
                }
            }

            yield return new WaitForSeconds(dashStayDuration);
            yield return Dash(actor, targetPoint, origin, dashDuration);
            yield break;
        }

        // **重攻擊：全隊回復血量 +10**
        Debug.Log($"[吟遊詩人重攻擊] {attacker.UnitName} 演奏治癒之歌，全隊回復10HP！");
        BattleEffectManager.Instance.HealTeamWithEffect(10);

        yield return null;
    }

    // --------------------------------------------------
    // Paladin 攻擊邏輯（輕攻擊 + 重攻擊）
    // --------------------------------------------------
    private IEnumerator HandlePaladinAttack(TeamSlotInfo attacker, TeamSlotInfo target, int beatInCycle, bool perfect)
    {
        var actor = attacker.Actor.transform;
        var charData = attacker.Actor.GetComponent<CharacterData>();
        if (charData == null) yield break;

        // === 第四拍：重攻擊 ===
        if (beatInCycle == BeatManager.Instance.beatsPerMeasure)
        {
            Debug.Log($"[聖騎士重攻擊] {attacker.UnitName} 嘲諷全體敵人！");
            if (charData.HeavyAttack?.SkillPrefab != null)
            {
                foreach (var enemy in EnemyTeamInfo)
                {
                    if (enemy == null || enemy.Actor == null) continue;

                    // 在敵方位置生成 Paladin 的重攻擊特效
                    Instantiate(charData.HeavyAttack.SkillPrefab, enemy.SlotTransform.position, Quaternion.identity);

                    // 套用嘲諷效果（假設 ApplyTaunt 仍為有效方法）
                    BattleEffectManager.Instance.ApplyTaunt(enemy.Actor, attacker.Actor, 16);
                }
            }
            yield break;
        }

        // === 普通攻擊（不附帶嘲諷） ===
        if (attacker == null || target == null) yield break;
        Vector3 origin = actor.position;
        Vector3 targetPoint = target.SlotTransform.position + meleeContactOffset;

        // Dash 前進
        yield return Dash(actor, origin, targetPoint, dashDuration);

        // 普攻特效
        GameObject attackPrefab = null;
        if (charData.NormalAttacks != null && charData.NormalAttacks.Count > 0)
            attackPrefab = charData.NormalAttacks[0].SkillPrefab;
        if (attackPrefab == null) attackPrefab = meleeVfxPrefab;

        if (attackPrefab != null)
        {
            var vfx = Instantiate(attackPrefab, targetPoint, Quaternion.identity);
            var sword = vfx.GetComponent<SwordHitSkill>();
            if (sword != null)
            {
                sword.attacker = attacker;
                sword.target = target;
                sword.isPerfect = perfect;
                sword.isHeavyAttack = false;
            }
        }

        // 傷害計算（沿用既有邏輯）
        BattleEffectManager.Instance.OnHit(attacker, target, perfect);

        yield return new WaitForSeconds(dashStayDuration);
        yield return Dash(actor, targetPoint, origin, dashDuration);
    }


    // --------------------------------------------------
    // 格檔
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
            Debug.Log("Miss！格檔未在節拍上。");
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
    // 旋轉邏輯
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
    // 攻擊序列與敵人搜尋
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
    // 敵人死亡與前推
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

}
