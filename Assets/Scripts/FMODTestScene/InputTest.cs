using UnityEngine;
using UnityEngine.InputSystem;

public class InputTest : MonoBehaviour
{
    [Header("輸入設定")]
    public InputActionReference anyKeyAction;

    [Header("Debug 設定")]
    public bool showDetailedLog = true;

    private int totalPerfect = 0;
    private int totalMiss = 0;
    private float totalPerfectDelta = 0f;

    private System.Action<InputAction.CallbackContext> inputHandler;

    private void OnEnable()
    {
        inputHandler = ctx => OnAnyKeyPressed();

        if (anyKeyAction != null)
        {
            anyKeyAction.action.started += inputHandler;
            anyKeyAction.action.Enable();
        }
        else
        {
            Debug.LogWarning("[InputTest] anyKeyAction 未綁定！");
        }
    }

    private void OnDisable()
    {
        if (anyKeyAction != null)
        {
            anyKeyAction.action.started -= inputHandler;
        }
    }

    // ============================================================
    // 當有輸入發生
    // ============================================================
    private void OnAnyKeyPressed()
    {
        var listener = FMODBeatListener2.Instance;

        if (listener == null)
        {
            Debug.LogWarning("[InputTest] Listener2 尚未初始化");
            return;
        }

        // ★ 新版 Listener2 只有 1 個 IsOnBeat()
        bool hit = listener.IsOnBeat(
            out FMODBeatListener2.Judge judge,
            out int nearestBeatIndex,
            out float deltaSec
        );

        // 從 nearestBeatIndex 計算四拍循環（1~4）
        int beatInCycle = ((nearestBeatIndex % 4) + 3) % 4 + 1;
        string beatType = (beatInCycle == 4) ? "重拍" : "輕拍";

        float deltaMs = deltaSec * 1000f;
        string timing = deltaSec > 0 ? "晚" : "早";

        // --------------------------
        // Perfect
        // --------------------------
        if (hit && judge == FMODBeatListener2.Judge.Perfect)
        {
            totalPerfect++;
            totalPerfectDelta += Mathf.Abs(deltaSec);

            float avgDelta = (totalPerfectDelta / totalPerfect) * 1000f;

            Debug.Log(
                $"<color=lime>✨ Perfect! </color>" +
                $"拍點 {nearestBeatIndex}（第 {beatInCycle} 拍・{beatType}） | " +
                $"Δ={deltaMs:+0.0;-0.0}ms ({timing}) | 平均誤差 {avgDelta:0.0}ms"
            );


            if (showDetailedLog) ShowStatistics();
            return;
        }

        // --------------------------
        // Early / Late
        // --------------------------
        if (hit && (judge == FMODBeatListener2.Judge.Early || judge == FMODBeatListener2.Judge.Late))
        {
            Debug.Log(
                $"<color=yellow>⚠ {judge}</color> " +
                $"拍點 {nearestBeatIndex} | Δ={deltaMs:+0.0;-0.0}ms ({timing})"
            );

            if (showDetailedLog) ShowStatistics();
            return;
        }

        // --------------------------
        // Miss
        // --------------------------
        totalMiss++;

        float perfectMs = listener.perfectWindow * 1000f;

        Debug.Log(
            $"<color=red>❌ Miss</color> " +
            $"最近拍點 {nearestBeatIndex} | Δ={deltaMs:+0.0;-0.0}ms | 超出 ±{perfectMs:0.0}ms"
        );

        if (showDetailedLog) ShowStatistics();
    }

    // ============================================================
    // 統計功能
    // ============================================================
    private float GetAccuracy()
    {
        int total = totalPerfect + totalMiss;
        if (total == 0) return 100f;
        return (totalPerfect / (float)total) * 100f;
    }

    private void ShowStatistics()
    {
        int total = totalPerfect + totalMiss;

        Debug.Log(
            $"<color=white>━━ 統計 ━━</color>\n" +
            $"<color=lime>Perfect: {totalPerfect}</color> | " +
            $"<color=red>Miss: {totalMiss}</color>\n" +
            $"總計: {total} | 準確率: {GetAccuracy():0.0}%"
        );
    }

    [ContextMenu("重置統計")]
    public void ResetStatistics()
    {
        totalPerfect = 0;
        totalMiss = 0;
        totalPerfectDelta = 0f;
        Debug.Log("<color=yellow>[InputTest] 已重置統計</color>");
    }

    [ContextMenu("顯示統計")]
    public void DisplayStatistics()
    {
        float avgDelta = (totalPerfect > 0)
            ? (totalPerfectDelta / totalPerfect) * 1000f
            : 0f;

        Debug.Log(
            $"<color=cyan>━━ InputTest 報告 ━━</color>\n" +
            $"Perfect: {totalPerfect}\n" +
            $"Miss: {totalMiss}\n" +
            $"準確率: {GetAccuracy():0.0}%\n" +
            $"Perfect 平均誤差: {avgDelta:0.0}ms"
        );
    }
}
