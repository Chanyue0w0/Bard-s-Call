using UnityEngine;

public class BattleEffectManager : MonoBehaviour
{
    public static BattleEffectManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // 技能命中回傳
    public void OnHit(BattleManager.TeamSlotInfo attacker, BattleManager.TeamSlotInfo target)
    {
        if (attacker == null || target == null) return;

        // ★ 改為用 BeatJudge 判斷是否對拍
        bool onBeat = BeatJudge.Instance.IsOnBeat();
        float multiplier = onBeat ? 1.0f : 0.0f; // 對拍 = 100% 傷害, Miss = 0%
        int finalDamage = Mathf.Max(0, Mathf.RoundToInt(attacker.Atk * multiplier));

        target.HP -= finalDamage;
        if (target.HP < 0) target.HP = 0;

        Debug.Log($"{attacker.UnitName} 命中 {target.UnitName}，傷害={finalDamage} 剩餘HP={target.HP} (OnBeat={onBeat})");

        // 通知血條 UI 更新
        var hb = target.Actor?.GetComponentInChildren<HealthBarUI>();
        if (hb != null) hb.ForceUpdate();

        if (target.HP <= 0)
        {
            HandleUnitDefeated(target);
        }
    }

    private void HandleUnitDefeated(BattleManager.TeamSlotInfo target)
    {
        Debug.Log($"{target.UnitName} 已被擊敗！");
        if (target.Actor != null)
        {
            GameObject.Destroy(target.Actor);
        }
    }
}
