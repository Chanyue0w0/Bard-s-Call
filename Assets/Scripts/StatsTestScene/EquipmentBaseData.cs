using UnityEngine;

public enum EquipmentSlotType
{
    Weapon,
    Armor,
    Accessory
}

[CreateAssetMenu(menuName = "Game/Data/Equipment Base Data", fileName = "EquipmentBaseData")]
public class EquipmentBaseData : ScriptableObject
{
    [Header("基本資訊")]
    public string equipmentId;
    public string displayName;
    public EquipmentSlotType slotType;

    [Header("能力值加成")]
    public int addCon;   // 體質
    public int addLuk;   // 幸運
    public int addPer;   // 感知
    public int addStr;   // 力量
    public int addAgi;   // 敏捷
    public int addInt;   // 智力
}
