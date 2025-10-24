using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class VibrationManager : MonoBehaviour
{
    public static VibrationManager Instance;

    [System.Serializable]
    public class VibrationPreset
    {
        [Tooltip("�_�ʼҦ��W�١A�Ҧp Perfect�BBlock�BHit")]
        public string name = "Default";

        [Tooltip("�����F�j�� (�C�W)")]
        [Range(0f, 1f)] public float lowFrequency = 0.3f;

        [Tooltip("�k���F�j�� (���W)")]
        [Range(0f, 1f)] public float highFrequency = 0.6f;

        [Tooltip("�_�ʫ���ɶ��]��^")]
        public float duration = 0.15f;
    }

    [Header("�w�]�_�ʼҦ��M��")]
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
    // �D��ơG�I�s�w�]�Ҧ��W��
    // ============================================================
    public void Vibrate(string presetName)
    {
        if (Gamepad.current == null) return;

        VibrationPreset preset = presets.Find(p => p.name == presetName);
        if (preset == null)
        {
            Debug.LogWarning($"[VibrationManager] �䤣��w�]�W�١G{presetName}");
            return;
        }

        Vibrate(preset.lowFrequency, preset.highFrequency, preset.duration);
    }

    // ============================================================
    // �ۭq�_�ʱj��
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
