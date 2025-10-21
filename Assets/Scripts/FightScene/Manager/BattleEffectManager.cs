using UnityEngine;
using System.Collections;

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

    [Header("共用 Shield 特效（當角色未指定 ShieldEffectPrefab 時使用）")]
    public GameObject shieldVfxPrefab;   // 預設格檔特效
    private GameObject[] blockEffects = new GameObject[3];

    [Header("Priest 回復特效")]
    public GameObject healVfxPrefab;

    // 每位角色的獨立格檔狀態
    private bool[] isBlocking = new bool[3];

    // =======================
    // 由 BattleManager 呼叫：啟動格檔
    // =======================
    public void ActivateBlock(int index, float duration, CharacterData charData, GameObject actor)
    {
        if (index < 0 || index >= isBlocking.Length) return;

        // ★ 移除「return」限制，允許同一角色連拍格檔
        // 若前一次格檔還沒結束 → 結束它，開啟新的
        if (isBlocking[index])
        {
            StopCoroutine($"BlockRoutine_{index}");
            isBlocking[index] = false;
            if (blockEffects[index] != null)
            {
                Destroy(blockEffects[index]);
                blockEffects[index] = null;
            }
        }

        // ★ 持續時間略短於一拍（防止格檔跨拍）
        float beatTime = BeatManager.Instance != null ? BeatManager.Instance.beatTravelTime : 0.5f;
        float adjustedDuration = Mathf.Min(duration, beatTime * 0.9f);

        StartCoroutine(BlockRoutine(index, adjustedDuration, charData, actor));
    }

    private IEnumerator BlockRoutine(int index, float duration, CharacterData charData, GameObject actor)
    {
        isBlocking[index] = true;
        Debug.Log($"【格檔啟動】角色 {actor.name} 進入無敵狀態 ({duration:F2}s)");

        // ★ 取得角色位置
        Vector3 spawnPos = BattleManager.Instance != null && index < BattleManager.Instance.CTeamInfo.Length
            ? BattleManager.Instance.CTeamInfo[index].SlotTransform.position
            : actor.transform.position;

        // ★ 特效位置上移 1.3
        spawnPos += Vector3.up * 1.3f;

        // ★ 生成格檔特效
        GameObject effectPrefab = (charData != null && charData.ShieldEffectPrefab != null)
            ? charData.ShieldEffectPrefab
            : shieldVfxPrefab;

        if (effectPrefab != null)
            blockEffects[index] = Instantiate(effectPrefab, spawnPos, Quaternion.identity);

        // 等待拍數時間
        yield return new WaitForSeconds(duration);

        // 結束格檔
        isBlocking[index] = false;
        if (blockEffects[index] != null)
        {
            Destroy(blockEffects[index]);
            blockEffects[index] = null;
        }

        Debug.Log($"【格檔結束】角色 {actor.name} 恢復可受傷");
    }

    // =======================
    // 傷害判定
    // =======================
    public void OnHit(BattleManager.TeamSlotInfo attacker, BattleManager.TeamSlotInfo target, bool isPerfect)
    {
        if (attacker == null || target == null) return;

        // 判斷該角色是否處於格檔狀態
        int targetIndex = System.Array.FindIndex(BattleManager.Instance.CTeamInfo, t => t == target);
        if (targetIndex >= 0 && isBlocking[targetIndex])
        {
            Debug.Log($"【格檔成功】{target.UnitName} 格檔 {attacker.UnitName} 的攻擊！");
            return;
        }

        float multiplier = isPerfect ? 1f : 0f;
        int finalDamage = Mathf.Max(0, Mathf.RoundToInt(attacker.Atk * multiplier));

        target.HP -= finalDamage;
        if (target.HP < 0) target.HP = 0;

        Debug.Log($"{attacker.UnitName} 命中 {target.UnitName}，傷害={finalDamage} 剩餘HP={target.HP} (Perfect={isPerfect})");

        // 回魔（Perfect）
        if (isPerfect)
            attacker.MP = Mathf.Min(attacker.MaxMP, attacker.MP + 10);

        // 血條更新
        var hb = target.Actor?.GetComponentInChildren<HealthBarUI>();
        if (hb != null) hb.ForceUpdate();

        // 死亡處理
        if (target.HP <= 0)
            HandleUnitDefeated(target);
    }

    private void HandleUnitDefeated(BattleManager.TeamSlotInfo target)
    {
        Debug.Log($"{target.UnitName} 已被擊敗！");
        if (target.Actor != null)
            Destroy(target.Actor);
    }

    // =======================
    // 全體回復（牧師用）
    // =======================
    public void HealTeam(int healAmount)
    {
        var team = BattleManager.Instance.CTeamInfo;
        foreach (var ally in team)
        {
            if (ally != null && ally.Actor != null)
            {
                ally.HP = Mathf.Min(ally.MaxHP, ally.HP + healAmount);

                var hb = ally.Actor.GetComponentInChildren<HealthBarUI>();
                if (hb != null) hb.ForceUpdate();

                Debug.Log($"{ally.UnitName} 回復 {healAmount} → 現在 HP={ally.HP}");
            }
        }
    }
}
