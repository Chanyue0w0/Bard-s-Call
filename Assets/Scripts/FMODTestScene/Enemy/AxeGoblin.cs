using UnityEngine;

public class AxeGoblin : MonoBehaviour
{
    public BeatSpriteAnimator anim;

    // 設為 public 方便你在 Inspector 想改成 6 拍 10 拍都可以
    public int attackIntervalBeats = 8;

    private int lastAttackBeat = -999;

    private void Reset()
    {
        if (anim == null)
            anim = GetComponent<BeatSpriteAnimator>();
    }

    private void OnEnable()
    {
        // 訂閱 FMOD 的拍點事件
        FMODBeatListener.OnGlobalBeat += HandleBeat;
    }

    private void OnDisable()
    {
        // 解除訂閱，避免場景切換時報錯
        FMODBeatListener.OnGlobalBeat -= HandleBeat;
    }

    private void HandleBeat(int globalBeat)
    {
        // 如果還沒開始播音樂，globalBeat 可能是 -1
        if (globalBeat < 0)
            return;

        // 檢查是否間隔拍數已達
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
