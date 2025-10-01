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

    // �ޯ�R���^��
    public void OnHit(BattleManager.TeamSlotInfo attacker, BattleManager.TeamSlotInfo target)
    {
        if (attacker == null || target == null) return;

        // �̸`��P�_�ˮ`���v
        float multiplier = IsOnBeat() ? 1.0f : 0.5f;
        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(attacker.Atk * multiplier));

        target.HP -= finalDamage;
        Debug.Log($"{attacker.UnitName} �R�� {target.UnitName}�A�ˮ`={finalDamage} �ѾlHP={target.HP}");

        if (target.HP <= 0)
        {
            HandleUnitDefeated(target);
        }
    }

    // TODO�G���ᱵ�`��޲z��
    public bool IsOnBeat()
    {
        // �w�]�H�K�^�ǡA���ӥѸ`��޲z�����ѧP�_
        return Random.value > 0.5f;
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
