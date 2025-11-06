using System.Collections;
using UnityEngine;

public abstract class EnemyBase : MonoBehaviour
{
    protected int slotIndex = -1;
    protected bool forceMove = false;

    // ★ Fever 狀態
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


    protected virtual void Awake()
    {
        if (ETeam == BattleManager.ETeam.None)
            ETeam = BattleManager.ETeam.Enemy;

        // ★ 監聽 FeverManager 事件
        FeverManager.OnFeverUltStart += HandleFeverStart;
    }

    protected virtual void OnDestroy()
    {
        FeverManager.OnFeverUltStart -= HandleFeverStart;
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

        // ★ Fever 鎖定倒數
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

    // ★ Fever事件處理：所有敵人共用
    private void HandleFeverStart(int durationBeats)
    {
        isFeverLock = true;
        feverBeatsRemaining = durationBeats;
        Debug.Log($"【Fever鎖定】{name} 停止行動 {durationBeats} 拍");
    }

    public bool IsFeverLocked() => isFeverLock;

}
