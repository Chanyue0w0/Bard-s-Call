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

    [Header("基礎傷害或倍率（可自訂）")]
    [Tooltip("可作為額外攻擊力倍率（例如 1.2f = 120% 攻擊力）")]
    public float DamageMultiplier = 1.0f;

    [Tooltip("若此招為固定傷害，可直接指定值（>0 時會覆蓋倍率計算）")]
    public int FixedDamage = 0;
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

    [Header("普通攻擊清單（多段連擊）")]
    public List<SkillInfo> NormalAttacks = new List<SkillInfo>();

    [Header("重攻擊（第四拍用）")]
    public SkillInfo HeavyAttack;

    [Header("技能清單")]
    public List<SkillInfo> Skills = new List<SkillInfo>();

    [Header("格檔特效 Prefab（可選）")]
    [Tooltip("角色格檔時顯示的特效，例如光盾或閃光效果")]
    public GameObject ShieldEffectPrefab;

    // 主攻擊取用
    public SkillInfo MainNormal => (NormalAttacks != null && NormalAttacks.Count > 0) ? NormalAttacks[0] : null;
    public SkillInfo MainSkill => (Skills != null && Skills.Count > 0) ? Skills[0] : null;
    public SkillInfo MainHeavyAttack => HeavyAttack;

    // 計算最終傷害（之後 BattleManager 可用這個統一處理）
    public int CalculateDamage(SkillInfo skill)
    {
        if (skill == null) return 0;

        if (skill.FixedDamage > 0)
            return skill.FixedDamage;

        return Mathf.RoundToInt(Atk * skill.DamageMultiplier);
    }
}
