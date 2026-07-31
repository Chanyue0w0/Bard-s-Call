using UnityEngine;

public class HeroCombatState : MonoBehaviour
{
    [Header("目前狀態")]
    public bool IsBlocking;

    [Header("硬直狀態")]
    [SerializeField]
    private int hitStunRemainingBeats;

    public bool IsInHitStun =>
        hitStunRemainingBeats > 0;

    // 避免同一拍重複收到事件時重複攻擊
    private int lastNormalAttackBeat =
        int.MinValue;

    public bool CanUseNormalAttack(
        CharacterData characterData,
        int beat)
    {
        if (characterData == null)
            return false;

        if (IsBlocking)
            return false;

        if (IsInHitStun)
            return false;

        if (characterData.NormalAttackIntervalBeats <= 0)
            return false;

        int adjustedBeat =
            beat -
            characterData.NormalAttackBeatOffset;

        if (adjustedBeat < 0)
            return false;

        if (adjustedBeat %
            characterData.NormalAttackIntervalBeats != 0)
        {
            return false;
        }

        if (lastNormalAttackBeat == beat)
            return false;

        return true;
    }

    public void MarkNormalAttackUsed(
        int beat)
    {
        lastNormalAttackBeat = beat;
    }

    public void EnterHitStun(
        int beats)
    {
        if (beats <= 0)
            return;

        // 不累加，只保留較長的剩餘硬直
        hitStunRemainingBeats =
            Mathf.Max(
                hitStunRemainingBeats,
                beats
            );
    }

    public void TickBeat()
    {
        if (hitStunRemainingBeats > 0)
        {
            hitStunRemainingBeats--;
        }
    }
}