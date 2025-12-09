using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FeverQTEManager : MonoBehaviour
{
    public static FeverQTEManager Instance;

    [Header("QTE Prefabs (SpriteRenderer)")]
    public GameObject prefabA;
    public GameObject prefabB;
    public GameObject prefabX;
    public GameObject prefabY;

    [Header("VFX")]
    public GameObject spawnImpactVFX;
    public GameObject hitExplosionVFX;   // ★ 新增：打擊爆炸特效

    [Header("Spawn Points")]
    public Transform spawnPoint;
    public Transform endPoint;
    public int qteCount = 5;
    public int preGenerateCount = 10;

    [Header("Slide Settings")]
    public float slideDuration = 0.15f; // 固定滑動時間（秒）
    [Header("Slide Speed Control")]
    public float slideDurationMax = 0.25f;   // 開場最慢
    public float slideDurationMin = 0.08f;   // 加到最極限的速度
    public float slideAcceleration = 0.015f; // 每次滑動加速量

    private float currentSlideDuration;      // 目前使用的速度


    private List<GameObject> activeQTEs = new List<GameObject>();
    private List<char> qteTypes = new List<char>();

    private Queue<char> bufferQueue = new Queue<char>();

    private int repeatCount = 0;
    private char lastType = '?';

    private float[] alphas = new float[] { 1f, 1f, 0.7f, 0.4f, 0.2f };

    private bool isFeverActive = false;
    private bool isSliding = false;

    [Header("Slide-in Offset")]
    public float spawnXOffset = -1f;   // 新QTE從 spawnPoint 往左偏移多少

    private int streakCount = 0;  // 目前連續數量

    [Header("Combo UI")]
    public GameObject qteComboTextObj;     // 指向 UI Text
    public UnityEngine.UI.Text qteComboText;     // 指向 UI Text
    public float comboPulseScale = 1.2f;         // 放大倍率
    public float comboPulseDuration = 0.15f;     // 放大時間
    public float comboShakeAmount = 4f;          // 震動幅度(px)

    private int qteComboCount = 0;               // 目前成功數量
    private Coroutine comboPulseRoutine;
    private Vector3 comboOriginalScale;
    private Vector3 comboOriginalPos;


    private void Awake()
    {
        Instance = this;
    }

    // ========================================================================
    // Fever 開始（第 9 拍）
    // ========================================================================
    public void StartQTE()
    {
        isFeverActive = true;

        qteComboTextObj.SetActive(true);
        qteComboCount = 0;
        if (qteComboText != null)
            qteComboText.text = "x0";

        ClearAllQTE();
        PreGenerateBuffer();

        // ★ 重設速度到最慢（剛開始很慢）
        currentSlideDuration = slideDurationMax;

        GenerateInitialActive();
    }

    // ========================================================================
    // Fever 結束（33 拍）
    // ========================================================================
    public void EndQTE()
    {
        isFeverActive = false;


        qteComboTextObj.SetActive(false);
        qteComboCount = 0;
        if (qteComboText != null)
            qteComboText.text = "x0";

        // ★ 先停止所有滑動動畫
        StopAllCoroutines();
        isSliding = false;

        // ★ 再清除所有 QTE
        ClearAllQTE();
    }

    public int GetQTEComboCount()
    {
        return qteComboCount;
    }

    // ========================================================================
    // 預先生成 10 顆
    // ========================================================================
    private void PreGenerateBuffer()
    {
        bufferQueue.Clear();
        for (int i = 0; i < preGenerateCount; i++)
            bufferQueue.Enqueue(GenerateNextType());
    }

    // ========================================================================
    // 初始化 5 顆
    // ========================================================================
    private void GenerateInitialActive()
    {
        for (int i = 0; i < qteCount; i++)
        {
            if (bufferQueue.Count == 0)
                PreGenerateBuffer();

            char t = bufferQueue.Dequeue();
            SpawnOneQTE(t);
        }

        // ★ 不要瞬間定位，而是讓五顆滑進來
        StartCoroutine(SlideAllQTEs());
    }


    // ========================================================================
    // 生成 QTE 不設定位置（稍後再滑動）
    // ========================================================================
    private void SpawnOneQTE(char type)
    {
        GameObject prefab = GetPrefab(type);
        if (prefab == null) return;

        GameObject obj = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        obj.transform.SetParent(this.transform);

        activeQTEs.Add(obj);
        qteTypes.Add(type);

        // ★ 初始化位置：從 spawnPoint.x + offset（向左）開始
        float startX = spawnPoint.position.x + spawnXOffset;
        Vector3 startPos = new Vector3(startX, spawnPoint.position.y, spawnPoint.position.z);

        obj.transform.position = startPos;

        // ★★★ 生成 Impact VFX ★★★
        if (spawnImpactVFX != null)
        {
            GameObject vfx = Instantiate(spawnImpactVFX, startPos, Quaternion.identity);
            Destroy(vfx, 1.5f); // 依你的特效時長調整
        }
    }


    private GameObject GetPrefab(char t)
    {
        switch (t)
        {
            case 'A': return prefabA;
            case 'B': return prefabB;
            case 'X': return prefabX;
            case 'Y': return prefabY;
        }
        return null;
    }

    // ========================================================================
    // 玩家打擊
    // ========================================================================
    public void OnPlayerHit(char inputType)
    {
        if (!isFeverActive) return;
        if (activeQTEs.Count == 0) return;
        if (isSliding) return; // 正在滑動時禁止輸入

        char expected = qteTypes[0];
        if (inputType != expected)
        {
            Debug.Log($"[QTE] 錯誤輸入，期待 {expected}，收到 {inputType}");
            return;
        }

        // ★★★ 加入 QTEHit 震動 ★★★
        VibrationManager.Instance?.Vibrate("QTEHit");

        FMODBeatListener2.Instance.PlayPerfectSFX();

        if (hitExplosionVFX != null && activeQTEs[0] != null)
        {
            Vector3 pos = activeQTEs[0].transform.position;
            GameObject fx = Instantiate(hitExplosionVFX, pos, Quaternion.identity);
            Destroy(fx, 1.5f);
        }

        // ★ 成功 Combo
        qteComboCount++;
        if (qteComboText != null)
        {
            qteComboText.text = "x" + qteComboCount;
            StartComboPulse();
        }

        // 刪除第一顆
        Destroy(activeQTEs[0]);
        activeQTEs.RemoveAt(0);
        qteTypes.RemoveAt(0);

        // 從 bufferQueue 補
        if (bufferQueue.Count == 0)
            PreGenerateBuffer();

        char next = bufferQueue.Dequeue();
        SpawnOneQTE(next);

        // 滑動 + 更新透明度
        StartCoroutine(SlideAllQTEs());
    }

    // ========================================================================
    // 滑動動畫（所有 QTE）
    // ========================================================================
    private IEnumerator SlideAllQTEs()
    {
        isSliding = true;

        Vector3[] startPos = new Vector3[activeQTEs.Count];
        Vector3[] endPos = new Vector3[activeQTEs.Count];

        for (int i = 0; i < activeQTEs.Count; i++)
        {
            startPos[i] = activeQTEs[i].transform.position;

            float t = i / (float)(qteCount - 1);
            endPos[i] = Vector3.Lerp(endPoint.position, spawnPoint.position, t);
        }

        float duration = currentSlideDuration;   // ★ 改用動態速度
        float time = 0;

        while (time < duration)
        {
            float lerp = time / duration;

            for (int i = 0; i < activeQTEs.Count; i++)
            {
                if (activeQTEs[i] != null)
                    activeQTEs[i].transform.position = Vector3.Lerp(startPos[i], endPos[i], lerp);
            }

            time += Time.deltaTime;
            yield return null;
        }

        RepositionAllImmediate();
        //ReapplyAlpha();

        // ★ 加速（越滑越快）
        currentSlideDuration = Mathf.Max(slideDurationMin, currentSlideDuration - slideAcceleration);

        isSliding = false;
    }


    // ========================================================================
    // 立即更新位置（初始化或滑動結束用）
    // ========================================================================
    private void RepositionAllImmediate()
    {
        for (int i = 0; i < activeQTEs.Count; i++)
        {
            float t = i / (float)(qteCount - 1);
            activeQTEs[i].transform.position = Vector3.Lerp(endPoint.position, spawnPoint.position, t);
        }
    }

    private void StartComboPulse()
    {
        if (qteComboText == null) return;

        if (comboOriginalScale == Vector3.zero)
            comboOriginalScale = qteComboText.transform.localScale;

        if (comboOriginalPos == Vector3.zero)
            comboOriginalPos = qteComboText.transform.localPosition;

        if (comboPulseRoutine != null)
            StopCoroutine(comboPulseRoutine);

        comboPulseRoutine = StartCoroutine(ComboPulseRoutine());
    }

    private IEnumerator ComboPulseRoutine()
    {
        float t = 0f;
        float dur = comboPulseDuration;

        // 目標放大
        Vector3 targetScale = comboOriginalScale * comboPulseScale;

        while (t < dur)
        {
            t += Time.deltaTime;
            float lerp = t / dur;

            // 放大動畫
            qteComboText.transform.localScale = Vector3.Lerp(comboOriginalScale, targetScale, lerp);

            // 微震動
            float shakeX = Mathf.Sin(Time.time * 60f) * comboShakeAmount;
            float shakeY = Mathf.Cos(Time.time * 50f) * comboShakeAmount;
            qteComboText.transform.localPosition = comboOriginalPos + new Vector3(shakeX, shakeY, 0) * 0.02f;

            yield return null;
        }

        // 回到正常
        qteComboText.transform.localScale = comboOriginalScale;
        qteComboText.transform.localPosition = comboOriginalPos;
    }


    // ========================================================================
    // 更新透明度
    // ========================================================================
    private void ReapplyAlpha()
    {
        for (int i = 0; i < activeQTEs.Count; i++)
        {
            SpriteRenderer sr = activeQTEs[i].GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                float a = (i < alphas.Length) ? alphas[i] : alphas[alphas.Length - 1];
                Color c = sr.color;
                sr.color = new Color(c.r, c.g, c.b, a);
            }
        }
    }

    // ========================================================================
    // 清除全部
    // ========================================================================
    public void ClearAllQTE()
    {
        // ★ 逐一安全刪除
        for (int i = 0; i < activeQTEs.Count; i++)
        {
            if (activeQTEs[i] != null)
                Destroy(activeQTEs[i]);
        }

        activeQTEs.Clear();
        qteTypes.Clear();
        bufferQueue.Clear();

        lastType = '?';
        repeatCount = 0;
    }


    // ========================================================================
    // 連鎖機率衰減
    // ========================================================================
    private char GenerateNextType()
    {
        char[] pool = new char[] { 'A', 'B', 'X', 'Y' };

        // 第一次生成
        if (lastType == '?')
        {
            lastType = pool[Random.Range(0, pool.Length)];
            streakCount = 1;
            return lastType;
        }

        // --------
        // 前 3 連擊：強制相同
        // --------
        if (streakCount < 3)
        {
            streakCount++;
            return lastType;
        }

        // --------
        // 第 4 顆開始：機率延續
        // --------
        int indexAfter3 = streakCount - 3;   // 第幾顆可變動 (1 = 第4顆)
        float continueProb = Mathf.Clamp(90f - (indexAfter3 - 1) * 10f, 10f, 90f) / 100f;

        if (Random.value < continueProb)
        {
            // 繼續同種
            streakCount++;
            return lastType;
        }

        // --------
        // 否則更換種類
        // --------
        List<char> others = new List<char>(pool);
        others.Remove(lastType);

        char newType = others[Random.Range(0, others.Count)];
        lastType = newType;
        streakCount = 1;

        return lastType;
    }

}
