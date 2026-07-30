using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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

    [Header("魔王操作資料")]
    [SerializeField] private BossData bossData;

    [SerializeField] private BeatSpriteAnimator bossAnimator;

    [Header("魔王普通攻擊連段狀態")]
    [SerializeField] private int bossNormalAttackIndex = 0;
    private readonly string[] bossNormalAttackClipNames =
    {
        "NormalAttack1",
        "NormalAttack2",
        "NormalAttack3",
        "NormalAttack4"
    };
    [Header("魔王攻擊待處理資料")]
    private bool hasPendingBossAttack = false;

    private FMODBeatListener2.Judge pendingBossAttackJudge;

    [Header("輸入（新 Input System）")]
    public InputActionReference actionAttackP1;
    public InputActionReference actionAttackP2;
    public InputActionReference actionAttackP3;
    public InputActionReference actionRotateLeft;
    public InputActionReference actionRotateRight;
    public InputActionReference actionBlockP1;
    public InputActionReference actionBlockP2;
    public InputActionReference actionBlockP3;

    [Header("輸入 Win,LoseMenu （新 Input System）")]
    public InputActionReference actionRestart;
    public InputActionReference actionNextLevel;
    public InputActionReference actionBackToMenu;


    [Header("Exit Game")]
    public InputActionReference actionExitGame;

    [Header("Fever 大招輸入")]
    public InputActionReference actionFeverUltimate;  // 新增輸入引用 (在 Inspector 綁定)
    private System.Action<InputAction.CallbackContext> feverUltHandler;

    [Header("輸入（新 Input System）對應按鍵")]
    private readonly char[] feverKeyMap = new char[] { 'X', 'Y', 'B', 'A' };

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

    [Header("Demon Destruction Ray")]
    public GameObject demonDestructionRayPrefab;   // 預設放入你的 Prefab（含 MultiStrikeSkill）
    public Transform demonRaySpawnPoint;           // 生成位置（可用 Bard 前方或 Demon 前方）

    [Header("Heavy Beat Expression VFX (三位玩家預設於場景 重拍提示特效)")]
    public GameObject heavyBeatVFX_P1;
    public GameObject heavyBeatVFX_P2;
    public GameObject heavyBeatVFX_P3;

    [Header("Fever 輸入狀態判定")]
    private bool isFeverInputMode = false;

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

    [Header("WinPanel (結算面板)")]
    public GameObject winPanel; 
    public Text summaryTimeText;
    public Text summaryComboText;

    [Header("LosePanel (結算面板)")]
    public GameObject losePanel;
    private bool isBattleEnded = false;

    // 用於安全解除 Input 綁定
    private System.Action<InputAction.CallbackContext> attackP1Handler;
    private System.Action<InputAction.CallbackContext> attackP2Handler;
    private System.Action<InputAction.CallbackContext> attackP3Handler;
    private System.Action<InputAction.CallbackContext> exitGameHandler;
    private System.Action<InputAction.CallbackContext> restartHandler;
    private System.Action<InputAction.CallbackContext> nextLevelHandler;
    private System.Action<InputAction.CallbackContext> backToMenuHandler;

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
        
        // 魔王動畫元件沒有另外指定時，
        // 嘗試從 BossData 所在角色中尋找。
        if (bossData != null && bossAnimator == null)
        {
            bossAnimator =
                bossData.GetComponentInChildren<BeatSpriteAnimator>();
        }
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
        exitGameHandler = ctx => OnExitGamePerformed();

        FMODBeatListener2.OnGlobalBeat += HandleBeatEffects; // ★ 新增

        if (actionAttackP1 != null) { actionAttackP1.action.started += attackP1Handler; actionAttackP1.action.Enable(); }
        if (actionAttackP2 != null) { actionAttackP2.action.started += attackP2Handler; actionAttackP2.action.Enable(); }
        if (actionAttackP3 != null) { actionAttackP3.action.started += attackP3Handler; actionAttackP3.action.Enable(); }
        //if (actionBlockP1 != null) { actionBlockP1.action.started += blockP1Handler; actionBlockP1.action.Enable(); }
        //if (actionBlockP2 != null) { actionBlockP2.action.started += blockP2Handler; actionBlockP2.action.Enable(); }
        //if (actionBlockP3 != null) { actionBlockP3.action.started += blockP3Handler; actionBlockP3.action.Enable(); }
        if (actionFeverUltimate != null) { actionFeverUltimate.action.started += feverUltHandler; actionFeverUltimate.action.Enable(); }

        if (actionExitGame != null)
        {
            actionExitGame.action.performed += exitGameHandler;
            actionExitGame.action.Enable();
        }

        // Restart / BackToMenu 綁定
        restartHandler = ctx => RestartBattle();
        nextLevelHandler = ctx => NextBattle();
        backToMenuHandler = ctx => BackToMenu();

        if (actionRestart != null)
        {
            actionRestart.action.performed += restartHandler;
            actionRestart.action.Disable();   // 平常不能按
        }

        if(actionNextLevel !=  null)
        {
            actionNextLevel.action.performed += nextLevelHandler;
            actionNextLevel.action.Disable();   // 平常不能按
        }

        if (actionBackToMenu != null)
        {
            actionBackToMenu.action.performed += backToMenuHandler;
            actionBackToMenu.action.Disable(); // 平常不能按
        }

        // 魔王動畫事件綁定
        if (bossAnimator == null && bossData != null)
        {
            bossAnimator =
                bossData.GetComponentInChildren<BeatSpriteAnimator>();
        }

        if (bossAnimator != null)
        {
            // 避免重複綁定
            bossAnimator.OnFrameEvent -= HandleBossAnimationFrameEvent;
            bossAnimator.OnFrameEvent += HandleBossAnimationFrameEvent;
        }
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

        if (actionExitGame != null)
            actionExitGame.action.performed -= exitGameHandler;

        if (actionRestart != null)
            actionRestart.action.performed -= restartHandler;

        if (actionNextLevel != null)
            actionNextLevel.action.performed -= nextLevelHandler;

        if (actionBackToMenu != null)
            actionBackToMenu.action.performed -= backToMenuHandler;

        if (bossAnimator != null)
        {
            bossAnimator.OnFrameEvent -= HandleBossAnimationFrameEvent;
        }

        FMODBeatListener2.OnGlobalBeat -= HandleBeatEffects; // ★ 新增


    }

    private void OnExitGamePerformed()
    {
        if (GlobalIndex.GameOver) return;
        if (GetFeverInputMode()) return;
        ReturnToCampScene();
    }


    //聆聽Beat，提供每拍效果偵測
    private void HandleBeatEffects(int beat)
    {
        BattleEffectManager.Instance.TickPoison();
        BattleEffectManager.Instance.TickTauntBeats(); // 可選
        BattleEffectManager.Instance.TickHolyEffect();

    }

    //進入Fever輸入狀態
    public void EnterFeverInputMode()
    {
        isFeverInputMode = true;
        Debug.Log("[BattleManager] 進入 Fever Input 模式（忽略節奏、改由 QTE 處理）");
    }

    //退出Fever輸入狀態
    public void ExitFeverInputMode()
    {
        isFeverInputMode = false;
        Debug.Log("[BattleManager] 離開 Fever Input 模式（恢復節奏攻擊）");
    }

    public bool GetFeverInputMode()
    {
        return isFeverInputMode;
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
        if (GlobalIndex.GameOver)//若遊戲結束
            return;
        if (GlobalIndex.isTutorialPanelOpened)//若遊戲教學中
            return;

        if (FeverManager.Instance == null) return;

        if (isFeverInputMode)
        {
            // 轉換 index → char
            char key = feverKeyMap[3];

            // 傳給 QTE Manager
            FeverQTEManager.Instance.OnPlayerHit(key);
            return;
        }

        // 拍點判定
        var listener = FMODBeatListener2.Instance;
        if (listener == null) return;

        bool hit = listener.IsOnBeat(
            out FMODBeatListener2.Judge judge,
            out int beatIndex,
            out float delta
        );

        if (judge != FMODBeatListener2.Judge.Perfect)
        {
            Debug.Log("[BattleManager] Fever大招 Miss，未在節拍上，不觸發。");
            return;
        }

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
        if (GlobalIndex.GameOver)//若遊戲結束
            return;
        if (GlobalIndex.isTutorialPanelOpened)//若遊戲教學中
            return;

        if (isFeverInputMode)
        {
            // 轉換 index → char
            char key = feverKeyMap[index];

            // 傳給 QTE Manager
            FeverQTEManager.Instance.OnPlayerHit(key);
            return;
        }

        // 一般戰鬥中，OnAttackKey(0) 改為魔王普通攻擊。
        if (index == 0)
        {
            HandleBossNormalAttackInput();
            return;
        }
        if (index == 1)
        {
            HandleBossBlockInput();
            return;
        }

        if (index == 2)
        {
            HandleBossHeavyAttackInput();
            return;
        }
        // 以下保留原本勇者操作程式。
        // 現階段 OnAttackKey(1)、OnAttackKey(2) 還會走舊流程。
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

        // Heavy Beat (通常為第 beatsPerMeasure 拍)
        bool isHeavyBeat = (beatInCycle == beatsPerMeasure);

        // P1 P2 P3 對應的 VFX
        GameObject heavyVfx = null;
        switch (index)
        {
            case 0: heavyVfx = heavyBeatVFX_P1; break;
            case 1: heavyVfx = heavyBeatVFX_P2; break;
            case 2: heavyVfx = heavyBeatVFX_P3; break;
        }

        if (perfect && isHeavyBeat && heavyVfx != null)
        {
            StartCoroutine(PlayHeavyBeatVFX(heavyVfx));
        }

        var target = FindEnemyByClass(attacker.ClassType);

        // ------------------------------------------------------------
        // 特例處理：即使沒有敵人也能發動的技能
        // ------------------------------------------------------------
        if (target == null)
        {
            
            // 其他角色無敵人 → 不攻擊
            return;
        }

        // ------------------------------------------------------------
        // 正常攻擊流程
        // ------------------------------------------------------------
        lastSuccessfulAttacker = attacker.Actor;
        StartCoroutine(LockAction(actionLockDuration));

        
        StartCoroutine(AttackSequence(attacker, target, target.SlotTransform.position, perfect));
    }

    private IEnumerator PlayHeavyBeatVFX(GameObject vfx)
    {
        vfx.SetActive(true);

        float beatSec = 0.5f;
        if (FMODBeatListener2.Instance != null)
            beatSec = FMODBeatListener2.Instance.SecondsPerBeat * 1f;

        yield return new WaitForSeconds(beatSec);

        vfx.SetActive(false);
    }

    private void HandleBossNormalAttackInput()
    {
        // ============================================================
        // 1. 確認魔王資料與動畫元件
        // ============================================================
        if (bossData == null)
        {
            Debug.LogWarning("[BattleManager] 尚未指定 BossData。");
            return;
        }

        if (bossAnimator == null)
        {
            bossAnimator =
                bossData.GetComponentInChildren<BeatSpriteAnimator>();

            if (bossAnimator == null)
            {
                Debug.LogWarning(
                    "[BattleManager] 魔王物件中找不到 BeatSpriteAnimator。"
                );
                return;
            }
        }

        // ============================================================
        // 2. 取得 FMOD 節拍判定
        // ============================================================
        FMODBeatListener2 listener = FMODBeatListener2.Instance;

        if (listener == null)
        {
            Debug.LogWarning(
                "[BattleManager] FMODBeatListener2 尚未初始化。"
            );
            return;
        }

        bool hit = listener.IsOnBeat(
            out FMODBeatListener2.Judge judge,
            out int nearestBeatIndex,
            out float deltaSec
        );

        bool isSuccessful =
        hit &&
        (
            judge == FMODBeatListener2.Judge.Perfect ||
            judge == FMODBeatListener2.Judge.Great
        );

        // ============================================================
        // 3. Miss：不播放攻擊，連段回到第一段
        // ============================================================
        if (!isSuccessful)
        {
            bossNormalAttackIndex = 0;
            hasPendingBossAttack = false;

            Debug.Log(
                "[Boss] 普通攻擊 Miss，下一次從第 1 段開始。"
            );

            return;
        }

        // ============================================================
        // 4. 決定目前可使用的普通攻擊段數
        // ============================================================
        int usableAttackCount = Mathf.Clamp(
            bossData.unlockedNormalAttackCount,
            1,
            bossNormalAttackClipNames.Length
        );

        // 防止 Index 因 Inspector 或執行時修改而超出範圍
        if (bossNormalAttackIndex < 0 ||
            bossNormalAttackIndex >= usableAttackCount)
        {
            bossNormalAttackIndex = 0;
        }

        // ============================================================
        // 5. 取得目前段數的動畫名稱
        // ============================================================
        string attackClipName =
            bossNormalAttackClipNames[bossNormalAttackIndex];

        int currentAttackStage =
            bossNormalAttackIndex + 1;

        // ============================================================
        // 6. 播放 BeatSpriteAnimator 動畫
        // ============================================================
        bossAnimator.Play(
            attackClipName,
            true
        );

        // BeatSpriteAnimator 找不到名稱時，Play() 會直接返回。
        // 播放後確認目前 Clip 是否真的切換成功。
        if (bossAnimator.GetCurrentClipName() != attackClipName)
        {
            Debug.LogError(
                $"[BattleManager] BeatSpriteAnimator 找不到動畫 Clip：{attackClipName}"
            );

            bossNormalAttackIndex = 0;
            return;
        }

        // 保存這次攻擊的節拍判定，
        // 等動畫走到 triggerAttack 時再進行得分判定。
        hasPendingBossAttack = true;
        pendingBossAttackJudge = judge;

        Debug.Log(
            $"[Boss] Perfect！播放普通攻擊第 {currentAttackStage} 段：{attackClipName}"
        );

        // ============================================================
        // 7. 推進到下一段
        // ============================================================
        bossNormalAttackIndex++;

        if (bossNormalAttackIndex >= usableAttackCount)
        {
            bossNormalAttackIndex = 0;
        }
    }
    private void HandleBossNormalAttackHit(string attackClipName)
    {
        if (!hasPendingBossAttack)
        {
            Debug.LogWarning(
                $"[Boss] {attackClipName} 出現 triggerAttack，" +
                "但目前沒有待處理的普通攻擊。"
            );

            return;
        }

        hasPendingBossAttack = false;

        TeamSlotInfo target = FindFirstHero();

        if (target == null || target.Actor == null)
        {
            Debug.Log(
                $"[Boss] {attackClipName} 找不到首位勇者。"
            );

            return;
        }

        HeroCombatState targetState =
            target.Actor.GetComponent<HeroCombatState>();

        // Component 可能掛在 Actor 的父物件
        if (targetState == null)
        {
            targetState =
                target.Actor.GetComponentInParent<HeroCombatState>();
        }

        bool isTargetBlocking =
            targetState != null &&
            targetState.IsBlocking;

        if (isTargetBlocking)
        {
            Debug.Log(
                $"[Boss] {attackClipName} 被首位勇者格擋，無法得分。"
            );

            return;
        }

        Debug.Log(
            $"[Boss] {attackClipName} 成功命中首位勇者，" +
            $"判定={pendingBossAttackJudge}，可以獲得分數。"
        );

        // 下一步在此呼叫既有的加分方法
    }
    private void HandleBossBlockInput()
    {
        if (bossData == null)
            return;

        if (bossAnimator == null)
        {
            bossAnimator =
                bossData.GetComponentInChildren<BeatSpriteAnimator>();

            if (bossAnimator == null)
                return;
        }

        FMODBeatListener2 listener =
            FMODBeatListener2.Instance;

        if (listener == null)
            return;

        bool hit = listener.IsOnBeat(
            out FMODBeatListener2.Judge judge,
            out int beatIndex,
            out float delta
        );

        bool isSuccessful =
            hit &&
            (
                judge == FMODBeatListener2.Judge.Perfect ||
                judge == FMODBeatListener2.Judge.Great
            );

        if (!isSuccessful)
        {
            Debug.Log("[Boss] Block Miss");
            return;
        }

        bossAnimator.Play("Block", true);

        Debug.Log(
            $"[Boss] {judge} Block"
        );
    }

    private void HandleBossHeavyAttackInput()
    {
        if (bossData == null)
        {
            Debug.LogWarning(
                "[BattleManager] 尚未指定 BossData。"
            );
            return;
        }

        if (bossAnimator == null)
        {
            bossAnimator =
                bossData.GetComponentInChildren<BeatSpriteAnimator>();

            if (bossAnimator == null)
            {
                Debug.LogWarning(
                    "[BattleManager] 魔王物件中找不到 BeatSpriteAnimator。"
                );
                return;
            }
        }

        FMODBeatListener2 listener =
            FMODBeatListener2.Instance;

        if (listener == null)
        {
            Debug.LogWarning(
                "[BattleManager] FMODBeatListener2 尚未初始化。"
            );
            return;
        }

        bool hit = listener.IsOnBeat(
            out FMODBeatListener2.Judge judge,
            out int nearestBeatIndex,
            out float deltaSec
        );

        bool isSuccessful =
            hit &&
            (
                judge == FMODBeatListener2.Judge.Perfect ||
                judge == FMODBeatListener2.Judge.Great
            );

        if (!isSuccessful)
        {
            Debug.Log(
                "[Boss] 重擊 Miss，不播放動畫。"
            );
            return;
        }

        const string heavyAttackClipName =
            "HeavyAttack";

        bossAnimator.Play(
            heavyAttackClipName,
            true
        );

        if (bossAnimator.GetCurrentClipName() !=
            heavyAttackClipName)
        {
            Debug.LogError(
                $"[BattleManager] BeatSpriteAnimator 找不到動畫 Clip：{heavyAttackClipName}"
            );
            return;
        }

        bossNormalAttackIndex = 0;

        Debug.Log(
            $"[Boss] {judge}！播放重擊動畫 HeavyAttack。"
        );
    }

    private void HandleBossAnimationFrameEvent(
    BeatSpriteFrame frame)
    {
        if (frame == null)
            return;

        // 目前只監聽攻擊觸發幀
        if (!frame.triggerAttack)
            return;

        if (bossAnimator == null)
            return;

        string currentClipName =
            bossAnimator.GetCurrentClipName();

        Debug.Log(
            $"[Boss] 收到 triggerAttack 動畫事件，Clip={currentClipName}"
        );

        // 普通攻擊事件
        if (currentClipName == "NormalAttack1" ||
            currentClipName == "NormalAttack2" ||
            currentClipName == "NormalAttack3" ||
            currentClipName == "NormalAttack4")
        {
            Debug.Log(
                $"[Boss] 普通攻擊命中時機：{currentClipName}"
            );

            return;
        }

        // 重擊事件
        if (currentClipName == "HeavyAttack")
        {
            Debug.Log(
                "[Boss] 重擊命中時機：HeavyAttack"
            );

            return;
        }
    }

    private TeamSlotInfo FindFirstHero()
    {
        for (int i = 0; i < CTeamInfo.Length; i++)
        {
            TeamSlotInfo hero = CTeamInfo[i];

            if (hero == null)
                continue;

            if (hero.Actor == null)
                continue;

            return hero;
        }

        return null;
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

    // --------------------------------------------------
    // 返回主畫面（CampScene）
    // --------------------------------------------------
    public void ReturnToCampScene()
    {
        // 重置關卡索引（可視需求調整）
        GlobalIndex.CurrentStageIndex = 0;
        GlobalIndex.TotalBattleTime = 0;
        GlobalIndex.MaxCombo = 0;
        GlobalIndex.MaxFeverCombo = 0;

        GlobalIndex.GameOver = false;
        GlobalIndex.isTutorial = false;
        GlobalIndex.isTutorialPanelOpened = false;
        //Time.timeScale = 1; //暫停時間
        // 切換回主畫面
        UnityEngine.SceneManagement.SceneManager.LoadScene("CampScene");
    }



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

    public void ShowWinPanel()
    {
        if (winPanel != null)
        {
            winPanel.SetActive(true);
            Debug.Log("[BattleManager] 勝利，開啟 WinPanel！");
        }
        else
        {
            Debug.LogWarning("[BattleManager] LosePanel 未綁定！");
        }

        // ★ 更新結算文字
        if (summaryTimeText != null)
            summaryTimeText.text = $"通關時間 {GlobalIndex.TotalBattleTime:F1} 秒";

        if (summaryComboText != null)
            summaryComboText.text = $"最高Fever連擊數 {GlobalIndex.MaxFeverCombo} Combo";

        GlobalIndex.GameOver = true;//遊戲結束

        // 啟用 NextLevel / BackToMenu 按鍵
        if (actionNextLevel != null)
            actionNextLevel.action.Enable();

        if (actionBackToMenu != null)
            actionBackToMenu.action.Enable();
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
        //Time.timeScale = 0; //暫停時間
        GlobalIndex.GameOver = true;

        // 啟用 Restart / BackToMenu 按鍵
        if (actionRestart != null)
            actionRestart.action.Enable();

        if (actionBackToMenu != null)
            actionBackToMenu.action.Enable();
    }

    private void NextBattle()
    {
        // 重置關卡索引（可視需求調整）
        GlobalIndex.CurrentLevelIndex++;
        GlobalIndex.CurrentStageIndex = 0;
        GlobalIndex.TotalBattleTime = 0;
        GlobalIndex.MaxCombo = 0;
        GlobalIndex.MaxFeverCombo = 0;
        GlobalIndex.GameOver = false;
        GlobalIndex.isTutorial = false;
        GlobalIndex.isTutorialPanelOpened = false;
        GlobalIndex.CurrentTotalHP = 200;
        GlobalIndex.MaxTotalHP = 200;
        GlobalIndex.RythmResonanceBuff = 0; // 對拍共鳴臨時加乘

        if(GlobalIndex.CurrentLevelIndex > 2)
        {
            ReturnToCampScene();
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            );

        }

    }

    private void RestartBattle()
    {
        // 重置關卡索引（可視需求調整）
        GlobalIndex.CurrentStageIndex = 0;
        GlobalIndex.TotalBattleTime = 0;
        GlobalIndex.MaxCombo = 0;
        GlobalIndex.MaxFeverCombo = 0;
        GlobalIndex.GameOver = false;
        GlobalIndex.isTutorial = false;
        GlobalIndex.isTutorialPanelOpened = false;
        GlobalIndex.CurrentTotalHP = 200;
        GlobalIndex.MaxTotalHP = 200;
        GlobalIndex.RythmResonanceBuff = 0; // 對拍共鳴臨時加乘

        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
        );
    }

    private void BackToMenu()
    {
        ReturnToCampScene();  // 你原本的回主畫面功能
    }


    public Vector3 GetPlayerPosition(int index)
    {
        if (index < 0 || index >= CTeamInfo.Length)
            return Vector3.zero;

        var slot = CTeamInfo[index];
        if (slot == null) return Vector3.zero;

        if (slot.Actor != null)
            return slot.Actor.transform.position;

        if (slot.SlotTransform != null)
            return slot.SlotTransform.position;

        return Vector3.zero;
    }

    public Vector3 GetEnemyPosition(int index)
    {
        if (index < 0 || index >= EnemyTeamInfo.Length)
            return Vector3.zero;

        var slot = EnemyTeamInfo[index];
        if (slot == null) return Vector3.zero;

        if (slot.Actor != null)
            return slot.Actor.transform.position;

        if (slot.SlotTransform != null)
            return slot.SlotTransform.position;

        return Vector3.zero;
    }

}
