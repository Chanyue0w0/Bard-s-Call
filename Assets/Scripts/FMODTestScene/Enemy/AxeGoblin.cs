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
        FMODBeatListener.OnGlobalBeat += HandleBeat;

        // ★ 修正：使用正確的 FMODBeatListener Getter
        //   讓哥布林進場後先等待 8 拍才第一次攻擊
        if (FMODBeatListener.Instance != null)
        {
            lastAttackBeat = FMODBeatListener.Instance.GlobalBeatIndex;
        }
        else
        {
            // 沒有 Listener 時至少避免 -999 造成立即攻擊
            lastAttackBeat = 0;
        }
    }

    private void OnDisable()
    {
        FMODBeatListener.OnGlobalBeat -= HandleBeat;
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
