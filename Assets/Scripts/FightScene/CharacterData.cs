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

    [Header("���q�����M��")]
    public List<SkillInfo> NormalAttacks = new List<SkillInfo>();

    [Header("�ޯ�M��")]
    public List<SkillInfo> Skills = new List<SkillInfo>();

    public SkillInfo MainNormal => (NormalAttacks != null && NormalAttacks.Count > 0) ? NormalAttacks[0] : null;
    public SkillInfo MainSkill => (Skills != null && Skills.Count > 0) ? Skills[0] : null;
}
