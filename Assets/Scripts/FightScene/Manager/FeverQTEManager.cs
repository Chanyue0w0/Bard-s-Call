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

        // ★ 先停止所有滑動動畫
        StopAllCoroutines();
        isSliding = false;

        // ★ 再清除所有 QTE
        ClearAllQTE();
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

        if (hitExplosionVFX != null && activeQTEs[0] != null)
        {
            Vector3 pos = activeQTEs[0].transform.position;
            GameObject fx = Instantiate(hitExplosionVFX, pos, Quaternion.identity);
            Destroy(fx, 1.5f);
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
        ReapplyAlpha();

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

        if (lastType == '?')
        {
            lastType = pool[Random.Range(0, pool.Length)];
            repeatCount = 1;
            return lastType;
        }

        float continueProb = Mathf.Max(90f - (repeatCount - 1) * 10f, 30f) / 100f;

        if (Random.value < continueProb)
        {
            repeatCount++;
            return lastType;
        }
        else
        {
            List<char> others = new List<char>(pool);
            others.Remove(lastType);

            char newType = others[Random.Range(0, others.Count)];
            lastType = newType;
            repeatCount = 1;

            return newType;
        }
    }
}
