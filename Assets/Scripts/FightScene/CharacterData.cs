using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SkillInfo
{
    [Header("名稱")]
    public string SkillName;

    [Header("Prefab（可留空）")]
    public GameObject SkillPrefab;

    [Header("消耗MP（可選）")]
    public int MPCost = 0;
}

public class CharacterData : MonoBehaviour
{
    [Header("角色基本資料")]
    public string CharacterName;
    public BattleManager.UnitClass ClassType = BattleManager.UnitClass.Warrior;

    [Header("戰鬥數值")]
    public int MaxHP = 100;
    public int HP = 100;
    public int MaxMP = 50;
    public int MP = 0;
    public int OriginAtk = 10;
    public int Atk = 10;

    [Header("普通攻擊清單")]
    public List<SkillInfo> NormalAttacks = new List<SkillInfo>();

    [Header("技能清單")]
    public List<SkillInfo> Skills = new List<SkillInfo>();

    public SkillInfo MainNormal => (NormalAttacks != null && NormalAttacks.Count > 0) ? NormalAttacks[0] : null;
    public SkillInfo MainSkill => (Skills != null && Skills.Count > 0) ? Skills[0] : null;
}
