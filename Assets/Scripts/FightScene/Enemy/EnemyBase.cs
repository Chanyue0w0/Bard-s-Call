using System.Collections;
using UnityEngine;

public abstract class EnemyBase : MonoBehaviour
{
    protected int slotIndex = -1;
    protected bool forceMove = false;

    protected Vector3 basePosLocal;
    protected Vector3 basePosWorld;

    public BattleManager.ETeam ETeam = BattleManager.ETeam.Enemy;
    protected BattleManager.TeamSlotInfo selfSlot;
    protected BattleManager.TeamSlotInfo targetSlot;
    public BattleManager.TeamSlotInfo thisSlotInfo;  // 紀錄該敵人對應的 TeamSlotInfo

    [HideInInspector]
    public GameObject tauntedByObj;           // 被哪個角色嘲諷
    [HideInInspector]
    public float tauntBeatsRemaining = 0f;    // 嘲諷剩餘拍數



    protected virtual void Awake()
    {
        if (ETeam == BattleManager.ETeam.None)
            ETeam = BattleManager.ETeam.Enemy;
    }

    protected virtual void Start()
    {
        // 延遲配對，確保 BattleManager 存在
        if (BattleManager.Instance == null)
        {
            StartCoroutine(DelayAssignSlot());
        }
        else
        {
            AutoAssignSlotIndex();
        }
    }

    protected virtual void Update()
    {
        if (tauntBeatsRemaining > 0)
        {
            float beatTime = (BeatManager.Instance != null) ? 60f / BeatManager.Instance.bpm : 0.4f;
            tauntBeatsRemaining -= Time.deltaTime / beatTime;
            if (tauntBeatsRemaining <= 0)
            {
                Debug.Log($"【嘲諷結束】{name} 恢復自由目標");
                tauntedByObj = null;
                tauntBeatsRemaining = 0;
            }
        }
    }


    // 初始化時由 BattleManager 指派
    public void InitSlotInfo(BattleManager.TeamSlotInfo info)
    {
        thisSlotInfo = info;
    }

    // 嘲諷檢查函式
    //protected BattleManager.TeamSlotInfo CheckTauntRedirectTarget(BattleManager.TeamSlotInfo originalTarget)
    //{
    //    // 檢查自己是否被嘲諷
    //    var taunter = BattleEffectManager.Instance.GetTaunter(thisSlotInfo);

    //    if (taunter != null && taunter.Actor != null)
    //    {
    //        Debug.Log($"【嘲諷生效】{thisSlotInfo.UnitName} 被迫攻擊 {taunter.UnitName}");
    //        return taunter;
    //    }

    //    // 否則使用原本的攻擊邏輯
    //    return originalTarget;
    //}

    // 延遲等待 BattleManager 準備好
    public IEnumerator DelayAssignSlot()
    {
        yield return new WaitUntil(() =>
            BattleManager.Instance != null && BattleManager.Instance.EnemyTeamInfo != null);
        AutoAssignSlotIndex();
    }

    protected virtual void AutoAssignSlotIndex()
    {
        if (BattleManager.Instance == null || BattleManager.Instance.EnemyTeamInfo == null)
            return;

        for (int i = 0; i < BattleManager.Instance.EnemyTeamInfo.Length; i++)
        {
            var info = BattleManager.Instance.EnemyTeamInfo[i];
            if (info != null && info.Actor == gameObject)
            {
                selfSlot = info;
                slotIndex = i;

                if (i < BattleManager.Instance.CTeamInfo.Length)
                    targetSlot = BattleManager.Instance.CTeamInfo[i];
                return;
            }
        }
    }

    public void SetForceMove(bool value) => forceMove = value;
    public bool IsForceMoving() => forceMove;

    // 子類別實作拍點邏輯
    //protected abstract void OnBeat();
}
