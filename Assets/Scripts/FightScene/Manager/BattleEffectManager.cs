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

    // �ޯ�R���^�ǡA�����Y�P�w���G
    public void OnHit(BattleManager.TeamSlotInfo attacker, BattleManager.TeamSlotInfo target, bool isPerfect)
    {
        if (attacker == null || target == null) return;

        float multiplier = 0f;

        if (isPerfect)
        {
            multiplier = 1f; // Perfect �� �ˮ`�[��
            attacker.MP = Mathf.Min(attacker.MaxMP, attacker.MP + 10); // Perfect �� �^�]
        }
        else
        {
            multiplier = 0f; // ���q Hit �� �򥻶ˮ`
        }

        int finalDamage = Mathf.Max(0, Mathf.RoundToInt(attacker.Atk * multiplier));

        target.HP -= finalDamage;
        if (target.HP < 0) target.HP = 0;

        Debug.Log($"{attacker.UnitName} �R�� {target.UnitName}�A�ˮ`={finalDamage} �ѾlHP={target.HP} (Perfect={isPerfect})");

        // �q����� UI ��s
        var hb = target.Actor?.GetComponentInChildren<HealthBarUI>();
        if (hb != null) hb.ForceUpdate();

        if (target.HP <= 0)
        {
            HandleUnitDefeated(target);
        }
    }

    private void HandleUnitDefeated(BattleManager.TeamSlotInfo target)
    {
        Debug.Log($"{target.UnitName} �w�Q���ѡI");
        if (target.Actor != null)
        {
            GameObject.Destroy(target.Actor);
        }
    }
}
