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

    protected virtual void Awake()
    {
        if (ETeam == BattleManager.ETeam.None)
            ETeam = BattleManager.ETeam.Enemy;
    }

    protected virtual void Start()
    {
        // ����t��A�T�O BattleManager �s�b
        if (BattleManager.Instance == null)
        {
            StartCoroutine(DelayAssignSlot());
        }
        else
        {
            AutoAssignSlotIndex();
        }
    }

    // ���𵥫� BattleManager �ǳƦn
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

    // �l���O��@���I�޿�
    //protected abstract void OnBeat();
}
