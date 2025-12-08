using UnityEngine;
using System.Collections;

public class FeverManager : MonoBehaviour
{
    public static FeverManager Instance { get; private set; }

    [Header("Fever 參數設定")]
    [Range(0f, 100f)] public float currentFever = 0f;
    public float feverMax = 100f;

    [Header("累積參數 (舊參數仍保留，不使用移除)")]
    public float gainPerPerfect = 1.12f;
    public float bonusPerBar = 0.5f;
    public float missPenalty = 8f;

    [Header("拍點判定設定")]
    public int beatsPerBar = 4;
    private int perfectCountInBar = 0;

    [Header("關聯 UI 元件")]
    public FeverUI feverUI;
    private bool feverTriggered = false;

    // --------------------------------------------------
    // ★★★ Fever 週期管理 ★★★
    // --------------------------------------------------
    private bool isFeverActive = false;
    private int feverBeatCounter = 0;

    public static event System.Action OnFeverEnd;

    // --------------------------------------------------
    // ★★★ 新增：新版 Fever 累加設定（Combo 加成）★★★
    // --------------------------------------------------
    [Header("新版 Fever 累加設定")]
    public float baseGain = 0.70f;        // 每次基本累加量
    public float comboFactor = 0.60f;     // Combo 影響最大增幅
    public int comboMax = 50;             // 50 Combo 達到滿倍率

    private int CurrentCombo
    {
        get
        {
            if (FMODBeatListener2.Instance == null)
                return 0;
            return FMODBeatListener2.Instance.GetComboCount(); // 來自 Listener2
        }
    }

    // --------------------------------------------------
    // ★★★ 新增：Fever 音樂控制參數 ★★★
    // --------------------------------------------------
    [Header("Fever 音樂控制")]
    [Tooltip("FMOD Global Parameter 名稱，用於淡入淡出主 BGM")]
    public string fadeParameterName = "Fade";

    [Tooltip("整個 Fever 持續拍數（目前僅備註，不強制控制流程邏輯）")]
    public int feverTotalBeats = 33;

    [Tooltip("開始 Fever 時，主 BGM 淡出所需拍數")]
    public int feverFadeOutBeats = 1;

    [Tooltip("從 Fever 開始後，第幾拍開始淡入主 BGM")]
    public int feverFadeInStartBeat = 29;

    [Tooltip("主 BGM 淡入所需拍數（例如 4 拍從 0 慢慢拉回 1）")]
    public int feverFadeInBeats = 4;

    // --------------------------------------------------
    // Fever 大招動畫控制
    // --------------------------------------------------
    [Header("Fever 大招演出參數")]
    public GameObject feverUltBackground;
    public Transform focusPoint;
    public float cameraZoomScale = 0.5f;
    public float zoomDuration = 0.3f;
    public float holdBeats = 4f;
    private Camera mainCam;
    private float originalSize;
    private Vector3 originalCamPos;

    [Header("Fever 大招特效")]
    public GameObject ultFocusVFXPrefab;

    public static event System.Action<int> OnFeverUltStart; // 參數為持續拍數


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

        mainCam = Camera.main;
        if (mainCam != null)
        {
            originalSize = mainCam.orthographicSize;
            originalCamPos = mainCam.transform.position;
        }

        UpdateFeverUI();
    }

    // --------------------------------------------------
    // ★★★ Perfect / Miss 累積邏輯（新版）★★★
    // --------------------------------------------------
    public void AddPerfect()
    {
        int combo = CurrentCombo;

        float comboRate = Mathf.Clamp01(combo / (float)comboMax);
        float gain = baseGain + comboFactor * comboRate;

        currentFever += gain;
        perfectCountInBar++;

        if (perfectCountInBar >= beatsPerBar)
        {
            currentFever += bonusPerBar;
            perfectCountInBar = 0;
        }

        CheckFeverLimit();
        UpdateFeverUI();
    }

    // ★ Miss 不扣 Fever
    public void AddMiss()
    {
        perfectCountInBar = 0;    // 清除小節 Perfect 數
        UpdateFeverUI();
    }

    private void CheckFeverLimit()
    {
        if (currentFever >= feverMax && !feverTriggered)
        {
            currentFever = feverMax;
            feverTriggered = true;
            Debug.Log("[FeverManager] Fever已滿，可使用大招。");
        }
    }

    // --------------------------------------------------
    // Fever 大招動畫流程（鏡頭 + 背景 + 角色行動）
    // --------------------------------------------------
    public IEnumerator HandleFeverUltimateSequence()
    {
        Debug.Log("[FeverManager] 啟動全隊大招動畫流程");

        // ★ 啟動 33 拍生命週期
        isFeverActive = true;
        feverBeatCounter = 0;
        FMODBeatListener2.OnGlobalBeat += TickFeverLife;

        // 1. 一啟動 Fever 就先處理音樂
        //    - 立刻播放 Fever 專用音樂
        //    - 同時讓主 BGM 依照拍點淡出 / 淡入
        if (FMODAudioPlayer.Instance != null)
        {
            FMODAudioPlayer.Instance.PlayFeverMusic();
        }
        StartCoroutine(FeverMusicRoutine());

        // 2. 原本 Fever 數值歸零
        currentFever = 0f;
        feverTriggered = false;
        UpdateFeverUI();

        // 3. 通知敵人進入 Fever 鎖定狀態（暫時維持 12 拍，之後你要改 33 我們再動）
        OnFeverUltStart?.Invoke(feverTotalBeats);
        Debug.Log("[FeverManager] 已通知所有敵人進入Fever鎖定狀態（12拍）");

        // ★ 改成：啟動黑幕協程（整段 Fever 週期管理）
        StartCoroutine(HandleFeverBlackout());

        if (mainCam != null && focusPoint != null)
            yield return StartCoroutine(CameraFocusZoom(true));

        if (ultFocusVFXPrefab != null && BattleManager.Instance != null)
        {
            var bm = BattleManager.Instance;
            foreach (var slot in bm.CTeamInfo)
            {
                if (slot != null && slot.Actor != null)
                {
                    Transform actorTrans = slot.Actor.transform;
                    Vector3 pos = actorTrans.position + new Vector3(0f, 0.5f, 0f);
                    GameObject vfx = Instantiate(ultFocusVFXPrefab, pos, Quaternion.identity);
                    vfx.transform.SetParent(actorTrans, worldPositionStays: true);

                    Destroy(vfx, 3f);
                }
            }
        }

        float secondsPerBeat = (BeatManager.Instance != null)
            ? (60f / BeatManager.Instance.bpm)
            : 0.6f;

        yield return new WaitForSeconds(secondsPerBeat * 1f);

        if (mainCam != null)
            StartCoroutine(CameraFocusZoom(false));

        yield return new WaitForSeconds(secondsPerBeat * 1f);
        if (BattleManager.Instance != null)
        {
            Debug.Log("[FeverManager] 第3拍：全隊施放大招！");
            BattleManager.Instance.TriggerFeverActions(phase: 3);
        }

        Debug.Log("[FeverManager] 大招動畫結束。");
    }

    // --------------------------------------------------
    // ★★★ Fever 黑幕：整段 Fever 期間顯示，結束時關閉 ★★★
    // --------------------------------------------------
    private IEnumerator HandleFeverBlackout()
    {
        if (feverUltBackground != null)
            feverUltBackground.SetActive(true);

        // 黑幕會持續到 Fever 結束
        while (isFeverActive)
            yield return null;

        if (feverUltBackground != null)
            feverUltBackground.SetActive(false);

        Debug.Log("[FeverManager] 黑幕已關閉（Fever 結束）");
    }


    private void TickFeverLife(int beatIndex)
    {
        if (!isFeverActive)
            return;

        feverBeatCounter++;

        // Debug.Log($"[FeverManager] FEVER 拍數：{feverBeatCounter}/{feverTotalBeats}");

        if (feverBeatCounter >= feverTotalBeats)
        {
            EndFever();
        }
    }

    private void EndFever()
    {
        if (!isFeverActive)
            return;

        isFeverActive = false;
        FMODBeatListener2.OnGlobalBeat -= TickFeverLife;

        Debug.Log("[FeverManager] FEVER 已結束（33 拍）");

        OnFeverEnd?.Invoke();

        // ★ 保險再關閉一次黑幕（不會影響協程邏輯）
        if (feverUltBackground != null)
            feverUltBackground.SetActive(false);
    }

    // --------------------------------------------------
    // ★★★ 新增：處理 Fever 期間主 BGM 的 Fade 流程 ★★★
    // --------------------------------------------------
    private IEnumerator FeverMusicRoutine()
    {
        // 一拍秒數：盡量用 FMOD 的 Listener 為準
        float secondsPerBeat = 0.6f;

        if (FMODBeatListener2.Instance != null)
        {
            secondsPerBeat = FMODBeatListener2.Instance.SecondsPerBeat;
        }
        else if (BeatManager.Instance != null)
        {
            secondsPerBeat = 60f / Mathf.Max(1f, BeatManager.Instance.bpm);
        }

        float fadeOutDuration = Mathf.Max(0.0f, feverFadeOutBeats) * secondsPerBeat;
        float fadeInDuration = Mathf.Max(0.0f, feverFadeInBeats) * secondsPerBeat;

        // 1. 開始 Fever 時：主 BGM 1 拍內從 1 → 0
        yield return StartCoroutine(FadeGlobalParameter(
            fadeParameterName,
            1f,
            0f,
            fadeOutDuration
        ));

        Debug.Log("[FeverManager] 主BGM 已在 1 拍內淡出至 0。");

        // 2. 等到第 29 拍再開始淡入
        //    從 Fever 開始算：
        //    - 第 1 拍 花在淡出
        //    - 想在第 feverFadeInStartBeat 拍開始淡入
        //    → 中間要額外等待 (startBeat - 1 - fadeOutBeats) 拍
        int startBeat = Mathf.Max(1, feverFadeInStartBeat);
        int fadeOutBeatsClamped = Mathf.Max(0, feverFadeOutBeats);

        int extraWaitBeats = startBeat - 1 - fadeOutBeatsClamped;
        if (extraWaitBeats > 0)
        {
            yield return new WaitForSeconds(extraWaitBeats * secondsPerBeat);
        }

        // 3. 從第 29 拍開始，在指定拍數內從 0 → 1 淡回主 BGM
        if (fadeInDuration > 0f)
        {
            Debug.Log($"[FeverManager] 從第 {startBeat} 拍開始，在 {feverFadeInBeats} 拍內淡入主BGM。");
            yield return StartCoroutine(FadeGlobalParameter(
                fadeParameterName,
                0f,
                1f,
                fadeInDuration
            ));
        }
        else
        {
            // 若淡入時間設為 0，則直接拉回 1
            FMODUnity.RuntimeManager.StudioSystem.setParameterByName(fadeParameterName, 1f);
        }

        Debug.Log("[FeverManager] Fever 音樂流程：主BGM 已淡回 1。");
    }

    // --------------------------------------------------
    // ★★★ 新增：共用 Global Parameter 淡入淡出工具 ★★★
    // --------------------------------------------------
    private IEnumerator FadeGlobalParameter(string paramName, float from, float to, float duration)
    {
        if (string.IsNullOrEmpty(paramName))
            yield break;

        if (duration <= 0f)
        {
            FMODUnity.RuntimeManager.StudioSystem.setParameterByName(paramName, to);
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration;
            float value = Mathf.Lerp(from, to, t);
            FMODUnity.RuntimeManager.StudioSystem.setParameterByName(paramName, value);
            yield return null;
        }

        // 確保最後收在目標值
        FMODUnity.RuntimeManager.StudioSystem.setParameterByName(paramName, to);
    }

    private IEnumerator CameraFocusZoom(bool zoomIn)
    {
        if (mainCam == null) yield break;

        float startSize = mainCam.orthographicSize;
        float endSize = zoomIn ? startSize * cameraZoomScale : originalSize;

        Vector3 startPos = mainCam.transform.position;
        Vector3 endPos = zoomIn && focusPoint != null
            ? new Vector3(focusPoint.position.x, focusPoint.position.y, startPos.z)
            : originalCamPos;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / zoomDuration;
            mainCam.orthographicSize = Mathf.Lerp(startSize, endSize, t);
            mainCam.transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
    }

    private void UpdateFeverUI()
    {
        if (feverUI != null)
        {
            float normalized = Mathf.Clamp01(currentFever / feverMax);
            feverUI.SetFeverValue(normalized);
        }
    }

    public void ResetFever()
    {
        currentFever = 0f;
        perfectCountInBar = 0;
        feverTriggered = false;
        UpdateFeverUI();
    }

    // --------------------------------------------------
    // ★★★ 對外公開 Fever 狀態（給 EnemyBase 同步用）★★★
    // --------------------------------------------------
    public bool IsFeverActive
    {
        get { return isFeverActive; }
    }

    public int RemainingFeverBeats
    {
        get
        {
            if (!isFeverActive) return 0;

            int remain = feverTotalBeats - feverBeatCounter;
            return Mathf.Max(0, remain);
        }
    }

}
