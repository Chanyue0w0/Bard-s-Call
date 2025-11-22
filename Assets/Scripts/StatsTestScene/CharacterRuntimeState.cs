using System;

[Serializable]
public class CharacterRuntimeState
{
    public CharacterBaseData baseData;

    // 等級與經驗
    public int level;
    public int currentExp;

    // 裝備欄位
    public EquipmentBaseData weapon;
    public EquipmentBaseData armor;
    public EquipmentBaseData accessory;

    // 合併後的能力值（基礎 + 裝備）
    public int totalCon;
    public int totalLuk;
    public int totalPer;
    public int totalStr;
    public int totalAgi;
    public int totalInt;

    // 衍生數值
    public int maxHP;
    public int currentHP;
    //public int physicalAttack;
    //public int magicAttack;
    //public float dodgeRate;
    public int luckContribution;

    public CharacterRuntimeState(CharacterBaseData baseData)
    {
        this.baseData = baseData;

        level = 1;
        currentExp = 0;

        if (baseData != null)
        {
            weapon = baseData.defaultWeapon;
            armor = baseData.defaultArmor;
            accessory = baseData.defaultAccessory;
        }

        StatCalculator.Recalculate(this);
        currentHP = maxHP;
    }
}
