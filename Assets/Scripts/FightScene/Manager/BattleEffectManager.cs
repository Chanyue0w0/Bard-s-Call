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
        //DontDestroyOnLoad(gameObject);
    }

    // 技能命中回傳
    public void OnHit(BattleManager.TeamSlotInfo attacker, BattleManager.TeamSlotInfo target)
    {
        if (attacker == null || target == null) return;

        float multiplier = IsOnBeat() ? 1.0f : 0.5f;
        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(attacker.Atk * multiplier));

        target.HP -= finalDamage;
        if (target.HP < 0) target.HP = 0; // 保證不會變成負數

        Debug.Log($"{attacker.UnitName} 命中 {target.UnitName}，傷害={finalDamage} 剩餘HP={target.HP}");

        // ★ 主動通知血條更新
        var hb = target.Actor?.GetComponentInChildren<HealthBarUI>();
        if (hb != null) hb.ForceUpdate();

        if (target.HP <= 0)
        {
            HandleUnitDefeated(target);
        }
    }


    // TODO：之後接節拍管理器
    public bool IsOnBeat()
    {
        // 預設隨便回傳，未來由節拍管理器提供判斷
        return Random.value > 0.5f;
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
