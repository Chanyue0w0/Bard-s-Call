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
        DontDestroyOnLoad(gameObject);
    }

    // 技能命中回傳
    public void OnHit(BattleManager.TeamSlotInfo attacker, BattleManager.TeamSlotInfo target)
    {
        if (attacker == null || target == null) return;

        // 依節拍判斷傷害倍率
        float multiplier = IsOnBeat() ? 1.0f : 0.5f;
        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(attacker.Atk * multiplier));

        target.HP -= finalDamage;
        Debug.Log($"{attacker.UnitName} 命中 {target.UnitName}，傷害={finalDamage} 剩餘HP={target.HP}");

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
