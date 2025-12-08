using System.Collections;
using UnityEngine;

public abstract class EnemyBase : MonoBehaviour
{
    protected int slotIndex = -1;
    protected bool forceMove = false;

    // --------------------------------------------------
    // ★ Fever 鎖定狀態（新版、拍點倒數）
    // --------------------------------------------------
    protected bool isFeverLock = false;
    protected int feverBeatsRemaining = 0;   // 真拍點扣減

    protected Vector3 basePosLocal;
    protected Vector3 basePosWorld;

    public BattleManager.ETeam ETeam = BattleManager.ETeam.Enemy;
    protected BattleManager.TeamSlotInfo selfSlot;
    protected BattleManager.TeamSlotInfo targetSlot;
    public BattleManager.TeamSlotInfo thisSlotInfo;

    [HideInInspector] public GameObject tauntedByObj;
    [HideInInspector] public float tauntBeatsRemaining = 0f;

    private SpriteRenderer spr;
    private Color originalColor;
    private Coroutine hitFlashRoutine;

    private readonly Color flashColor = new Color(1f, 0.3f, 0.3f);
    private readonly float hitFlashDuration = 0.5f;

    // --------------------------------------------------
    // Awake
    // --------------------------------------------------
    protected virtual void Awake()
    {
        if (ETeam == BattleManager.ETeam.None)
            ETeam = BattleManager.ETeam.Enemy;

        // Fever 相關事件
        FeverManager.OnFeverUltStart += HandleFeverStart;
        FeverManager.OnFeverEnd += HandleFeverEnd;

        spr = GetComponentInChildren<SpriteRenderer>();
        if (spr != null)
            originalColor = spr.color;
        else
            Debug.LogWarning($"{name} 找不到 SpriteRenderer（受擊閃紅效果停用）");
    }

    protected virtual void OnEnable()
    {
        // ★ 拍點控制 Fever 倒數
        FMODBeatListener2.OnGlobalBeat += HandleFeverBeatTick;

        // ★ 若當前正在 Fever → 新生成敵人也要立即進入鎖定
        TrySyncWithCurrentFeverStatus();
    }

    protected virtual void OnDisable()
    {
        FMODBeatListener2.OnGlobalBeat -= HandleFeverBeatTick;
    }

    protected virtual void OnDestroy()
    {
        FeverManager.OnFeverUltStart -= HandleFeverStart;
        FeverManager.OnFeverEnd -= HandleFeverEnd;
        FMODBeatListener2.OnGlobalBeat -= HandleFeverBeatTick;
    }

    // --------------------------------------------------
    // Slot 綁定
    // --------------------------------------------------
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

    // --------------------------------------------------
    // Update（保持嘲諷即可）
    // --------------------------------------------------
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

        // ★ Fever 不再在 Update() 裡扣減（避免提前解除）
        //   Fever 只在拍點事件 HandleFeverBeatTick 裡扣
    }

    // --------------------------------------------------
    // ★ Fever 拍點倒數：精準、不卡拍、不會提前解除
    // --------------------------------------------------
    private void HandleFeverBeatTick(int beat)
    {
        if (!isFeverLock) return;

        // 每拍減少一次
        feverBeatsRemaining -= 1;

        if (feverBeatsRemaining <= 0)
        {
            isFeverLock = false;
            feverBeatsRemaining = 0;
            Debug.Log($"【Fever解除】{name} 自動於拍點倒數結束");
        }
    }

    // ★ 新生成敵人時，若當前正在 Fever → 立即補同步
    private void TrySyncWithCurrentFeverStatus()
    {
        var fever = FeverManager.Instance;
        if (fever == null)
            return;

        if (fever.IsFeverActive)
        {
            isFeverLock = true;
            feverBeatsRemaining = fever.RemainingFeverBeats;

            // 保險：如果計算出來是 0，就不要鎖 0 拍，直接當作至少 1 拍
            if (feverBeatsRemaining <= 0)
                feverBeatsRemaining = 1;

            Debug.Log($"【Fever補同步】{name} 新生成 → 鎖定剩餘 {feverBeatsRemaining} 拍");
        }
    }

    // --------------------------------------------------
    // ★ Fever 開始
    // --------------------------------------------------
    private void HandleFeverStart(int durationBeats)
    {
        if (this == null || gameObject == null) return;

        isFeverLock = true;
        feverBeatsRemaining = durationBeats;

        Debug.Log($"【Fever鎖定】{name} 停止行動 {durationBeats} 拍");
    }

    // --------------------------------------------------
    // ★ Fever 結束（強制解除）
    // --------------------------------------------------
    private void HandleFeverEnd()
    {
        if (this == null || gameObject == null) return;

        isFeverLock = false;
        feverBeatsRemaining = 0;

        Debug.Log($"【Fever結束】{name} 立即恢復行動！");
    }

    public bool IsFeverLocked() => isFeverLock;

    // --------------------------------------------------
    // 受擊閃爍
    // --------------------------------------------------
    public virtual void OnDamaged(int dmg, bool isHeavy)
    {
        Debug.Log($"{name} 受傷！ dmg={dmg}");

        if (spr == null) return;

        if (hitFlashRoutine != null)
            StopCoroutine(hitFlashRoutine);

        hitFlashRoutine = StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        float half = hitFlashDuration * 0.5f;
        float t = 0f;

        while (t < half)
        {
            t += Time.deltaTime;
            spr.color = Color.Lerp(originalColor, flashColor, t / half);
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            spr.color = Color.Lerp(flashColor, originalColor, t / half);
            yield return null;
        }

        spr.color = originalColor;
        hitFlashRoutine = null;
    }

    public virtual void OnDeath()
    {
        // 由子類別覆寫
    }
}
