using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class VibrationManager : MonoBehaviour
{
    public static VibrationManager Instance;

    [System.Serializable]
    public class VibrationPreset
    {
        [Tooltip("震動模式名稱，例如 Perfect、Block、Hit")]
        public string name = "Default";

        [Tooltip("左馬達強度 (低頻)")]
        [Range(0f, 1f)] public float lowFrequency = 0.3f;

        [Tooltip("右馬達強度 (高頻)")]
        [Range(0f, 1f)] public float highFrequency = 0.6f;

        [Tooltip("震動持續時間（秒）")]
        public float duration = 0.15f;
    }

    [Header("預設震動模式清單")]
    public List<VibrationPreset> presets = new List<VibrationPreset>()
    {
        new VibrationPreset() { name = "Perfect", lowFrequency = 0.3f, highFrequency = 0.8f, duration = 0.12f },
        new VibrationPreset() { name = "Block",   lowFrequency = 0.9f, highFrequency = 1.0f, duration = 0.25f },
        new VibrationPreset() { name = "Hit",     lowFrequency = 0f, highFrequency = 0f, duration = 0f },
        new VibrationPreset() { name = "Miss",    lowFrequency = 0f, highFrequency = 0f, duration = 0f }
    };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        //DontDestroyOnLoad(gameObject);
    }

    // ============================================================
    // 主函數：呼叫預設模式名稱
    // ============================================================
    public void Vibrate(string presetName)
    {
        if (Gamepad.current == null) return;

        VibrationPreset preset = presets.Find(p => p.name == presetName);
        if (preset == null)
        {
            Debug.LogWarning($"[VibrationManager] 找不到預設名稱：{presetName}");
            return;
        }

        Vibrate(preset.lowFrequency, preset.highFrequency, preset.duration);
    }

    // ============================================================
    // 自訂震動強度
    // ============================================================
    public void Vibrate(float lowFrequency, float highFrequency, float duration)
    {
        if (Gamepad.current == null) return;

        Gamepad.current.SetMotorSpeeds(lowFrequency, highFrequency);
        CancelInvoke(nameof(StopVibration));
        Invoke(nameof(StopVibration), duration);
    }

    private void StopVibration()
    {
        if (Gamepad.current == null) return;
        Gamepad.current.SetMotorSpeeds(0, 0);
    }
}
