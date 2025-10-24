using UnityEngine;

public abstract class EnemyBase : MonoBehaviour
{
    protected int slotIndex = -1;
    protected bool forceMove = false;

    // �� �Ω��x�s���ʰ�Ǧ�m
    protected Vector3 basePosLocal;
    protected Vector3 basePosWorld;

    protected virtual void Awake()
    {
        AutoAssignSlotIndex();
    }

    protected void AutoAssignSlotIndex()
    {
        // �۰ʰt��ĤH����
        for (int i = 0; i < BattleManager.Instance.ETeamInfo.Length; i++)
        {
            if (BattleManager.Instance.ETeamInfo[i].Actor == this.gameObject)
            {
                slotIndex = i;
                Debug.Log($"�i{gameObject.name}�j�۰ʰt��� ETeamInfo[{i}]");
                return;
            }
        }
        Debug.LogWarning($"�i{gameObject.name}�j�䤣����� ETeamInfo ���ޡI");
    }

    // �Ȱ�/��_���ʦ欰
    public void SetForceMove(bool value)
    {
        forceMove = value;
    }

    // ���\�l���d��
    public bool IsForceMoving() => forceMove;

    // ���o�ۤv�P�ؼ� slot
    protected BattleManager.TeamSlotInfo selfSlot => BattleManager.Instance.ETeamInfo[slotIndex];
    protected BattleManager.TeamSlotInfo targetSlot => BattleManager.Instance.CTeamInfo[slotIndex];

    // �C�ӼĤH���ݦۦ��@ OnBeat()
    protected abstract void OnBeat();
}
