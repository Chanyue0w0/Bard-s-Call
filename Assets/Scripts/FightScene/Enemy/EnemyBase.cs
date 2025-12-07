using System.Collections;
using UnityEngine;

public abstract class EnemyBase : MonoBehaviour
{
    protected int slotIndex = -1;
    protected bool forceMove = false;

    protected bool isFeverLock = false;
    protected float feverBeatsRemaining = 0f;

    protected Vector3 basePosLocal;
    protected Vector3 basePosWorld;

    public BattleManager.ETeam ETeam = BattleManager.ETeam.Enemy;
    protected BattleManager.TeamSlotInfo selfSlot;
    protected BattleManager.TeamSlotInfo targetSlot;
    public BattleManager.TeamSlotInfo thisSlotInfo;

    [HideInInspector] public GameObject tauntedByObj;
    [HideInInspector] public float tauntBeatsRemaining = 0f;

    // ------------------------------
    // ★ SpriteRenderer 與受擊動畫
    // ------------------------------
    private SpriteRenderer spr;          // 主渲染器
    private Color originalColor;         // 初始顏色
    private Coroutine hitFlashRoutine;   // 避免重複疊加

    // 顏色參數
    private readonly Color flashColor = new Color(1f, 0.3f, 0.3f); // 淡紅色
    private readonly float hitFlashDuration = 0.5f;               // 總時長 0.5 秒

    protected virtual void Awake()
    {
        if (ETeam == BattleManager.ETeam.None)
            ETeam = BattleManager.ETeam.Enemy;

        FeverManager.OnFeverUltStart += HandleFeverStart;

        // -----------------------
        // ★ 自動抓 SpriteRenderer
        // -----------------------
        spr = GetComponentInChildren<SpriteRenderer>();
        if (spr != null)
        {
            originalColor = spr.color;
        }
        else
        {
            Debug.LogWarning($"{name} 找不到 SpriteRenderer（受擊閃紅效果將不啟用）");
        }
    }

    protected virtual void OnDestroy()
    {
        FeverManager.OnFeverUltStart -= HandleFeverStart;
    }

    public IEnumerator DelayAssignSlot()
    {
        yield return new WaitForSeconds(0.05f);

        var bm = BattleManager.Instance;
        if (bm == null) yield break;

        for (int i = 0; i < bm.EnemyTeamInfo.Length; i++)
        {
            var info = bm.EnemyTeamInfo[i];
            if (info != null && info.Actor == this.gameObject)
            {
                thisSlotInfo = info;
                slotIndex = i;
                basePosWorld = transform.position;
                basePosLocal = transform.localPosition;
                Debug.Log($"[DelayAssignSlot] {name} 已綁定 slot {i}");
                yield break;
            }
        }
    }

    public void AssignSelfSlot(BattleManager.TeamSlotInfo slot)
    {
        selfSlot = slot;
        thisSlotInfo = slot;
    }


    protected virtual void Update()
    {
        // 嘲諷倒數
        if (tauntBeatsRemaining > 0)
        {
            float beatTime = (BeatManager.Instance != null) ? 60f / BeatManager.Instance.bpm : 0.4f;
            tauntBeatsRemaining -= Time.deltaTime / beatTime;
            if (tauntBeatsRemaining <= 0)
            {
                tauntedByObj = null;
                tauntBeatsRemaining = 0;
            }
        }

        // Fever鎖定
        if (isFeverLock)
        {
            float beatTime = (BeatManager.Instance != null) ? 60f / BeatManager.Instance.bpm : 0.4f;
            feverBeatsRemaining -= Time.deltaTime / beatTime;
            if (feverBeatsRemaining <= 0)
            {
                isFeverLock = false;
                feverBeatsRemaining = 0;
                Debug.Log($"【Fever恢復】{name} 可再次行動");
            }
        }
    }

    // ==================================================
    // ★★★ 受傷事件（所有敵人共用） ★★★
    // ==================================================
    public virtual void OnDamaged(int dmg, bool isHeavy)
    {
        Debug.Log($"{name} 受傷！ dmg={dmg}");

        if (spr == null) return;

        // 若正在跑受擊動畫 → 取消重跑
        if (hitFlashRoutine != null)
            StopCoroutine(hitFlashRoutine);

        hitFlashRoutine = StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        float half = hitFlashDuration * 0.5f;
        float t = 0f;

        // -----------------------------
        // 原色 → 淡紅色
        // -----------------------------
        while (t < half)
        {
            t += Time.deltaTime;
            float lerp = t / half;
            spr.color = Color.Lerp(originalColor, flashColor, lerp);
            yield return null;
        }

        t = 0f;

        // -----------------------------
        // 淡紅色 → 原色
        // -----------------------------
        while (t < half)
        {
            t += Time.deltaTime;
            float lerp = t / half;
            spr.color = Color.Lerp(flashColor, originalColor, lerp);
            yield return null;
        }

        spr.color = originalColor;
        hitFlashRoutine = null;
    }

    private void HandleFeverStart(int durationBeats)
    {
        if (this == null || gameObject == null) return;

        isFeverLock = true;
        feverBeatsRemaining = durationBeats;

        Debug.Log($"【Fever鎖定】{name} 停止行動 {durationBeats} 拍");
    }

    public bool IsFeverLocked() => isFeverLock;

    public virtual void OnDeath()
    {
        // 敵人死亡處理 …
    }

}
