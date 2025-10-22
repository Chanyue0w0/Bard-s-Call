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

    [Header("�@�� Shield �S�ġ]���⥼���w ShieldEffectPrefab �ɨϥΡ^")]
    public GameObject shieldVfxPrefab;
    private GameObject[] blockEffects = new GameObject[3];

    [Header("Priest �^�_�S��")]
    public GameObject healVfxPrefab;

    // �C�쨤�⪺���ɪ��A�P��{�l��
    private bool[] isBlocking = new bool[3];
    private Coroutine[] blockCoroutines = new Coroutine[3];

    public bool isHeavyAttack = false;

    public void ActivateBlock(int index, float duration, CharacterData charData, GameObject actor)
    {
        if (index < 0 || index >= isBlocking.Length) return;

        // �Y�w������ �� �������ª�
        if (blockCoroutines[index] != null)
        {
            StopCoroutine(blockCoroutines[index]);
            blockCoroutines[index] = null;
        }

        // �M�z�ݯd�S��
        if (blockEffects[index] != null)
        {
            Destroy(blockEffects[index]);
            blockEffects[index] = null;
        }

        // �H�笰��Ǯɶ��]���u��@��A�קK���^
        float beatTime = BeatManager.Instance != null ? BeatManager.Instance.beatTravelTime : 0.5f;
        float adjustedDuration = Mathf.Min(duration, beatTime * 0.9f);

        blockCoroutines[index] = StartCoroutine(BlockRoutine(index, adjustedDuration, charData, actor));
    }

    private IEnumerator BlockRoutine(int index, float duration, CharacterData charData, GameObject actor)
    {
        isBlocking[index] = true;
        Debug.Log($"�i���ɱҰʡj���� {actor.name} �i�J�L�Ī��A ({duration:F2}s)");

        // ���o�ͦ���m
        Vector3 spawnPos = BattleManager.Instance != null && index < BattleManager.Instance.CTeamInfo.Length
            ? BattleManager.Instance.CTeamInfo[index].SlotTransform.position
            : actor.transform.position;

        spawnPos += Vector3.up * 1.3f; // �S�ĤW��

        // �ͦ��S��
        GameObject effectPrefab = (charData != null && charData.ShieldEffectPrefab != null)
            ? charData.ShieldEffectPrefab
            : shieldVfxPrefab;

        if (effectPrefab != null)
            blockEffects[index] = Instantiate(effectPrefab, spawnPos, Quaternion.identity);

        // ���ݵ���
        yield return new WaitForSeconds(duration);

        // ��������
        isBlocking[index] = false;
        if (blockEffects[index] != null)
        {
            Destroy(blockEffects[index]);
            blockEffects[index] = null;
        }

        blockCoroutines[index] = null; // �M�Ŭ���
        Debug.Log($"�i���ɵ����j���� {actor.name} ��_�i����");
    }

    // =======================
    // �ˮ`�P�w�]�t�������P�w�P ShieldGoblin �}���޿�^
    // =======================
    public void OnHit(BattleManager.TeamSlotInfo attacker, BattleManager.TeamSlotInfo target, bool isPerfect, bool isHeavyAttack = false)
    {
        if (attacker == null || target == null) return;

        // =======================================
        // ShieldGoblin �ä[���m�޿�
        // =======================================
        if (target.Actor != null)
        {
            var goblin = target.Actor.GetComponent<ShieldGoblin>();
            if (goblin != null)
            {
                // �Y���ɤ��B���}��
                if (goblin.IsBlocking())
                {
                    // �Y�O������ �� �}��
                    if (isHeavyAttack)
                    {
                        goblin.BreakShield();
                        Debug.Log($"�i�}�����\�j{attacker.UnitName} �����������} {target.UnitName} �����m�I");
                    }
                    else
                    {
                        Debug.Log($"�i���ɦ��\�j{target.UnitName} �פU {attacker.UnitName} �������I");
                        return; // �����ˮ`
                    }
                }
            }
        }

        // =======================================
        // ���a����ɧP�w�]�즳����^
        // =======================================
        int targetIndex = System.Array.FindIndex(BattleManager.Instance.CTeamInfo, t => t == target);
        if (targetIndex >= 0 && isBlocking[targetIndex])
        {
            VibrationManager.Instance.Vibrate("Block");
            Debug.Log($"�i���ɦ��\�j{target.UnitName} ���� {attacker.UnitName} �������I");
            return;
        }

        // =======================================
        // �@��ˮ`�p��]�O�d���޿�^
        // =======================================
        float multiplier = isPerfect ? 1f : 0f;
        int finalDamage = Mathf.Max(0, Mathf.RoundToInt(attacker.Atk * multiplier));

        target.HP -= finalDamage;
        if (target.HP < 0) target.HP = 0;

        Debug.Log($"{attacker.UnitName} �R�� {target.UnitName}�A�ˮ`={finalDamage} �ѾlHP={target.HP} (Perfect={isPerfect})");

        // Perfect �^�]
        if (isPerfect)
            attacker.MP = Mathf.Min(attacker.MaxMP, attacker.MP + 10);

        // �����s
        var hb = target.Actor?.GetComponentInChildren<HealthBarUI>();
        if (hb != null) hb.ForceUpdate();

        // =======================================
        // �ˬd���`
        // =======================================
        if (target.HP <= 0)
        {
            HandleUnitDefeated(target);
        }
    }


    private void HandleUnitDefeated(BattleManager.TeamSlotInfo target)
    {
        Debug.Log($"{target.UnitName} �w�Q���ѡI");

        // �T�{�O�_���ĤH
        int enemyIndex = System.Array.FindIndex(BattleManager.Instance.ETeamInfo, t => t == target);
        if (enemyIndex >= 0)
        {
            BattleManager.Instance.OnEnemyDeath(enemyIndex);
            return;
        }

        // �Y���ڤ訤�⦺�`
        int allyIndex = System.Array.FindIndex(BattleManager.Instance.CTeamInfo, t => t == target);
        if (allyIndex >= 0)
        {
            // ����i�[�W�ڤ覺�`�B�z�]�Ҧp�Ѱ����ɡB����ʵe���^
            if (target.Actor != null)
                Destroy(target.Actor);

            Debug.Log($"�ڤ訤�� {target.UnitName} �}�`�I");
        }
    }


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

                Debug.Log($"{ally.UnitName} �^�_ {healAmount} �� �{�b HP={ally.HP}");
            }
        }
    }

    // =======================
    // �ä[���ɤ䴩�]�ĤH�M�Ρ^
    // =======================
    public void ActivateInfiniteBlock(GameObject actor, CharacterData charData)
    {
        Vector3 spawnPos = actor.transform.position ; //+Vector3.up * 1.3f
        GameObject effectPrefab = (charData != null && charData.ShieldEffectPrefab != null)
            ? charData.ShieldEffectPrefab
            : shieldVfxPrefab;

        if (effectPrefab != null)
        {
            // �ͦ�����s�b�����ɯS��
            GameObject effect = Instantiate(effectPrefab, spawnPos, Quaternion.identity);
            effect.transform.SetParent(actor.transform, true);

            // �Y�ӯS�ħt Explosion �}���A�����ةR�� 9999 ��
            var explosion = effect.GetComponent<Explosion>();
            if (explosion != null)
            {
                explosion.SetLifeTime(9999f);
                explosion.SetUseUnscaledTime(true);
                explosion.Initialize();
            }


            Debug.Log($"�i�ä[���ɱҰʡj{actor.name} �i�J���m���A�]�S�ĺ��� 9999 ��^");
        }
    }


    // ��ʲ������ɯS�ġ]�Ω�}���^
    public void RemoveBlockEffect(GameObject actor)
    {
        var effects = actor.GetComponentsInChildren<Explosion>();
        foreach (var e in effects)
        {
            Destroy(e.gameObject);
        }
        Debug.Log($"�i���ɯS�ĸѰ��j{actor.name}");
    }


}
