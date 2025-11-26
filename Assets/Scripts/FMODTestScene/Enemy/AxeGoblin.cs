using UnityEngine;

public class AxeGoblin : MonoBehaviour
{
    public BeatSpriteAnimator anim;

    public int attackIntervalBeats = 8;

    private int lastAttackBeat = -999;

    private void Reset()
    {
        if (anim == null)
            anim = GetComponent<BeatSpriteAnimator>();
    }

    private void OnEnable()
    {
        FMODBeatListener2.OnGlobalBeat += HandleBeat;

        // ★ 修正：使用正確的 FMODBeatListener Getter
        //   讓哥布林進場後先等待 8 拍才第一次攻擊
        lastAttackBeat = FMODBeatListener2.Instance.GlobalBeatIndex;
        
    }

    private void OnDisable()
    {
        FMODBeatListener2.OnGlobalBeat -= HandleBeat;
    }

    private void HandleBeat(int globalBeat)
    {
        if (globalBeat < 0)
            return;

        if (globalBeat - lastAttackBeat >= attackIntervalBeats)
        {
            lastAttackBeat = globalBeat;
            DoAttack();
        }
    }

    public void DoAttack()
    {
        if (anim != null)
        {
            anim.Play("Attack", true);
        }
    }
}
