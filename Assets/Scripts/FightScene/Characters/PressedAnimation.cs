using System.Collections;
using UnityEngine;

public class PressedAnimation : MonoBehaviour
{
    [Header("Perfect（跳躍）設定")]
    public float jumpHeight = 0.35f;
    public float jumpTime = 0.15f;

    [Header("Miss（抖動）設定")]
    public float shakeTime = 0.25f;
    public float shakeStrength = 0.18f;

    private Coroutine currentAnim;

    //角色原始 localPosition（不會因 Dash 或動畫改變）
    private Vector3 initialLocalPos;

    private void Awake()
    {
        // 不在 Awake 保存位置，因為位置可能由 BattleManager 排位時還沒定案
    }

    private void Start()
    {
        // ★ 在 Start 記錄角色原始站位
        initialLocalPos = transform.localPosition;
    }

    private void OnEnable()
    {
        // 若物件復活/重新啟用，強制回到原位
        //transform.localPosition = initialLocalPos;
    }

    // ============================================================
    // 對外 API
    // ============================================================
    public void PlayPerfect()
    {
        PlayAnimation(PerfectJumpAnimation());
    }

    public void PlayMiss()
    {
        PlayAnimation(MissShakeAnimation());
    }

    private void PlayAnimation(IEnumerator routine)
    {
        if (currentAnim != null)
            StopCoroutine(currentAnim);

        currentAnim = StartCoroutine(routine);
    }


    // ============================================================
    // Perfect 動畫（跳躍）
    // ============================================================
    private IEnumerator PerfectJumpAnimation()
    {
        Transform actor = transform;

        //起點永遠是初始位置
        Vector3 startPos = initialLocalPos;
        Vector3 peakPos = startPos + new Vector3(0, jumpHeight, 0);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / jumpTime;
            actor.localPosition = Vector3.Lerp(
                startPos,
                peakPos,
                Mathf.Sin(t * Mathf.PI * 0.5f)
            );
            yield return null;
        }

        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / jumpTime;
            actor.localPosition = Vector3.Lerp(peakPos, startPos, t);
            yield return null;
        }

        //最後強制回到角色真 初始站位
        actor.localPosition = initialLocalPos;
        currentAnim = null;
    }


    // ============================================================
    // Miss 動畫（左右抖動）
    // ============================================================
    private IEnumerator MissShakeAnimation()
    {
        Transform actor = transform;

        //起點永遠是初始位置
        Vector3 origin = initialLocalPos;

        float t = 0f;
        while (t < shakeTime)
        {
            t += Time.deltaTime;
            float damper = 1f - (t / shakeTime);
            float offsetX = Mathf.Sin(t * 60f) * shakeStrength * damper;

            actor.localPosition = origin + new Vector3(offsetX, 0, 0);
            yield return null;
        }

        //最後回到原位
        actor.localPosition = initialLocalPos;
        currentAnim = null;
    }
}
