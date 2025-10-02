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

    // ★ Shield 格檔狀態
    private bool isShielding = false;

    public void ActivateShield(float duration)
    {
        if (!isShielding)
            StartCoroutine(ShieldCoroutine(duration));
    }

    private System.Collections.IEnumerator ShieldCoroutine(float duration)
    {
        isShielding = true;
        Debug.Log("【格檔生效】全隊免傷開始");
        yield return new WaitForSeconds(duration);
        isShielding = false;
        Debug.Log("【格檔結束】全隊恢復可受傷狀態");
    }

    // 技能命中回傳，直接吃判定結果
    public void OnHit(BattleManager.TeamSlotInfo attacker, BattleManager.TeamSlotInfo target, bool isPerfect)
    {
        if (attacker == null || target == null) return;

        // ★ 判斷是否在格檔狀態
        if (isShielding)
        {
            Debug.Log($"{attacker.UnitName} 命中 {target.UnitName}，但被格檔免傷！");
            return; // 直接免傷
        }

        float multiplier = 0f;

        if (isPerfect)
        {
            multiplier = 1f; // Perfect → 傷害加成
            attacker.MP = Mathf.Min(attacker.MaxMP, attacker.MP + 10); // Perfect → 回魔
        }
        else
        {
            multiplier = 0f; // 普通 Hit → 基本傷害
        }

        int finalDamage = Mathf.Max(0, Mathf.RoundToInt(attacker.Atk * multiplier));

        target.HP -= finalDamage;
        if (target.HP < 0) target.HP = 0;

        Debug.Log($"{attacker.UnitName} 命中 {target.UnitName}，傷害={finalDamage} 剩餘HP={target.HP} (Perfect={isPerfect})");

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
