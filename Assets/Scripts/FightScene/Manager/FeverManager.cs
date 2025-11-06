using UnityEngine;
using System.Collections;

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
    public int beatsPerBar = 4;
    private int perfectCountInBar = 0;

    [Header("關聯 UI 元件")]
    public FeverUI feverUI;
    private bool feverTriggered = false;

    // --------------------------------------------------
    // Fever 大招動畫控制
    // --------------------------------------------------
    [Header("Fever 大招演出參數")]
    public GameObject feverUltBackground;   // Fever 背景 UI
    public Transform focusPoint;            // 聚焦目標（角色或中央）
    public float cameraZoomScale = 0.5f;    // 鏡頭縮放比例
    public float zoomDuration = 0.3f;       // 進出縮放時間
    public float holdBeats = 4f;            // 持續拍數（4拍）
    private Camera mainCam;
    private float originalSize;
    private Vector3 originalCamPos;
    [Header("Fever 大招特效")]
    public GameObject ultFocusVFXPrefab; // ★ 新增：全隊大招聚氣特效


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
    // Perfect / Miss 累積邏輯
    // --------------------------------------------------
    public void AddPerfect()
    {
        currentFever += gainPerPerfect;
        perfectCountInBar++;

        if (perfectCountInBar >= beatsPerBar)
        {
            currentFever += bonusPerBar;
            perfectCountInBar = 0;
        }

        CheckFeverLimit();
        UpdateFeverUI();
    }

    public void AddMiss()
    {
        currentFever -= missPenalty;
        perfectCountInBar = 0;
        currentFever = Mathf.Max(currentFever, 0f);
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
    // Fever 大招動畫流程（鏡頭 + 背景）
    // --------------------------------------------------
    public IEnumerator HandleFeverUltimateSequence()
    {
        Debug.Log("[FeverManager] 啟動全隊大招動畫流程");

        // ★ 立即歸零 Fever，防止重複觸發
        currentFever = 0f;
        feverTriggered = false;
        UpdateFeverUI();

        if (feverUltBackground != null)
            feverUltBackground.SetActive(true);

        // 鏡頭縮放聚焦
        if (mainCam != null && focusPoint != null)
            yield return StartCoroutine(CameraFocusZoom(true));

        // ★ 生成三位玩家的 Ult 聚氣特效
        if (ultFocusVFXPrefab != null && BattleManager.Instance != null)
        {
            var bm = BattleManager.Instance;
            for (int i = 0; i < bm.CTeamInfo.Length; i++)
            {
                var slot = bm.CTeamInfo[i];
                if (slot != null && slot.Actor != null)
                {
                    Transform actorTrans = slot.Actor.transform;
                    Vector3 pos = actorTrans.position + new Vector3(0f, 0.5f, 0f);
                    GameObject vfx = GameObject.Instantiate(ultFocusVFXPrefab, pos, Quaternion.identity);
                    GameObject.Destroy(vfx, 3f);
                }
            }
        }

        // ----------------------------
        // 計算節奏時間
        // ----------------------------
        float secondsPerBeat = (BeatManager.Instance != null)
            ? (60f / BeatManager.Instance.bpm)
            : 0.6f;

        float focusDuration = secondsPerBeat * 2f; // 鏡頭聚焦持續2拍
        float bgDuration = secondsPerBeat * 4f;    // 背景持續4拍

        // ----------------------------
        // 1. 鏡頭持續2拍 → 回復
        // ----------------------------
        yield return new WaitForSeconds(focusDuration);

        if (mainCam != null)
            yield return StartCoroutine(CameraFocusZoom(false));

        // ----------------------------
        // 2. 背景再多留2拍後關閉
        // ----------------------------
        float remainDuration = bgDuration - focusDuration;
        if (remainDuration > 0f)
            yield return new WaitForSeconds(remainDuration);

        if (feverUltBackground != null)
            feverUltBackground.SetActive(false);

        Debug.Log("[FeverManager] 大招動畫結束，可進入全隊施放階段。");
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

    // --------------------------------------------------
    // UI 更新與重置
    // --------------------------------------------------
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
}
