using UnityEngine;

public abstract class EnemyBase : MonoBehaviour
{
    protected int slotIndex = -1;
    protected bool forceMove = false;

    // ★ 用於儲存移動基準位置
    protected Vector3 basePosLocal;
    protected Vector3 basePosWorld;

    protected virtual void Awake()
    {
        AutoAssignSlotIndex();
    }

    protected void AutoAssignSlotIndex()
    {
        // 自動配對敵人索引
        for (int i = 0; i < BattleManager.Instance.ETeamInfo.Length; i++)
        {
            if (BattleManager.Instance.ETeamInfo[i].Actor == this.gameObject)
            {
                slotIndex = i;
                Debug.Log($"【{gameObject.name}】自動配對到 ETeamInfo[{i}]");
                return;
            }
        }
        Debug.LogWarning($"【{gameObject.name}】找不到對應 ETeamInfo 索引！");
    }

    // 暫停/恢復移動行為
    public void SetForceMove(bool value)
    {
        forceMove = value;
    }

    // 允許子類查詢
    public bool IsForceMoving() => forceMove;

    // 取得自己與目標 slot
    protected BattleManager.TeamSlotInfo selfSlot => BattleManager.Instance.ETeamInfo[slotIndex];
    protected BattleManager.TeamSlotInfo targetSlot => BattleManager.Instance.CTeamInfo[slotIndex];

    // 每個敵人都需自行實作 OnBeat()
    protected abstract void OnBeat();
}
