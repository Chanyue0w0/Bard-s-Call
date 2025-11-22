using UnityEngine;

public static class StatCalculator
{
    // 角色的實際數值全部在這裡計算
    public static void Recalculate(CharacterRuntimeState state)
    {
        if (state == null || state.baseData == null)
        {
            Debug.LogWarning("StatCalculator.Recalculate 收到的 state 或 baseData 為 null");
            return;
        }

        // 先從基礎值開始
        int con = state.baseData.baseCon;
        int luk = state.baseData.baseLuk;
        int per = state.baseData.basePer;
        int str = state.baseData.baseStr;
        int agi = state.baseData.baseAgi;
        int intel = state.baseData.baseInt;

        // 再加上裝備加成
        AddEquipmentStats(state.weapon, ref con, ref luk, ref per, ref str, ref agi, ref intel);
        AddEquipmentStats(state.armor, ref con, ref luk, ref per, ref str, ref agi, ref intel);
        AddEquipmentStats(state.accessory, ref con, ref luk, ref per, ref str, ref agi, ref intel);

        // 存回總能力值
        state.totalCon = con;
        state.totalLuk = luk;
        state.totalPer = per;
        state.totalStr = str;
        state.totalAgi = agi;
        state.totalInt = intel;

        // 衍生數值計算
        state.maxHP = con * 20;                  // 你指定的規則
        //state.physicalAttack = str;              // 初版先直接等於力量
        //state.magicAttack = intel;               // 初版先直接等於智力
        state.luckContribution = luk;            // 之後隊伍掉寶率用

        // 閃避率先做個簡單示意：敏捷 100 → 50% 閃避
        //state.dodgeRate = Mathf.Clamp01(agi * 0.5f / 100f);
    }

    private static void AddEquipmentStats(
        EquipmentBaseData equip,
        ref int con,
        ref int luk,
        ref int per,
        ref int str,
        ref int agi,
        ref int intel)
    {
        if (equip == null)
            return;

        con += equip.addCon;
        luk += equip.addLuk;
        per += equip.addPer;
        str += equip.addStr;
        agi += equip.addAgi;
        intel += equip.addInt;
    }
}
