using UnityEngine;
using FMODUnity;

public class FMODAudioPlayer : MonoBehaviour
{
    public static FMODAudioPlayer Instance { get; private set; }

    [Header("Paladin")]
    public EventReference Paladin_LightBeat;
    public EventReference Paladin_HeavyBeat;

    [Header("Bard")]
    public EventReference Bard_LightBeat;
    public EventReference Bard_HeavyBeat;

    [Header("Mage")]
    public EventReference Mage_LightBeat;
    public EventReference Mage_HeavyBeat;

    [Header("Fever")]
    public EventReference FeverMusic;

    [Header("Enemies")]
    public EventReference AxeGoblin_NormalAttack;
    public EventReference MageGoblin_NormalAttack;
    public EventReference AttackWarning;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ============================================================
    // 公用 API — 任意音效播放
    // ============================================================
    public void Play(EventReference evt)
    {
        if (!evt.IsNull)
            RuntimeManager.PlayOneShot(evt);
        else
            Debug.LogWarning("[FMODAudioPlayer] 試圖播放未設定的音效");
    }

    // ============================================================
    // 便捷 API：直接呼叫函式即可
    // ============================================================
    public void PlayPaladinLight() => Play(Paladin_LightBeat);
    public void PlayPaladinHeavy() => Play(Paladin_HeavyBeat);

    public void PlayBardLight() => Play(Bard_LightBeat);
    public void PlayBardHeavy() => Play(Bard_HeavyBeat);

    public void PlayMageLight() => Play(Mage_LightBeat);
    public void PlayMageHeavy() => Play(Mage_HeavyBeat);

    public void PlayFeverMusic() => Play(FeverMusic);

    public void PlayAxeGoblinAttack() => Play(AxeGoblin_NormalAttack);
    public void PlayAttackWarning() => Play(AttackWarning);
}
