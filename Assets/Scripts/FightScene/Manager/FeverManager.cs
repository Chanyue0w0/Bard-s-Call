using UnityEngine;

public class FeverManager : MonoBehaviour
{
    public static FeverManager Instance { get; private set; }

    [Header("Fever 參數設定")]
    [Range(0f, 100f)] public float currentFever = 0f;
    public float feverMax = 100f;

    [Header("累積參數")]
    public float gainPerPerfect = 1.12f;    // 每次 Perfect 增加值
    public float bonusPerBar = 0.5f;        // 小節全Perfect獎勵
    public float missPenalty = 8f;          // Miss 扣除值

    [Header("拍點判定設定")]
    public int beatsPerBar = 4;             // 每小節拍數
    private int perfectCountInBar = 0;      // 當前小節的Perfect計數

    [Header("關聯 UI 元件")]
    public FeverUI feverUI;                 // 指向你的 FeverUI 腳本
    private bool feverTriggered = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (feverUI == null)
            feverUI = FindObjectOfType<FeverUI>();
        UpdateFeverUI();
    }

    // --------------------------------------------------
    // 外部呼叫接口
    // --------------------------------------------------

    // Perfect 命中
    public void AddPerfect()
    {
        currentFever += gainPerPerfect;
        perfectCountInBar++;

        // 小節結算
        if (perfectCountInBar >= beatsPerBar)
        {
            // 若該小節全Perfect → 額外加成
            currentFever += bonusPerBar;
            perfectCountInBar = 0; // 重置小節計數
        }

        CheckFeverLimit();
        UpdateFeverUI();
    }

    // 錯誤打擊（Miss）
    public void AddMiss()
    {
        currentFever -= missPenalty;
        perfectCountInBar = 0; // 該小節獎勵失效
        currentFever = Mathf.Max(currentFever, 0f);

        UpdateFeverUI();
    }

    // --------------------------------------------------
    // Fever 狀態檢查與觸發
    // --------------------------------------------------
    private void CheckFeverLimit()
    {
        if (currentFever >= feverMax && !feverTriggered)
        {
            currentFever = feverMax;
            feverTriggered = true;
            TriggerFever();
        }
    }

    private void TriggerFever()
    {
        Debug.Log("FEVER TRIGGERED!");
        // 這裡可加入全隊大招或演出事件
        // 例如呼叫 BattleManager.TriggerTeamUltimate();
    }

    // --------------------------------------------------
    // UI 更新
    // --------------------------------------------------
    private void UpdateFeverUI()
    {
        if (feverUI != null)
        {
            float normalized = Mathf.Clamp01(currentFever / feverMax);
            feverUI.SetFeverValue(normalized);
        }
    }

    // 外部可重置（例如戰鬥結束或施放完全隊大招後）
    public void ResetFever()
    {
        currentFever = 0f;
        perfectCountInBar = 0;
        feverTriggered = false;
        UpdateFeverUI();
    }
}
