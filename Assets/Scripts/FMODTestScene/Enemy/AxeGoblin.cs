using UnityEngine;

public class AxeGoblin : MonoBehaviour
{
    public BeatSpriteAnimator anim;

    [Header("警告特效")]
    public GameObject warningPrefab;
    public Vector3 warningOffset;

    [Header("攻擊特效")]
    public GameObject attackPrefab;
    public Vector3 attackOffset;

    [Header("攻擊間隔（拍）")]
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

        lastAttackBeat = FMODBeatListener2.Instance.GlobalBeatIndex;

        if (anim != null)
            anim.OnFrameEvent += HandleAnimEvent;
    }

    private void OnDisable()
    {
        FMODBeatListener2.OnGlobalBeat -= HandleBeat;

        if (anim != null)
            anim.OnFrameEvent -= HandleAnimEvent;
    }

    private void HandleBeat(int globalBeat)
    {
        if (globalBeat - lastAttackBeat >= attackIntervalBeats)
        {
            lastAttackBeat = globalBeat;
            DoAttack();
        }
    }

    public void DoAttack()
    {
        if (anim != null)
            anim.Play("Attack", true);
    }

    private void HandleAnimEvent(BeatSpriteFrame frame)
    {
        // 警告
        if (frame.triggerWarning && warningPrefab != null)
        {
            Instantiate(
                warningPrefab,
                transform.position + warningOffset,
                Quaternion.identity
            );
        }

        // 攻擊
        if (frame.triggerAttack && attackPrefab != null)
        {
            Instantiate(
                attackPrefab,
                transform.position + attackOffset,
                Quaternion.identity
            );
        }
    }
}
