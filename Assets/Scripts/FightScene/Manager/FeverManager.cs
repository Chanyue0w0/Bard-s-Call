using UnityEngine;
using System.Collections;

public class FeverManager : MonoBehaviour
{
    public static FeverManager Instance { get; private set; }

    [Header("Fever 參數設定")]
    [Range(0f, 100f)] public float currentFever = 0f;
    public float feverMax = 100f;

    [Header("累積參數")]
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
    // Fever 大招動畫流程（鏡頭 + 背景 + 角色行動）
    // --------------------------------------------------
    public IEnumerator HandleFeverUltimateSequence()
    {
        Debug.Log("[FeverManager] 啟動全隊大招動畫流程");

        // 歸零防重觸發
        currentFever = 0f;
        feverTriggered = false;
        UpdateFeverUI();

        if (feverUltBackground != null)
            feverUltBackground.SetActive(true);

        // 鏡頭聚焦
        if (mainCam != null && focusPoint != null)
            yield return StartCoroutine(CameraFocusZoom(true));

        // 生成聚氣特效（第1拍期間）
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
                    Destroy(vfx, 3f);
                }
            }
        }

        // 拍點節奏計算
        float secondsPerBeat = (BeatManager.Instance != null)
            ? (60f / BeatManager.Instance.bpm)
            : 0.6f;

        // ★ 第1拍：聚焦中
        yield return new WaitForSeconds(secondsPerBeat * 1f);

        // ★ 第2拍：開始鏡頭回復
        if (mainCam != null)
            StartCoroutine(CameraFocusZoom(false));

        // ★ 第3拍：全隊施放大招
        yield return new WaitForSeconds(secondsPerBeat * 1f);
        if (BattleManager.Instance != null)
        {
            Debug.Log("[FeverManager] 第3拍：全隊施放大招！");
            BattleManager.Instance.TriggerFeverActions(phase: 3);
        }

        // ★ 第4~第8拍：維持背景（收尾氣氛）
        yield return new WaitForSeconds(secondsPerBeat * 5f);
        if (feverUltBackground != null)
            feverUltBackground.SetActive(false);

        Debug.Log("[FeverManager] 大招動畫結束。");
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
}
