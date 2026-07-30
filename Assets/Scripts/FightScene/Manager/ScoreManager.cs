using System;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    // ============================================================
    // 得分來源
    // 後續可以依照不同操作套用不同倍率
    // ============================================================
    public enum ScoreActionType
    {
        NormalAttack,
        HeavyAttack,
        Block,
        Skill,
        Fever,
        Other
    }

    // ============================================================
    // Inspector：基礎分數
    // ============================================================
    [Header("節拍判定基礎分數")]
    [SerializeField] private int perfectBaseScore = 100;
    [SerializeField] private int greatBaseScore = 70;

    // ============================================================
    // Inspector：不同操作的倍率
    // 第一版可以全部先維持 1
    // ============================================================
    [Header("操作類型倍率")]
    [SerializeField] private float normalAttackMultiplier = 1f;
    [SerializeField] private float heavyAttackMultiplier = 1f;
    [SerializeField] private float blockMultiplier = 1f;
    [SerializeField] private float skillMultiplier = 1f;
    [SerializeField] private float feverMultiplier = 1f;

    // ============================================================
    // Inspector：Combo 倍率
    // 每達到 comboStep 次 Combo，增加 comboBonusPerStep 倍率
    //
    // 例如：
    // comboStep = 10
    // comboBonusPerStep = 0.1
    //
    // Combo 0～9   = 1.0倍
    // Combo 10～19 = 1.1倍
    // Combo 20～29 = 1.2倍
    // ============================================================
    [Header("Combo 倍率")]
    [SerializeField] private int comboStep = 10;
    [SerializeField] private float comboBonusPerStep = 0.1f;
    [SerializeField] private float maxComboMultiplier = 3f;

    // ============================================================
    // 執行時資料
    // ============================================================
    [Header("目前分數資料")]
    [SerializeField] private int currentScore = 0;
    [SerializeField] private int perfectCount = 0;
    [SerializeField] private int greatCount = 0;

    // ============================================================
    // UI 或其他系統可以訂閱的事件
    // ============================================================

    /// <summary>
    /// 總分發生變化。
    /// 參數：目前總分。
    /// </summary>
    public event Action<int> OnScoreChanged;

    /// <summary>
    /// 成功獲得一次分數。
    ///
    /// 參數：
    /// 1. 本次增加分數
    /// 2. 增加後的總分
    /// 3. 節拍判定
    /// 4. 得分來源
    /// </summary>
    public event Action<
        int,
        int,
        FMODBeatListener2.Judge,
        ScoreActionType
    > OnScoreAdded;

    /// <summary>
    /// 分數被重置。
    /// </summary>
    public event Action OnScoreReset;

    // ============================================================
    // 對外讀取
    // ============================================================
    public int CurrentScore => currentScore;
    public int PerfectCount => perfectCount;
    public int GreatCount => greatCount;

    // ============================================================
    // Unity 生命週期
    // ============================================================
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "[ScoreManager] 場景中存在重複的 ScoreManager，已刪除重複物件。"
            );

            Destroy(gameObject);
            return;
        }

        Instance = this;

        // ScoreManager 不使用 DontDestroyOnLoad。
        // 每次重新進入戰鬥場景，都會建立新的分數資料。
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ============================================================
    // 主要加分入口
    // ============================================================

    /// <summary>
    /// 根據判定、操作類型與 Combo 計算並加入分數。
    /// </summary>
    public int AddScore(
        FMODBeatListener2.Judge judge,
        ScoreActionType actionType,
        int comboCount
    )
    {
        // --------------------------------------------------------
        // 1. 取得判定基礎分數
        // --------------------------------------------------------
        int baseScore = GetJudgeBaseScore(judge);

        // Miss 或無效判定不會得分
        if (baseScore <= 0)
        {
            Debug.Log(
                $"[ScoreManager] 判定={judge}，本次不增加分數。"
            );

            return 0;
        }

        // --------------------------------------------------------
        // 2. 取得操作倍率
        // --------------------------------------------------------
        float actionMultiplier =
            GetActionMultiplier(actionType);

        // --------------------------------------------------------
        // 3. 取得 Combo 倍率
        // --------------------------------------------------------
        float comboMultiplier =
            GetComboMultiplier(comboCount);

        // --------------------------------------------------------
        // 4. 計算最終分數
        // --------------------------------------------------------
        float calculatedScore =
            baseScore *
            actionMultiplier *
            comboMultiplier;

        int addedScore =
            Mathf.Max(
                0,
                Mathf.RoundToInt(calculatedScore)
            );

        // --------------------------------------------------------
        // 5. 累積判定次數
        // --------------------------------------------------------
        if (judge == FMODBeatListener2.Judge.Perfect)
        {
            perfectCount++;
        }
        else if (judge == FMODBeatListener2.Judge.Great)
        {
            greatCount++;
        }

        // --------------------------------------------------------
        // 6. 增加總分
        // --------------------------------------------------------
        currentScore += addedScore;

        // --------------------------------------------------------
        // 7. 通知 UI 與其他系統
        // --------------------------------------------------------
        OnScoreAdded?.Invoke(
            addedScore,
            currentScore,
            judge,
            actionType
        );

        OnScoreChanged?.Invoke(currentScore);

        Debug.Log(
            $"[ScoreManager] " +
            $"類型={actionType} | " +
            $"判定={judge} | " +
            $"基礎分={baseScore} | " +
            $"操作倍率={actionMultiplier:0.00} | " +
            $"Combo={comboCount} | " +
            $"Combo倍率={comboMultiplier:0.00} | " +
            $"獲得={addedScore} | " +
            $"總分={currentScore}"
        );

        return addedScore;
    }

    /// <summary>
    /// 不使用 Combo 時的簡易加分入口。
    /// Combo 預設為 0。
    /// </summary>
    public int AddScore(
        FMODBeatListener2.Judge judge,
        ScoreActionType actionType
    )
    {
        return AddScore(
            judge,
            actionType,
            0
        );
    }

    // ============================================================
    // 基礎分數
    // ============================================================

    private int GetJudgeBaseScore(
        FMODBeatListener2.Judge judge
    )
    {
        switch (judge)
        {
            case FMODBeatListener2.Judge.Perfect:
                return perfectBaseScore;

            case FMODBeatListener2.Judge.Great:
                return greatBaseScore;

            default:
                return 0;
        }
    }

    // ============================================================
    // 操作倍率
    // ============================================================

    private float GetActionMultiplier(
        ScoreActionType actionType
    )
    {
        switch (actionType)
        {
            case ScoreActionType.NormalAttack:
                return normalAttackMultiplier;

            case ScoreActionType.HeavyAttack:
                return heavyAttackMultiplier;

            case ScoreActionType.Block:
                return blockMultiplier;

            case ScoreActionType.Skill:
                return skillMultiplier;

            case ScoreActionType.Fever:
                return feverMultiplier;

            default:
                return 1f;
        }
    }

    // ============================================================
    // Combo 倍率
    // ============================================================

    public float GetComboMultiplier(int comboCount)
    {
        if (comboCount <= 0)
        {
            return 1f;
        }

        // 避免 Inspector 將 comboStep 設為 0
        int safeComboStep =
            Mathf.Max(1, comboStep);

        int reachedSteps =
            comboCount / safeComboStep;

        float multiplier =
            1f +
            reachedSteps *
            comboBonusPerStep;

        return Mathf.Clamp(
            multiplier,
            1f,
            Mathf.Max(1f, maxComboMultiplier)
        );
    }

    // ============================================================
    // 重置
    // ============================================================

    public void ResetScore()
    {
        currentScore = 0;
        perfectCount = 0;
        greatCount = 0;

        OnScoreReset?.Invoke();
        OnScoreChanged?.Invoke(currentScore);

        Debug.Log(
            "[ScoreManager] 分數資料已重置。"
        );
    }

    // ============================================================
    // 測試用途
    // 可以在 Inspector 元件右上角選單執行
    // 正式接入攻擊後可以保留或移除
    // ============================================================

    [ContextMenu("Test/Add Perfect Normal Attack")]
    private void TestAddPerfectNormalAttack()
    {
        AddScore(
            FMODBeatListener2.Judge.Perfect,
            ScoreActionType.NormalAttack,
            0
        );
    }

    [ContextMenu("Test/Add Great Normal Attack")]
    private void TestAddGreatNormalAttack()
    {
        AddScore(
            FMODBeatListener2.Judge.Great,
            ScoreActionType.NormalAttack,
            0
        );
    }

    [ContextMenu("Test/Reset Score")]
    private void TestResetScore()
    {
        ResetScore();
    }
}