using UnityEngine;

public enum CharacterClassType
{
    Paladin,
    Bard,
    CatGirl,
    Warrior,
    Archer,
    Rogue
}

[CreateAssetMenu(menuName = "Game/Data/Character Base Data", fileName = "CharacterBaseData")]
public class CharacterBaseData : ScriptableObject
{
    [Header("基本資訊")]
    public string characterId;
    public string displayName;
    public CharacterClassType classType;

    [Header("Lv1 基礎能力值（0~100）")]
    public int baseCon;  // 體質
    public int baseLuk;  // 幸運
    public int basePer;  // 感知
    public int baseStr;  // 力量
    public int baseAgi;  // 敏捷
    public int baseInt;  // 智力

    [Header("初始裝備")]
    public EquipmentBaseData defaultWeapon;
    public EquipmentBaseData defaultArmor;
    public EquipmentBaseData defaultAccessory;
}
