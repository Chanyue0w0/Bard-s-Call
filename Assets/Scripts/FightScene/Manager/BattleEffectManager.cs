using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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

    // -------------------------
    // �k�v�R�q�S�ĺ޲z
    // -------------------------
    [Header("Mage �R�q�S��")]
    public GameObject mageChargeVfxPrefab; // ���w�k�v���W���R�q�S��Prefab
    private Dictionary<BattleManager.TeamSlotInfo, GameObject> mageChargeEffects = new();
    // -------------------------
    // �k�v�R�q�h�Ƭ���
    // -------------------------
    private Dictionary<BattleManager.TeamSlotInfo, int> mageChargeStacks = new Dictionary<BattleManager.TeamSlotInfo, int>();

    public int GetChargeStacks(BattleManager.TeamSlotInfo mage)
    {
        if (mage == null) return 0;
        return mageChargeStacks.ContainsKey(mage) ? mageChargeStacks[mage] : 0;
    }

    public void AddChargeStack(BattleManager.TeamSlotInfo mage)
    {
        if (mage == null) return;
        if (!mageChargeStacks.ContainsKey(mage))
            mageChargeStacks[mage] = 0;

        mageChargeStacks[mage]++;
        // ����W����6�h
        mageChargeStacks[mage] = Mathf.Min(mageChargeStacks[mage], 6);

        // �Y�����R��A�ͦ��S��
        if (mage.Actor != null && mageChargeVfxPrefab != null)
        {
            // �Y�S�Ĥ��s�b�Τw�Q�P���A���s�ͦ�
            if (!mageChargeEffects.ContainsKey(mage) || mageChargeEffects[mage] == null)
            {
                Vector3 spawnPos = mage.Actor.transform.position;
                var effect = Instantiate(mageChargeVfxPrefab, spawnPos, Quaternion.identity);
                mageChargeEffects[mage] = effect;
                Debug.Log($"�i�R�q�S�ĥͦ��j�� {mage.UnitName} ��m {spawnPos}");
            }
        }

        // ��s HeavyAttackBarUI ���
        var bar = mage.Actor.GetComponentInChildren<HeavyAttackBarUI>();
        if (bar != null)
        {
            bar.UpdateComboCount(mageChargeStacks[mage]);
        }

        // �P�B comboState�A�� UI �� Update() �U�@�V�����G�O
        var combo = mage.Actor.GetComponent<CharacterComboState>();
        if (combo != null)
        {
            combo.comboCount = mageChargeStacks[mage];
            combo.currentPhase = mageChargeStacks[mage]; // �Y�A UI �� phase ��ܥi�@�֧�s
        }


        Debug.Log($"�i�R�q�W�[�j{mage.UnitName} �{�b {mageChargeStacks[mage]} �h�C");

    }

    public void ResetChargeStacks(BattleManager.TeamSlotInfo mage)
    {
        if (mage == null) return;

        // �k�s�h��
        if (mageChargeStacks.ContainsKey(mage))
            mageChargeStacks[mage] = 0;

        // �����S��
        if (mageChargeEffects.ContainsKey(mage) && mageChargeEffects[mage] != null)
        {
            Destroy(mageChargeEffects[mage]);
            mageChargeEffects.Remove(mage);
        }

        // ��s HeavyAttackBarUI ����k�s
        if (mage.Actor != null)
        {
            var bar = mage.Actor.GetComponentInChildren<HeavyAttackBarUI>();
            if (bar != null)
                bar.UpdateComboCount(0);

            // �P�B comboState�A�T�O�U�@�V���Q Update() �\�^�G�O
            var combo = mage.Actor.GetComponent<CharacterComboState>();
            if (combo != null)
            {
                combo.comboCount = 0;
                combo.currentPhase = 0;
            }
        }


        Debug.Log($"�i�R�q�M���j{mage.UnitName} �R�q�k�s�ò����S�ġC");
    }


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
    public void OnHit(BattleManager.TeamSlotInfo attacker, BattleManager.TeamSlotInfo target, bool isPerfect, bool isHeavyAttack = false, int overrideDamage = -1)
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
            var darkKnight = target.Actor.GetComponent<DarkLongSwordKnight>();
            if (darkKnight != null)
            {
                // �Y Boss ���@�ޥB���}�a
                if (darkKnight.isShieldActive && !darkKnight.isShieldBroken)
                {
                    if (isHeavyAttack)
                    {
                        darkKnight.BreakShield();
                        Debug.Log($"�i�}�����\�j{attacker.UnitName} �����������} DarkLongSwordKnight �����m�I");
                    }
                    else
                    {
                        Debug.Log($"�i���ɦ��\�jDarkLongSwordKnight �פU {attacker.UnitName} �������I");
                        return; // �פU�����A���y���ˮ`
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
        int finalDamage = (overrideDamage >= 0)
            ? overrideDamage
            : Mathf.Max(0, Mathf.RoundToInt(attacker.Atk * (isPerfect ? 1f : 0f)));


        target.HP -= finalDamage;
        if (target.HP < 0) target.HP = 0;

        Debug.Log($"{attacker.UnitName} �R�� {target.UnitName}�A�ˮ`={finalDamage} �ѾlHP={target.HP} (Perfect={isPerfect})");

        // Perfect �^�]
        if (isPerfect)
            attacker.MP = Mathf.Min(attacker.MaxMP, attacker.MP + 10);

        // �����s
        var hb = target.Actor?.GetComponentInChildren<HealthBarUI>();
        if (hb != null) hb.ForceUpdate();

        // ���a��μĤ���ˮ� �� �Y�O�k�v�h�M���R�q�h
        if (target.Actor != null)
        {
            var data = target.Actor.GetComponent<CharacterData>();
            if (data != null && data.ClassType == BattleManager.UnitClass.Mage)
            {
                ResetChargeStacks(target);
                Debug.Log($"�i�R�q���_�j{target.UnitName} ��������A�R�q�k�s");
            }
        }


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
        int enemyIndex = System.Array.FindIndex(BattleManager.Instance.EnemyTeamInfo, t => t == target);
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
