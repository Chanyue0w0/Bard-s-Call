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

    [Header("Fever 大招展示位置")]
    public Transform feverUltShowingPoint;

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
    //private System.Action<InputAction.CallbackContext> blockP1Handler;
    //private System.Action<InputAction.CallbackContext> blockP2Handler;
    //private System.Action<InputAction.CallbackContext> blockP3Handler;

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
        //blockP1Handler = ctx => OnBlockKey(0);
        //blockP2Handler = ctx => OnBlockKey(1);
        //blockP3Handler = ctx => OnBlockKey(2);
        feverUltHandler = ctx => OnFeverUltimate();

        FMODBeatListener2.OnGlobalBeat += HandleBeatEffects; // ★ 新增

        if (actionAttackP1 != null) { actionAttackP1.action.started += attackP1Handler; actionAttackP1.action.Enable(); }
        if (actionAttackP2 != null) { actionAttackP2.action.started += attackP2Handler; actionAttackP2.action.Enable(); }
        if (actionAttackP3 != null) { actionAttackP3.action.started += attackP3Handler; actionAttackP3.action.Enable(); }
        //if (actionBlockP1 != null) { actionBlockP1.action.started += blockP1Handler; actionBlockP1.action.Enable(); }
        //if (actionBlockP2 != null) { actionBlockP2.action.started += blockP2Handler; actionBlockP2.action.Enable(); }
        //if (actionBlockP3 != null) { actionBlockP3.action.started += blockP3Handler; actionBlockP3.action.Enable(); }
        if (actionFeverUltimate != null) { actionFeverUltimate.action.started += feverUltHandler; actionFeverUltimate.action.Enable(); }
    }

    private void OnDisable()
    {
        if (actionAttackP1 != null) actionAttackP1.action.started -= attackP1Handler;
        if (actionAttackP2 != null) actionAttackP2.action.started -= attackP2Handler;
        if (actionAttackP3 != null) actionAttackP3.action.started -= attackP3Handler;
        //if (actionBlockP1 != null) actionBlockP1.action.started -= blockP1Handler;
        //if (actionBlockP2 != null) actionBlockP2.action.started -= blockP2Handler;
        //if (actionBlockP3 != null) actionBlockP3.action.started -= blockP3Handler;
        if (actionFeverUltimate != null) { actionFeverUltimate.action.started -= feverUltHandler;}

        FMODBeatListener2.OnGlobalBeat -= HandleBeatEffects; // ★ 新增
    }

    //聆聽Beat，提供每拍效果偵測
    private void HandleBeatEffects(int beat)
    {
        BattleEffectManager.Instance.TickPoison();
        BattleEffectManager.Instance.TickTauntBeats(); // 可選
        BattleEffectManager.Instance.TickHolyEffect();

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
        //if (FeverManager.Instance == null) return;

        //// 使用新的 FMODBeatListener2
        //if (FMODBeatListener2.Instance == null)
        //{
        //    Debug.LogWarning("[BattleManager] FMODBeatListener2.Instance 為 null，無法判定對拍！");
        //    return;
        //}

        //bool perfect = FMODBeatListener2.Instance.IsOnBeat();
        //if (!perfect)
        //{
        //    Debug.Log("[BattleManager] Fever大招 Miss，未在節拍上，不觸發。");
        //    return;
        //}

        //if (FeverManager.Instance.currentFever < FeverManager.Instance.feverMax)
        //{
        //    Debug.Log("[BattleManager] Fever未滿，無法啟動大招。");
        //    return;
        //}

        //Debug.Log("[BattleManager] 對拍成功且Fever滿值，啟動全隊大招！");
        //StartCoroutine(FeverManager.Instance.HandleFeverUltimateSequence());
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
        Vector3 targetPos = (feverUltShowingPoint != null)
            ? feverUltShowingPoint.position
            : (firstTarget.SlotTransform.position + meleeContactOffset);


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

        dashTargetPos = feverUltShowingPoint.position;

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
                    Vector3 spawnPos = ally.Actor.transform.position + new Vector3(0f, 0.5f, 0f);
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
        Vector3 dashTargetPos = (feverUltShowingPoint != null)
            ? feverUltShowingPoint.position
            : (firstTarget.SlotTransform.position + meleeContactOffset);


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
        if (attacker.HP <= 0)
        {
            Debug.Log($"[{attacker.UnitName}] 已死亡，僅打節拍但不觸發攻擊。");
            return;
        }

        var listener = FMODBeatListener2.Instance;
        if (listener == null)
        {
            Debug.LogWarning("[BattleManager] Listener2 尚未初始化，無法判定對拍！");
            return;
        }

        // ★★★ 使用新版 Listener2 的 IsOnBeat() (3 個 out) ★★★
        bool hit = listener.IsOnBeat(
            out FMODBeatListener2.Judge judge,
            out int nearestBeatIndex,
            out float deltaSec
        );

        // ★★★ Perfect / Miss 判定（新版） ★★★
        bool perfect = hit && judge == FMODBeatListener2.Judge.Perfect;
        // 使用動畫呼叫
        var anim = attacker.Actor.GetComponent<PressedAnimation>();

        if (perfect)
        {
            if (anim != null) anim.PlayPerfect();
        }
        else
        {
            if (anim != null) anim.PlayMiss();
            Debug.Log("Miss！未在節拍上，不觸發攻擊。");
            return;
        }


        // ★★★ 新 Listener2 的 “四拍循環” 來源（正確） ★★★
        int beatInCycle = listener.CorrectedBeatInCycle;

        // ★★★ 一小節拍數（通常是 4） ★★★
        int beatsPerMeasure = listener.BeatsPerMeasure;

        var target = FindEnemyByClass(attacker.ClassType);

        // ------------------------------------------------------------
        // 特例處理：即使沒有敵人也能發動的技能
        // ------------------------------------------------------------
        if (target == null)
        {
            // 法師普攻：無敵人仍能充能（非重拍）
            if (attacker.ClassType == UnitClass.Mage && beatInCycle != beatsPerMeasure)
            {
                Debug.Log("[特例] 法師普攻在無敵人時仍可充能");
                StartCoroutine(HandleMageAttack(attacker, null, beatInCycle, beatsPerMeasure, perfect));
                return;
            }

            // 吟遊詩人重拍：無敵人也能治癒
            if (attacker.ClassType == UnitClass.Bard)
            {
                Debug.Log("[特例] 吟遊詩人重攻擊在無敵人時仍可施放治癒");
                StartCoroutine(HandleBardAttack(attacker, null, beatInCycle, beatsPerMeasure, perfect));
                return;
            }

            // 其他角色無敵人 → 不攻擊
            return;
        }

        // ------------------------------------------------------------
        // 正常攻擊流程
        // ------------------------------------------------------------
        lastSuccessfulAttacker = attacker.Actor;
        StartCoroutine(LockAction(actionLockDuration));

        if (attacker.ClassType == UnitClass.Warrior)
            StartCoroutine(HandleWarriorAttack(attacker, target, beatInCycle, beatsPerMeasure, perfect));
        else if (attacker.ClassType == UnitClass.Mage)
            StartCoroutine(HandleMageAttack(attacker, target, beatInCycle, beatsPerMeasure, perfect));
        else if (attacker.ClassType == UnitClass.Ranger)
            StartCoroutine(HandleRangerAttack(attacker, beatInCycle, beatsPerMeasure, perfect));
        else if (attacker.ClassType == UnitClass.Bard)
            StartCoroutine(HandleBardAttack(attacker, target, beatInCycle, beatsPerMeasure, perfect));
        else if (attacker.ClassType == UnitClass.Paladin)
            StartCoroutine(HandlePaladinAttack(attacker, target, beatInCycle, beatsPerMeasure, perfect));
        else
            StartCoroutine(AttackSequence(attacker, target, target.SlotTransform.position, perfect));
    }



    private IEnumerator HandleWarriorAttack(TeamSlotInfo attacker, TeamSlotInfo target, int beatInCycle,int beatsPerMeasure, bool perfect)
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
        if (beatInCycle == beatsPerMeasure)
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
                sword.isHeavyAttack = (beatInCycle == beatsPerMeasure);
            }
        }

        yield return new WaitForSeconds(dashStayDuration);
        yield return Dash(actor, targetPoint, origin, dashDuration);
    }

    private IEnumerator HandleMageAttack(TeamSlotInfo attacker, TeamSlotInfo target, int beatInCycle, int beatsPerMeasure, bool perfect)
    {
        var actor = attacker.Actor.transform;
        var charData = attacker.Actor.GetComponent<CharacterData>();
        if (charData == null) yield break;

        int chargeStacks = BattleEffectManager.Instance.GetChargeStacks(attacker);

        // === Damage Matrix（依照 0~6 層） ===
        int[] damageMatrix = new int[] { 10, 20, 30, 50, 70, 90, 120 };
        int clampedStack = Mathf.Clamp(chargeStacks, 0, damageMatrix.Length - 1);
        int heavyDamage = damageMatrix[clampedStack] + GlobalIndex.RythmResonanceBuff;

        // === 第四拍：重攻擊 ===
        if (beatInCycle == beatsPerMeasure)
        {
            Vector2 spawnPos = enemyPositions[1].position;

            GameObject skillObj = Instantiate(charData.HeavyAttack.SkillPrefab, spawnPos, Quaternion.identity);
            Debug.Log($"[Fever-Mage] 雷電 MultiStrikeSkill 生成於敵方第2位置！");

            // 設定 MultiStrikeSkill 屬性
            MultiStrikeSkill skill = skillObj.GetComponent<MultiStrikeSkill>();
            if (skill != null)
            {
                skill.attacker = attacker;

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
                skill.damage = heavyDamage;   // ★ 使用矩陣 damage
            }

            BattleEffectManager.Instance.ResetChargeStacks(attacker);

            yield break;
        }

        // === 普通攻擊 ===
        else
        {
            if (target != null && charData.NormalAttacks != null)
            {
                var normal = Instantiate(charData.NormalAttacks[0].SkillPrefab, target.Actor.transform.position, Quaternion.identity);
                var skill = normal.GetComponent<FireBallSkill>();
                if (skill != null)
                {
                    skill.attacker = attacker;
                    skill.target = target;
                    skill.isPerfect = perfect;
                    skill.isHeavyAttack = true;
                    skill.damage = 10 + GlobalIndex.RythmResonanceBuff;
                }
            }
            // 增加疊層 & 特效
            BattleEffectManager.Instance.AddChargeStack(attacker);
            Debug.Log($"[法師普攻] 第 {beatInCycle} 拍充能 +1 層。");
        }

        yield return null;
    }


    // --------------------------------------------------
    // Ranger 攻擊邏輯
    // --------------------------------------------------
    private IEnumerator HandleRangerAttack(TeamSlotInfo attacker, int beatInCycle,int beatsPerMeasure, bool perfect)
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
        if (beatInCycle == beatsPerMeasure)
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
    private IEnumerator HandleBardAttack(TeamSlotInfo attacker, TeamSlotInfo target, int beatInCycle, int beatsPerMeasure, bool perfect)
    {
        var actor = attacker.Actor.transform;
        var charData = attacker.Actor.GetComponent<CharacterData>();
        if (charData == null) yield break;

        // **普通攻擊：需要敵人存在**
        if (beatInCycle != beatsPerMeasure)
        {
            // **輕攻擊：全隊回復血量 +10**
            Debug.Log($"[吟遊詩人重攻擊] {attacker.UnitName} 演奏治癒之歌，全隊回復10HP！");
            BattleEffectManager.Instance.HealTeamWithEffect(10 + GlobalIndex.RythmResonanceBuff);
        }
        else
        {
            // **重攻擊：全隊回復血量 +50**
            Debug.Log($"[吟遊詩人重攻擊] {attacker.UnitName} 演奏治癒之歌，全隊回復20HP！");
            BattleEffectManager.Instance.HealTeamWithEffect(20 + GlobalIndex.RythmResonanceBuff);
        }

        yield return null;
    }

    // --------------------------------------------------
    // Paladin 攻擊邏輯（輕攻擊 + 重攻擊）
    // --------------------------------------------------
    private IEnumerator HandlePaladinAttack(TeamSlotInfo attacker, TeamSlotInfo target, int beatInCycle, int beatsPerMeasure, bool perfect)
    {
        var actor = attacker.Actor.transform;
        var charData = attacker.Actor.GetComponent<CharacterData>();
        if (charData == null) yield break;

        int index = System.Array.FindIndex(CTeamInfo, t => t == attacker);

        // ---------------------------
        // ★ 第一步：所有 Paladin 攻擊 → 先格檔一拍
        // ---------------------------
        BattleEffectManager.Instance.ActivateBlock(
            index,
            0.9f, //BeatManager.Instance.beatTravelTime
            charData,
            attacker.Actor
        );

        // *延遲 0.05 秒讓格檔特效確實生成（安全做法）
        yield return new WaitForSeconds(0.05f);

        // ---------------------------
        // ★ 第二步：判斷 輕攻擊 / 重攻擊
        // ---------------------------
        bool isHeavy = (beatInCycle == beatsPerMeasure);

        if (!isHeavy)
        {
            // ---------------------------
            // ★ 輕攻擊 → 只有格檔，不做任何攻擊行為
            // ---------------------------
            Debug.Log($"[Paladin 輕攻擊格檔] {attacker.UnitName} 格檔 1 拍。");

            // 可考慮播一個防禦動畫：actor.GetComponent<PressedAnimation>()?.PlayPerfect();

            yield break;
        }
        else
        {
            // =========================================================
            // 3. ★★★ Paladin 重攻擊：射出 FireBallSkill（40 傷害）★★★
            // =========================================================
            //Debug.Log($"[Paladin 重攻擊] {attacker.UnitName} 發動神聖火焰！");

            //// 找首位敵人
            //var firstEnemy = FindNextValidEnemy(0);
            //if (firstEnemy == null || firstEnemy.Actor == null)
            //{
            //    Debug.Log("[Paladin 重攻擊] 沒有敵人，取消攻擊。");
            //    yield break;
            //}

            //// 取出重攻擊技能 prefab（必須為 FireBallSkill）
            //if (charData.HeavyAttack == null || charData.HeavyAttack.SkillPrefab == null)
            //{
            //    Debug.LogWarning("[Paladin 重攻擊] HeavyAttack Prefab 未設定！");
            //    yield break;
            //}

            //GameObject projectilePrefab = charData.HeavyAttack.SkillPrefab;

            //// ---------------------------------------------------------
            //// 3-1. 投射物生成位置
            //// ---------------------------------------------------------
            //Vector3 spawnOffset = new Vector3(0f, 0.5f, 0f);
            //Vector3 spawnPos = actor.position + spawnOffset;

            //GameObject proj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);

            //// ---------------------------------------------------------
            //// 3-2. 套用 FireBallSkill 數值（與弓箭手一致）
            //// ---------------------------------------------------------
            //FireBallSkill fire = proj.GetComponent<FireBallSkill>();
            //if (fire != null)
            //{
            //    fire.attacker = attacker;
            //    fire.target = firstEnemy;
            //    fire.isPerfect = perfect;
            //    fire.isHeavyAttack = true;

            //    // ★ 固定傷害 10 點（你指定的）
            //    fire.damage = 10;
            //}
            //else
            //{
            //    Debug.LogWarning("[Paladin 重攻擊] HeavyAttack Prefab 裡沒有 FireBallSkill！");
            //}
        }
        
        yield break;
    }


    // --------------------------------------------------
    // 格檔
    // --------------------------------------------------
    //private void OnBlockKey(int index)
    //{
    //    //if (_isActionLocked) return;
    //    //if (index < 0 || index >= CTeamInfo.Length) return;
    //    //var slot = CTeamInfo[index];
    //    //if (slot == null || slot.Actor == null) return;

    //    //if (FMODBeatListener2.Instance == null)
    //    //{
    //    //    Debug.LogWarning("[BattleManager] FMODBeatListener2.Instance 為 null，無法判定格檔對拍！");
    //    //    return;
    //    //}

    //    //bool perfect = FMODBeatListener2.Instance.IsOnBeat();
    //    //if (!perfect)
    //    //{
    //    //    Debug.Log("Miss！格檔未在節拍上。");
    //    //    return;
    //    //}

    //    //var charData = slot.Actor.GetComponent<CharacterData>();
    //    //if (charData == null) return;

    //    //BattleEffectManager.Instance.ActivateBlock(
    //    //    index,
    //    //    BeatManager.Instance.beatTravelTime,   // 這個之後也要改成 FMOD 的時間
    //    //    charData,
    //    //    slot.Actor
    //    //);
    //}

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

    // --------------------------------------------------
    // 返回主畫面（CampScene）
    // --------------------------------------------------
    public void ReturnToCampScene()
    {
        // 重置關卡索引（可視需求調整）
        GlobalIndex.CurrentStageIndex = 0;
        GlobalIndex.TotalBattleTime = 0;
        GlobalIndex.MaxCombo = 0;

        // 切換回主畫面
        UnityEngine.SceneManagement.SceneManager.LoadScene("CampScene");
    }


    // --------------------------------------------------
    // 玩家失敗判定與 LosePanel 顯示
    // --------------------------------------------------
    [Header("戰敗 UI 面板")]
    public GameObject losePanel;
    private bool isBattleEnded = false;

    public void CheckPlayerDefeat()
    {
        if (isBattleEnded) return;

        bool allDead = true;

        if (GlobalIndex.CurrentTotalHP > 0)
            allDead = false;
        //foreach (var slot in CTeamInfo)
        //{
        //    if (slot != null && slot.Actor != null && slot.HP > 0)
        //    {
        //        allDead = false;
        //        break;
        //    }
        //}

        if (allDead)
        {
            isBattleEnded = true;
            ShowLosePanel();
        }
    }

    private void ShowLosePanel()
    {
        if (losePanel != null)
        {
            losePanel.SetActive(true);
            Debug.Log("[BattleManager] 全員陣亡，開啟 LosePanel！");
        }
        else
        {
            Debug.LogWarning("[BattleManager] LosePanel 未綁定！");
        }
    }

}
