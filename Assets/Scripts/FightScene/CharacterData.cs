using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SkillInfo
{
    [Header("�W��")]
    public string SkillName;

    [Header("Prefab�]�i�d�š^")]
    public GameObject SkillPrefab;

    [Header("����MP�]�i��^")]
    public int MPCost = 0;

    [Header("��¦�ˮ`�έ��v�]�i�ۭq�^")]
    [Tooltip("�i�@���B�~�����O���v�]�Ҧp 1.2f = 120% �����O�^")]
    public float DamageMultiplier = 1.0f;

    [Tooltip("�Y���۬��T�w�ˮ`�A�i�������w�ȡ]>0 �ɷ|�л\���v�p��^")]
    public int FixedDamage = 0;
}

public class CharacterData : MonoBehaviour
{
    [Header("����򥻸��")]
    public string CharacterName;
    public BattleManager.UnitClass ClassType = BattleManager.UnitClass.Warrior;

    [Header("�԰��ƭ�")]
    public int MaxHP = 100;
    public int HP = 100;
    public int MaxMP = 50;
    public int MP = 0;
    public int OriginAtk = 10;
    public int Atk = 10;

    [Header("���q�����M��]�h�q�s���^")]
    public List<SkillInfo> NormalAttacks = new List<SkillInfo>();

    [Header("�������]�ĥ|��Ρ^")]
    public SkillInfo HeavyAttack;

    [Header("�ޯ�M��")]
    public List<SkillInfo> Skills = new List<SkillInfo>();

    [Header("���ɯS�� Prefab�]�i��^")]
    [Tooltip("������ɮ���ܪ��S�ġA�Ҧp���ީΰ{���ĪG")]
    public GameObject ShieldEffectPrefab;

    // �D��������
    public SkillInfo MainNormal => (NormalAttacks != null && NormalAttacks.Count > 0) ? NormalAttacks[0] : null;
    public SkillInfo MainSkill => (Skills != null && Skills.Count > 0) ? Skills[0] : null;
    public SkillInfo MainHeavyAttack => HeavyAttack;

    // �p��̲׶ˮ`�]���� BattleManager �i�γo�ӲΤ@�B�z�^
    public int CalculateDamage(SkillInfo skill)
    {
        if (skill == null) return 0;

        if (skill.FixedDamage > 0)
            return skill.FixedDamage;

        return Mathf.RoundToInt(Atk * skill.DamageMultiplier);
    }
}
