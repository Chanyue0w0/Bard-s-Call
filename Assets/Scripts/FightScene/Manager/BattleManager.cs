using System.Collections;
using System.Collections.Generic;
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
    // Fever 大招階段（第3拍觸發）
    // --------------------------------------------------
    public void TriggerFeverActions(int phase)
    {
        if (phase != 3) return; // 僅在第三拍觸發實際攻擊行動
        Debug.Log("[BattleManager] Fever 第3拍：全隊施放各自大招！");

        foreach (var slot in CTeamInfo)
        {
            if (slot == null || slot.Actor == null || slot.HP <= 0)
                continue;

            var charData = slot.Actor.GetComponent<CharacterData>();
            if (charData == null)
            {
                Debug.LogWarning($"[Fever] {slot.UnitName} 沒有 CharacterData，略過。");
                continue;
            }

            switch (slot.ClassType)
            {
                // -----------------------------
                // 聖騎士 Paladin
                // -----------------------------
                case UnitClass.Paladin:
                    StartCoroutine(HandlePaladinFever(slot, charData));
                    break;

                // -----------------------------
                // 吟遊詩人 Bard
                // -----------------------------
                case UnitClass.Bard:
                    StartCoroutine(HandleBardFever(slot, charData));
                    break;

                // -----------------------------
                // 法師 Mage
                // -----------------------------
                case UnitClass.Mage:
                    StartCoroutine(HandleMageFever(slot, charData));
                    break;

                default:
                    Debug.Log($"[Fever] {slot.UnitName} 無特別大招。");
                    break;
            }
        }
    }

    #region Fever 技能細節實作

    // =============================================
    // 1. 聖騎士 Fever：全體嘲諷攻擊
    // =============================================
    private IEnumerator HandlePaladinFever(TeamSlotInfo paladin, CharacterData data)
    {
        Debug.Log($"[Fever-Paladin] {paladin.UnitName} 啟動神聖猛擊！");

        var firstTarget = FindNextValidEnemy(0);
        if (firstTarget == null || firstTarget.Actor == null)
        {
            Debug.Log("[Fever-Paladin] 無敵人存在，跳過 Dash。");
            yield break;
        }

        Transform actor = paladin.Actor.transform;
        Vector3 origin = actor.position;
        Vector3 targetPos = firstTarget.SlotTransform.position + meleeContactOffset;

        // 拍長（以 BeatManager 為基準）
        float secondsPerBeat = (BeatManager.Instance != null)
            ? (60f / BeatManager.Instance.bpm)
            : 0.6f;

        // 第1拍：衝刺到敵人位置
        yield return Dash(actor, origin, targetPos, dashDuration);
        Debug.Log("[Fever-Paladin] Dash 完成 → 蓄力準備中。");

        // 第2拍：原地停留（蓄力Pose）
        yield return new WaitForSeconds(secondsPerBeat);
        Debug.Log("[Fever-Paladin] 開始釋放神聖猛擊特效！");

        // 第3拍：生成 MultiStrikeSkill（攻擊）
        if (data.Skills != null && data.Skills[0].SkillPrefab != null)
        {
            GameObject skillObj = Instantiate(data.Skills[0].SkillPrefab, targetPos, Quaternion.identity);
            MultiStrikeSkill skill = skillObj.GetComponent<MultiStrikeSkill>();

            if (skill != null)
            {
                // 指定攻擊者
                skill.attacker = paladin;

                // 取得所有有效敵人
                List<BattleManager.TeamSlotInfo> allEnemies = new List<BattleManager.TeamSlotInfo>();
                foreach (var enemy in EnemyTeamInfo)
                {
                    if (enemy != null && enemy.Actor != null && enemy.HP > 0)
                        allEnemies.Add(enemy);
                }

                skill.targets = allEnemies;
                skill.isPerfect = true;
                skill.isHeavyAttack = true;
                //skill.damage = paladin.Atk * 2; // 可視覺化傷害調整
            }
        }

        // 第4拍：原地等待一拍（收刀Pose）
        Debug.Log("[Fever-Paladin] 等待一拍後返回原位。");
        yield return new WaitForSeconds(secondsPerBeat);

        // 返回原位
        yield return Dash(actor, targetPos, origin, dashDuration);
        Debug.Log("[Fever-Paladin] 返回原位完成。");
    }

    // =============================================
    // 2. 吟遊詩人 Fever：共七拍節奏
    // 第1～5拍：延遲（演奏前奏）
    // 第5拍末：Dash 至敵方首位前方（若無敵人則固定前進）
    // 第6拍：在全隊位置生成治癒特效並全體治療（特效綁定角色）
    // 第7拍：收尾並返回原位
    // =============================================
    private IEnumerator HandleBardFever(TeamSlotInfo bard, CharacterData data)
    {
        Debug.Log($"[Fever-Bard] {bard.UnitName} 正在演奏治癒旋律……");

        float secondsPerBeat = (BeatManager.Instance != null)
            ? (60f / BeatManager.Instance.bpm)
            : 0.6f;

        // 找出敵方首位（若無敵人則給預設 dash 位置）
        var firstTarget = FindNextValidEnemy(0);
        Transform actor = bard.Actor.transform;
        Vector3 origin = actor.position;
        Vector3 dashTargetPos;

        if (firstTarget != null && firstTarget.Actor != null)
            dashTargetPos = firstTarget.SlotTransform.position + meleeContactOffset;
        else
            dashTargetPos = origin + new Vector3(-1.5f, 0f, 0f); // 沒敵人時也前進一小段

        // ★ 第1～4拍：延遲（演奏前奏）
        yield return new WaitForSeconds(secondsPerBeat * 4f);
        Debug.Log("[Fever-Bard] 演奏高潮來臨，向前邁進！");

        // ★ 第4拍末：Dash 至敵方首位前方（必定等待完成）
        yield return Dash(actor, origin, dashTargetPos, dashDuration);
        Debug.Log("[Fever-Bard] Dash 完成，準備施放治癒！");

        // ★ 第5拍：在全隊位置生成治癒特效 + 全體治療
        yield return new WaitForSeconds(secondsPerBeat);
        Debug.Log($"[Fever-Bard] {bard.UnitName} 演奏群體治癒！");

        if (data.Skills != null && data.Skills[0] != null && data.Skills[0].SkillPrefab != null)
        {
            foreach (var ally in CTeamInfo)
            {
                if (ally != null && ally.Actor != null && ally.HP > 0)
                {
                    Vector3 spawnPos = ally.SlotTransform.position + new Vector3(0f, 0.5f, 0f);
                    GameObject vfx = Instantiate(data.Skills[0].SkillPrefab, spawnPos, Quaternion.identity);

                    // ★ 將治癒特效設為角色子物件，確保跟隨角色移動
                    vfx.transform.SetParent(ally.Actor.transform, worldPositionStays: true);

                    Debug.Log($"[Fever-Bard] 治癒光環生成並附著於 {ally.UnitName}！");
                    GameObject.Destroy(vfx, 4f);
                }
            }
        }
        else
        {
            Debug.LogWarning("[Fever-Bard] 未設定 Skill[0] 或 Prefab 無效，無法生成特效。");
        }

        // 執行全體治療
        BattleEffectManager.Instance.HealTeamWithEffect(50);
        Debug.Log("[Fever-Bard] 全體回復 50 HP！");

        // ★ 第7拍：收尾並返回原位
        yield return new WaitForSeconds(secondsPerBeat);
        yield return Dash(actor, dashTargetPos, origin, dashDuration);
        Debug.Log("[Fever-Bard] 返回原位完成。");
    }


    // =============================================
    // 3. 法師 Fever：共經過五拍
    // 第1～2拍：延遲蓄氣
    // 第3拍：Dash 至敵方首位
    // 第4拍：生成雷電 MultiStrikeSkill（敵方第2格）
    // 第5拍：停留一拍後返回原位
    // =============================================
    private IEnumerator HandleMageFever(TeamSlotInfo mage, CharacterData data)
    {
        Debug.Log($"[Fever-Mage] {mage.UnitName} 正在聚集魔力……");

        // 每拍秒數
        float secondsPerBeat = (BeatManager.Instance != null)
            ? (60f / BeatManager.Instance.bpm)
            : 0.6f;

        // 找出敵方首位與第二位
        var firstTarget = FindNextValidEnemy(0);
        var secondTarget = (EnemyTeamInfo.Length > 1) ? EnemyTeamInfo[1] : null;

        if (firstTarget == null || firstTarget.Actor == null)
        {
            Debug.Log("[Fever-Mage] 無敵人存在，跳過 Dash。");
            yield break;
        }

        Transform actor = mage.Actor.transform;
        Vector3 origin = actor.position;
        Vector3 dashTargetPos = firstTarget.SlotTransform.position + meleeContactOffset;

        // ★ 第1～2拍：延遲兩拍（聚氣）
        yield return new WaitForSeconds(secondsPerBeat * 2f);
        Debug.Log("[Fever-Mage] 聚能完成，開始施放法術！");

        // ★ 第3拍：Dash 至首位敵人前
        yield return Dash(actor, origin, dashTargetPos, dashDuration);
        Debug.Log("[Fever-Mage] Dash 完成，準備釋放雷擊！");

        // ★ 第4拍：在敵方第二位生成 MultiStrikeSkill
        yield return new WaitForSeconds(secondsPerBeat);

        Vector3 spawnPos;
        if (secondTarget != null && secondTarget.Actor != null)
            spawnPos = secondTarget.SlotTransform.position;
        else
        {
            // fallback：若第二格沒敵人則使用中衛或固定點
            int midIndex = EnemyTeamInfo.Length / 2;
            var targetMid = EnemyTeamInfo[midIndex];
            spawnPos = (targetMid != null && targetMid.Actor != null)
                ? targetMid.SlotTransform.position
                : new Vector3(-2f, 0f, 0f);
        }

        if (data.Skills != null && data.Skills[0] != null && data.Skills[0].SkillPrefab != null)
        {
            GameObject skillObj = Instantiate(data.Skills[0].SkillPrefab, spawnPos, Quaternion.identity);
            Debug.Log($"[Fever-Mage] 雷電 MultiStrikeSkill 生成於敵方第2位置！");

            // 設定 MultiStrikeSkill 屬性
            MultiStrikeSkill skill = skillObj.GetComponent<MultiStrikeSkill>();
            if (skill != null)
            {
                skill.attacker = mage;

                // 加入全體敵人為目標
                List<BattleManager.TeamSlotInfo> allEnemies = new List<BattleManager.TeamSlotInfo>();
                foreach (var enemy in EnemyTeamInfo)
                {
                    if (enemy != null && enemy.Actor != null && enemy.HP > 0)
                        allEnemies.Add(enemy);
                }

                skill.targets = allEnemies;
                skill.isPerfect = true;
                skill.isHeavyAttack = true;
            }
        }
        else
        {
            Debug.LogWarning("[Fever-Mage] 未設定 Skill[0] 或 Prefab 無效，無法生成特效。");
        }

        // ★ 第5拍：原地停留一拍（收尾Pose）
        yield return new WaitForSeconds(secondsPerBeat);
        Debug.Log("[Fever-Mage] 結束施法，返回原位。");

        // ★ 返回原位
        yield return Dash(actor, dashTargetPos, origin, dashDuration);
        Debug.Log("[Fever-Mage] 返回原位完成。");
    }

    #endregion

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
                //int damage = chargeStacks * 30;
                //target.HP -= damage;
                //if (target.HP < 0) target.HP = 0;

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
        //BattleEffectManager.Instance.OnHit(attacker, target, perfect);

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
