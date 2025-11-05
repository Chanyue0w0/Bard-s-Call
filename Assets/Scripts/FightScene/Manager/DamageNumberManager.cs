using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageNumberManager : MonoBehaviour
{
    public static DamageNumberManager Instance { get; private set; }

    [Header("數字粒子 Prefab (索引0~9)")]
    public ParticleSystem[] digitPrefabs = new ParticleSystem[10];

    [Header("預熱設定")]
    public int prewarmPerDigit = 8;

    [Header("配置")]
    public float digitSpacing = 0.22f;        // 位數之間的水平距離
    public float groupOffsetY = 1.4f;         // 生成時相對目標的高度
    public float groupFloatUp = 0.8f;         // 漂浮位移高度
    public float groupLifetime = 0.9f;        // 群組存在時間（同時做位移）
    public float randomHorizontalJitter = 0.06f; // 為每個群組加一點隨機水平偏移
    public Vector3 groupScale = Vector3.one;  // 整體縮放

    [Header("排序與圖層")]
    public string sortingLayerName = "Default";
    public int sortingOrder = 20;

    // 內部：各數字的物件池
    private readonly Dictionary<int, Queue<ParticleSystem>> _pool = new Dictionary<int, Queue<ParticleSystem>>();
    private Transform _poolRoot;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _poolRoot = new GameObject("[DamageNumberPool]").transform;
        _poolRoot.SetParent(transform, false);

        // 建立池與預熱
        for (int d = 0; d <= 9; d++)
        {
            _pool[d] = new Queue<ParticleSystem>();
            if (digitPrefabs[d] == null) continue;

            for (int i = 0; i < prewarmPerDigit; i++)
            {
                var ps = Instantiate(digitPrefabs[d], _poolRoot);
                PreparePSRenderer(ps);
                ps.gameObject.SetActive(false);
                _pool[d].Enqueue(ps);
            }
        }
    }

    // 對渲染排序屬性做一次設定（避免每次Spawn重複寫）
    private void PreparePSRenderer(ParticleSystem ps)
    {
        var r = ps.GetComponent<ParticleSystemRenderer>();
        if (r != null)
        {
            r.sortingLayerName = sortingLayerName;
            r.sortingOrder = sortingOrder;
        }
    }

    // 取得或生成指定數字的粒子
    private ParticleSystem RentDigitPS(int digit)
    {
        if (!_pool.ContainsKey(digit)) _pool[digit] = new Queue<ParticleSystem>();

        if (_pool[digit].Count > 0)
        {
            var ps = _pool[digit].Dequeue();
            ps.gameObject.SetActive(true);
            return ps;
        }
        else
        {
            var prefab = digitPrefabs[digit];
            var ps = Instantiate(prefab, _poolRoot);
            PreparePSRenderer(ps);
            return ps;
        }
    }

    // 歸還到池
    private void ReturnDigitPS(int digit, ParticleSystem ps)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.transform.SetParent(_poolRoot, false);
        ps.gameObject.SetActive(false);
        _pool[digit].Enqueue(ps);
    }

    // 對外：顯示傷害數字
    public void ShowDamage(Transform target, int value)
    {
        if (target == null) return;
        if (value < 0) value = 0; // 防呆

        // 建立群組
        var groupGO = new GameObject("DamageNumberGroup");
        var group = groupGO.AddComponent<DamageNumberGroup>();
        group.manager = this;
        group.transform.localScale = groupScale;

        // 初始位置（頭頂＋少許隨機水平抖動）
        Vector3 pos = target.position + Vector3.up * groupOffsetY;
        pos.x += Random.Range(-randomHorizontalJitter, randomHorizontalJitter);
        group.transform.position = pos;

        // 轉成字元陣列，並計算置中
        var chars = value.ToString().ToCharArray();
        float totalWidth = (chars.Length - 1) * digitSpacing;
        float startX = -totalWidth / 2f;

        // 逐位生成
        for (int i = 0; i < chars.Length; i++)
        {
            int digit = chars[i] - '0';
            var ps = RentDigitPS(digit);

            ps.transform.SetParent(group.transform, false);
            ps.transform.localPosition = new Vector3(startX + i * digitSpacing, 0f, 0f);

            // 確保每次播放都從頭開始
            ps.Clear(true);
            ps.Play(true);

            // 告知群組：這個粒子的壽命與該用哪個digit池歸還
            float lifetime = EstimateLifetime(ps);
            group.RegisterDigit(ps, digit, lifetime);
        }

        // 啟動群組位移與回收計時
        group.Begin(groupFloatUp, groupLifetime);
    }

    // 粗估單個粒子系統播完時間
    private float EstimateLifetime(ParticleSystem ps)
    {
        var main = ps.main;
        float duration = main.duration;

        // 取 startLifetime.constant/constantMax 作為上限
        float lifetime = 0f;
        if (main.startLifetime.mode == ParticleSystemCurveMode.Constant)
            lifetime = main.startLifetime.constant;
        else if (main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants)
            lifetime = main.startLifetime.constantMax;
        else
            lifetime = 0.5f; // 若是曲線就給個保守值

        return duration + lifetime + 0.05f;
    }

    // 給群組呼叫：把位數粒子歸還
    public void ReturnDigit(int digit, ParticleSystem ps)
    {
        ReturnDigitPS(digit, ps);
    }
}
