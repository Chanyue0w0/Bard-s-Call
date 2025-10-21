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
    public GameObject shieldVfxPrefab;   // �w�]���ɯS��
    private GameObject[] blockEffects = new GameObject[3];

    [Header("Priest �^�_�S��")]
    public GameObject healVfxPrefab;

    // �C�쨤�⪺�W�߮��ɪ��A
    private bool[] isBlocking = new bool[3];

    // =======================
    // �� BattleManager �I�s�G�Ұʮ���
    // =======================
    public void ActivateBlock(int index, float duration, CharacterData charData, GameObject actor)
    {
        if (index < 0 || index >= isBlocking.Length) return;

        // �� �����ureturn�v����A���\�P�@����s�����
        // �Y�e�@�������٨S���� �� �������A�}�ҷs��
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

        // �� ����ɶ����u��@��]������ɸ��^
        float beatTime = BeatManager.Instance != null ? BeatManager.Instance.beatTravelTime : 0.5f;
        float adjustedDuration = Mathf.Min(duration, beatTime * 0.9f);

        StartCoroutine(BlockRoutine(index, adjustedDuration, charData, actor));
    }

    private IEnumerator BlockRoutine(int index, float duration, CharacterData charData, GameObject actor)
    {
        isBlocking[index] = true;
        Debug.Log($"�i���ɱҰʡj���� {actor.name} �i�J�L�Ī��A ({duration:F2}s)");

        // �� ���o�����m
        Vector3 spawnPos = BattleManager.Instance != null && index < BattleManager.Instance.CTeamInfo.Length
            ? BattleManager.Instance.CTeamInfo[index].SlotTransform.position
            : actor.transform.position;

        // �� �S�Ħ�m�W�� 1.3
        spawnPos += Vector3.up * 1.3f;

        // �� �ͦ����ɯS��
        GameObject effectPrefab = (charData != null && charData.ShieldEffectPrefab != null)
            ? charData.ShieldEffectPrefab
            : shieldVfxPrefab;

        if (effectPrefab != null)
            blockEffects[index] = Instantiate(effectPrefab, spawnPos, Quaternion.identity);

        // ���ݩ�Ʈɶ�
        yield return new WaitForSeconds(duration);

        // ��������
        isBlocking[index] = false;
        if (blockEffects[index] != null)
        {
            Destroy(blockEffects[index]);
            blockEffects[index] = null;
        }

        Debug.Log($"�i���ɵ����j���� {actor.name} ��_�i����");
    }

    // =======================
    // �ˮ`�P�w
    // =======================
    public void OnHit(BattleManager.TeamSlotInfo attacker, BattleManager.TeamSlotInfo target, bool isPerfect)
    {
        if (attacker == null || target == null) return;

        // �P�_�Ө���O�_�B����ɪ��A
        int targetIndex = System.Array.FindIndex(BattleManager.Instance.CTeamInfo, t => t == target);
        if (targetIndex >= 0 && isBlocking[targetIndex])
        {
            Debug.Log($"�i���ɦ��\�j{target.UnitName} ���� {attacker.UnitName} �������I");
            return;
        }

        float multiplier = isPerfect ? 1f : 0f;
        int finalDamage = Mathf.Max(0, Mathf.RoundToInt(attacker.Atk * multiplier));

        target.HP -= finalDamage;
        if (target.HP < 0) target.HP = 0;

        Debug.Log($"{attacker.UnitName} �R�� {target.UnitName}�A�ˮ`={finalDamage} �ѾlHP={target.HP} (Perfect={isPerfect})");

        // �^�]�]Perfect�^
        if (isPerfect)
            attacker.MP = Mathf.Min(attacker.MaxMP, attacker.MP + 10);

        // �����s
        var hb = target.Actor?.GetComponentInChildren<HealthBarUI>();
        if (hb != null) hb.ForceUpdate();

        // ���`�B�z
        if (target.HP <= 0)
            HandleUnitDefeated(target);
    }

    private void HandleUnitDefeated(BattleManager.TeamSlotInfo target)
    {
        Debug.Log($"{target.UnitName} �w�Q���ѡI");
        if (target.Actor != null)
            Destroy(target.Actor);
    }

    // =======================
    // ����^�_�]���v�Ρ^
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

                Debug.Log($"{ally.UnitName} �^�_ {healAmount} �� �{�b HP={ally.HP}");
            }
        }
    }
}
