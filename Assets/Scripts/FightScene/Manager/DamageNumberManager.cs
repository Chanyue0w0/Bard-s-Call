using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageNumberManager : MonoBehaviour
{
    public static DamageNumberManager Instance { get; private set; }

    [Header("數字粒子 Prefab (紅色傷害版 0~9)")]
    public ParticleSystem[] digitPrefabs = new ParticleSystem[10];

    [Header("數字粒子 Prefab (綠色治療版 0~9)")]   // ★ 新增：治療用
    public ParticleSystem[] healDigitPrefabs = new ParticleSystem[10];

    [Header("數字粒子 Prefab (格擋用 0~9)")]  // ★ 新增：格擋數字
    public ParticleSystem[] blockedDigitPrefabs = new ParticleSystem[10];

    // 格擋專用池
    [Header("預熱設定")]
    public int prewarmPerDigit = 8;

    [Header("配置")]
    public float digitSpacing = 0.22f;
    public float groupOffsetY = 1.4f;
    public float groupFloatUp = 0.8f;
    public float groupLifetime = 0.9f;
    public float randomHorizontalJitter = 0.06f;
    public Vector3 groupScale = Vector3.one;

    [Header("排序與圖層")]
    public string sortingLayerName = "Default";
    public int sortingOrder = 20;

    // 內部池：分成紅傷、綠治、藍擋
    private readonly Dictionary<int, Queue<ParticleSystem>> _damagePool = new();
    private readonly Dictionary<int, Queue<ParticleSystem>> _healPool = new();
    private readonly Dictionary<int, Queue<ParticleSystem>> _blockedPool = new();
    private Transform _poolRoot;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _poolRoot = new GameObject("[DamageNumberPool]").transform;
        _poolRoot.SetParent(transform, false);

        // 建立兩組池：傷害與治療
        PrewarmPool(digitPrefabs, _damagePool);
        PrewarmPool(healDigitPrefabs, _healPool);
        PrewarmPool(blockedDigitPrefabs, _blockedPool);
    }

    private void PrewarmPool(ParticleSystem[] prefabs, Dictionary<int, Queue<ParticleSystem>> pool)
    {
        for (int d = 0; d <= 9; d++)
        {
            pool[d] = new Queue<ParticleSystem>();
            if (prefabs == null || prefabs[d] == null) continue;

            for (int i = 0; i < prewarmPerDigit; i++)
            {
                var ps = Instantiate(prefabs[d], _poolRoot);
                PreparePSRenderer(ps);
                ps.gameObject.SetActive(false);
                pool[d].Enqueue(ps);
            }
        }
    }

    private void PreparePSRenderer(ParticleSystem ps)
    {
        var r = ps.GetComponent<ParticleSystemRenderer>();
        if (r != null)
        {
            r.sortingLayerName = sortingLayerName;
            r.sortingOrder = sortingOrder;
        }
    }

    // 通用：從指定池取用
    private ParticleSystem RentDigitPS(int digit, Dictionary<int, Queue<ParticleSystem>> pool, ParticleSystem[] prefabs)
    {
        if (!pool.ContainsKey(digit)) pool[digit] = new Queue<ParticleSystem>();

        if (pool[digit].Count > 0)
        {
            var ps = pool[digit].Dequeue();
            ps.gameObject.SetActive(true);
            return ps;
        }
        else
        {
            var prefab = prefabs[digit];
            var ps = Instantiate(prefab, _poolRoot);
            PreparePSRenderer(ps);
            return ps;
        }
    }

    private void ReturnDigitPS(int digit, ParticleSystem ps, Dictionary<int, Queue<ParticleSystem>> pool)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.transform.SetParent(_poolRoot, false);
        ps.gameObject.SetActive(false);
        pool[digit].Enqueue(ps);
    }

    // ===============================
    // 顯示傷害數字（紅）
    // ===============================
    public void ShowDamage(Transform target, int value)
    {
        ShowNumber(target, value, _damagePool, digitPrefabs, "DamageNumberGroup");
    }

    // ===============================
    // 顯示治療數字（綠）
    // ===============================
    public void ShowHeal(Transform target, int value)
    {
        ShowNumber(target, value, _healPool, healDigitPrefabs, "HealNumberGroup");
    }

    // ===============================
    // 顯示格擋傷害（藍色/灰色等 0~9）
    // ===============================
    public void ShowBlocked(Transform target, int value)
    {
        ShowNumber(target, value, _blockedPool, blockedDigitPrefabs, "BlockedNumberGroup");
    }


    // 共用邏輯：生成數字群組
    private void ShowNumber(Transform target, int value,
        Dictionary<int, Queue<ParticleSystem>> pool, ParticleSystem[] prefabs, string groupName)
    {
        if (target == null) return;
        if (value < 0) return;

        var groupGO = new GameObject(groupName);
        var group = groupGO.AddComponent<DamageNumberGroup>();
        group.manager = this;
        group.transform.localScale = groupScale;

        Vector3 pos = target.position + Vector3.up * groupOffsetY;
        pos.x += Random.Range(-randomHorizontalJitter, randomHorizontalJitter);
        group.transform.position = pos;

        var chars = value.ToString().ToCharArray();
        float totalWidth = (chars.Length - 1) * digitSpacing;
        float startX = -totalWidth / 2f;

        for (int i = 0; i < chars.Length; i++)
        {
            int digit = chars[i] - '0';
            var ps = RentDigitPS(digit, pool, prefabs);

            ps.transform.SetParent(group.transform, false);
            ps.transform.localPosition = new Vector3(startX + i * digitSpacing, 0f, 0f);

            ps.Clear(true);
            ps.Play(true);

            float lifetime = EstimateLifetime(ps);
            group.RegisterDigit(ps, digit, lifetime, pool);
        }

        group.Begin(groupFloatUp, groupLifetime);
    }

    private float EstimateLifetime(ParticleSystem ps)
    {
        var main = ps.main;
        float duration = main.duration;
        float lifetime = 0f;
        if (main.startLifetime.mode == ParticleSystemCurveMode.Constant)
            lifetime = main.startLifetime.constant;
        else if (main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants)
            lifetime = main.startLifetime.constantMax;
        else
            lifetime = 0.5f;
        return duration + lifetime + 0.05f;
    }

    // 讓群組呼叫：依來源池回收
    public void ReturnDigit(int digit, ParticleSystem ps, Dictionary<int, Queue<ParticleSystem>> pool)
    {
        ReturnDigitPS(digit, ps, pool);
    }
}
