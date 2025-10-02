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

    // ★ Shield 格檔狀態（僅作用於玩家隊伍）
    private bool isShielding = false;

    [Header("Shield 特效")]
    public GameObject shieldVfxPrefab;   // 指定 Shield 特效 Prefab
    private GameObject activeShieldVfx;  // 當前存在的 Shield 特效

    public void ActivateShield(float duration)
    {
        if (!isShielding)
            StartCoroutine(ShieldCoroutine(duration));
    }

    private System.Collections.IEnumerator ShieldCoroutine(float duration)
    {
        isShielding = true;
        Debug.Log("【格檔生效】玩家隊伍免傷開始");

        // ★ 生成 ShieldVFX
        if (shieldVfxPrefab != null && activeShieldVfx == null)
        {
            // 這裡我先給定一個位置，或可改成跟隨玩家隊伍的空物件
            activeShieldVfx = Instantiate(shieldVfxPrefab, new Vector2(1.21f, -2.67f), Quaternion.identity);
        }

        yield return new WaitForSeconds(duration);

        isShielding = false;
        Debug.Log("【格檔結束】玩家隊伍恢復可受傷狀態");

        // ★ 刪除 ShieldVFX
        if (activeShieldVfx != null)
        {
            Destroy(activeShieldVfx);
            activeShieldVfx = null;
        }
    }

    // 技能命中回傳，直接吃判定結果
    public void OnHit(BattleManager.TeamSlotInfo attacker, BattleManager.TeamSlotInfo target, bool isPerfect)
    {
        if (attacker == null || target == null) return;

        // ★ 判斷是否在格檔狀態，且目標必須是玩家隊伍 (比對 Actor)
        bool targetIsPlayer = System.Array.Exists(
            BattleManager.Instance.CTeamInfo,
            t => t != null && t.Actor == target.Actor
        );
        if (isShielding && targetIsPlayer)
        {
            Debug.Log($"{attacker.UnitName} 命中 {target.UnitName}，但玩家隊伍格檔免傷！");
            return; // 玩家隊伍免傷
        }

        float multiplier = 0f;

        if (isPerfect)
        {
            multiplier = 1f; // Perfect → 傷害加成
            attacker.MP = Mathf.Min(attacker.MaxMP, attacker.MP + 10); // Perfect → 回魔
        }
        else
        {
            multiplier = 0f; // Miss → 無傷害
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
