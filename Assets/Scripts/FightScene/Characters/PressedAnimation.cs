using System.Collections;
using UnityEngine;

public class PressedAnimation : MonoBehaviour
{
    [Header("Perfect／普通攻擊（跳躍）設定")]
    public float jumpHeight = 0.35f;
    public float jumpTime = 0.15f;

    [Header("Miss（抖動）設定")]
    public float shakeTime = 0.25f;
    public float shakeStrength = 0.18f;

    [Header("受傷動畫設定")]
    [Tooltip("順時針傾斜角度。Unity 2D 中順時針使用負值。")]
    public float hitTiltAngle = 20f;

    [Tooltip("受傷時向後移動的水平距離。正值代表 local X 正方向。")]
    public float hitBackDistance = 0.18f;

    [Tooltip("受傷後跳的高度。")]
    public float hitJumpHeight = 0.12f;

    [Tooltip("受傷動畫推出時間。")]
    public float hitOutTime = 0.1f;

    [Tooltip("受傷動畫返回時間。")]
    public float hitReturnTime = 0.18f;

    private Coroutine currentAnim;

    // 角色排位完成後的原始位置與旋轉
    private Vector3 initialLocalPos;
    private Quaternion initialLocalRotation;

    private void Start()
    {
        initialLocalPos =
            transform.localPosition;

        initialLocalRotation =
            transform.localRotation;
    }

    private void OnDisable()
    {
        ResetTransform();
    }

    // ============================================================
    // 對外 API
    // ============================================================

    /// <summary>
    /// 原本 Perfect 判定的小跳躍。
    /// </summary>
    public void PlayPerfect()
    {
        PlayAnimation(
            JumpAnimation()
        );
    }

    /// <summary>
    /// 勇者普通攻擊時的小跳躍。
    /// </summary>
    public void PlayAttack()
    {
        PlayAnimation(
            JumpAnimation()
        );
    }

    /// <summary>
    /// 原本 Miss 判定的左右抖動。
    /// </summary>
    public void PlayMiss()
    {
        PlayAnimation(
            MissShakeAnimation()
        );
    }

    /// <summary>
    /// 勇者受到魔王攻擊時的受傷動畫。
    /// </summary>
    public void PlayHit()
    {
        PlayAnimation(
            HitAnimation()
        );
    }

    private void PlayAnimation(
        IEnumerator routine)
    {
        if (currentAnim != null)
        {
            StopCoroutine(
                currentAnim
            );
        }

        // 避免上一個動畫被中斷後，
        // 從錯誤的位置或旋轉開始下一個動畫
        ResetTransform();

        currentAnim =
            StartCoroutine(
                routine
            );
    }

    private void ResetTransform()
    {
        transform.localPosition =
            initialLocalPos;

        transform.localRotation =
            initialLocalRotation;
    }

    // ============================================================
    // Perfect／普通攻擊動畫
    // ============================================================

    private IEnumerator JumpAnimation()
    {
        Transform actor =
            transform;

        Vector3 startPos =
            initialLocalPos;

        Vector3 peakPos =
            startPos +
            new Vector3(
                0f,
                jumpHeight,
                0f
            );

        float safeJumpTime =
            Mathf.Max(
                0.01f,
                jumpTime
            );

        float t = 0f;

        while (t < 1f)
        {
            t +=
                Time.deltaTime /
                safeJumpTime;

            float easedT =
                Mathf.Sin(
                    Mathf.Clamp01(t) *
                    Mathf.PI *
                    0.5f
                );

            actor.localPosition =
                Vector3.Lerp(
                    startPos,
                    peakPos,
                    easedT
                );

            yield return null;
        }

        t = 0f;

        while (t < 1f)
        {
            t +=
                Time.deltaTime /
                safeJumpTime;

            actor.localPosition =
                Vector3.Lerp(
                    peakPos,
                    startPos,
                    Mathf.Clamp01(t)
                );

            yield return null;
        }

        ResetTransform();

        currentAnim = null;
    }

    // ============================================================
    // Miss 動畫
    // ============================================================

    private IEnumerator MissShakeAnimation()
    {
        Transform actor =
            transform;

        Vector3 origin =
            initialLocalPos;

        float safeShakeTime =
            Mathf.Max(
                0.01f,
                shakeTime
            );

        float t = 0f;

        while (t < safeShakeTime)
        {
            t += Time.deltaTime;

            float damper =
                1f -
                Mathf.Clamp01(
                    t / safeShakeTime
                );

            float offsetX =
                Mathf.Sin(t * 60f) *
                shakeStrength *
                damper;

            actor.localPosition =
                origin +
                new Vector3(
                    offsetX,
                    0f,
                    0f
                );

            yield return null;
        }

        ResetTransform();

        currentAnim = null;
    }

    // ============================================================
    // 受傷動畫
    // ============================================================

    private IEnumerator HitAnimation()
    {
        Transform actor =
            transform;

        Vector3 startPos =
            initialLocalPos;

        Quaternion startRotation =
            initialLocalRotation;

        // 正值 hitTiltAngle 轉換成負 Z，
        // 在 2D 畫面中呈現順時針傾斜
        Quaternion hitRotation =
            startRotation *
            Quaternion.Euler(
                0f,
                0f,
                -hitTiltAngle
            );

        Vector3 hitPosition =
            startPos +
            new Vector3(
                hitBackDistance,
                hitJumpHeight,
                0f
            );

        float safeOutTime =
            Mathf.Max(
                0.01f,
                hitOutTime
            );

        float safeReturnTime =
            Mathf.Max(
                0.01f,
                hitReturnTime
            );

        // 第一段：後跳並順時針傾斜
        float t = 0f;

        while (t < 1f)
        {
            t +=
                Time.deltaTime /
                safeOutTime;

            float easedT =
                Mathf.Sin(
                    Mathf.Clamp01(t) *
                    Mathf.PI *
                    0.5f
                );

            actor.localPosition =
                Vector3.Lerp(
                    startPos,
                    hitPosition,
                    easedT
                );

            actor.localRotation =
                Quaternion.Lerp(
                    startRotation,
                    hitRotation,
                    easedT
                );

            yield return null;
        }

        // 第二段：回到原始站位與旋轉
        t = 0f;

        while (t < 1f)
        {
            t +=
                Time.deltaTime /
                safeReturnTime;

            float easedT =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(t)
                );

            actor.localPosition =
                Vector3.Lerp(
                    hitPosition,
                    startPos,
                    easedT
                );

            actor.localRotation =
                Quaternion.Lerp(
                    hitRotation,
                    startRotation,
                    easedT
                );

            yield return null;
        }

        ResetTransform();

        currentAnim = null;
    }
}