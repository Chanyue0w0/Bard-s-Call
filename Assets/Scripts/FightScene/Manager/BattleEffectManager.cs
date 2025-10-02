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

    // �� Shield ���ɪ��A�]�ȧ@�Ω󪱮a����^
    private bool isShielding = false;

    [Header("Shield �S��")]
    public GameObject shieldVfxPrefab;   // ���w Shield �S�� Prefab
    private GameObject activeShieldVfx;  // ��e�s�b�� Shield �S��

    public void ActivateShield(float duration)
    {
        if (!isShielding)
            StartCoroutine(ShieldCoroutine(duration));
    }

    private System.Collections.IEnumerator ShieldCoroutine(float duration)
    {
        isShielding = true;
        Debug.Log("�i���ɥͮġj���a����K�˶}�l");

        // �� �ͦ� ShieldVFX
        if (shieldVfxPrefab != null && activeShieldVfx == null)
        {
            // �o�̧ڥ����w�@�Ӧ�m�A�Υi�令���H���a����Ū���
            activeShieldVfx = Instantiate(shieldVfxPrefab, new Vector2(1.21f, -2.67f), Quaternion.identity);
        }

        yield return new WaitForSeconds(duration);

        isShielding = false;
        Debug.Log("�i���ɵ����j���a�����_�i���˪��A");

        // �� �R�� ShieldVFX
        if (activeShieldVfx != null)
        {
            Destroy(activeShieldVfx);
            activeShieldVfx = null;
        }
    }

    // �ޯ�R���^�ǡA�����Y�P�w���G
    public void OnHit(BattleManager.TeamSlotInfo attacker, BattleManager.TeamSlotInfo target, bool isPerfect)
    {
        if (attacker == null || target == null) return;

        // �� �P�_�O�_�b���ɪ��A�A�B�ؼХ����O���a���� (��� Actor)
        bool targetIsPlayer = System.Array.Exists(
            BattleManager.Instance.CTeamInfo,
            t => t != null && t.Actor == target.Actor
        );
        if (isShielding && targetIsPlayer)
        {
            Debug.Log($"{attacker.UnitName} �R�� {target.UnitName}�A�����a������ɧK�ˡI");
            return; // ���a����K��
        }

        float multiplier = 0f;

        if (isPerfect)
        {
            multiplier = 1f; // Perfect �� �ˮ`�[��
            attacker.MP = Mathf.Min(attacker.MaxMP, attacker.MP + 10); // Perfect �� �^�]
        }
        else
        {
            multiplier = 0f; // Miss �� �L�ˮ`
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
