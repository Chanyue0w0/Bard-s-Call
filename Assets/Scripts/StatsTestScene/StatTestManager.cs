using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StatTestManager : MonoBehaviour
{
    [Header("角色基礎資料")]
    public List<CharacterBaseData> characterBaseList = new List<CharacterBaseData>();

    [Header("測試用裝備")]
    public EquipmentBaseData testWeapon;
    public EquipmentBaseData testArmor;

    [Header("UI 元件")]
    public Dropdown characterDropdown;
    public Text baseStatsText;
    public Text totalStatsText;
    public Text derivedStatsText;
    public Text partyStatsText;

    private List<CharacterRuntimeState> runtimeStates = new List<CharacterRuntimeState>();

    private void Awake()
    {
        InitRuntimeStates();
        InitDropdown();
        RefreshAllUI();
    }

    private void InitRuntimeStates()
    {
        runtimeStates.Clear();

        foreach (var baseData in characterBaseList)
        {
            if (baseData == null)
                continue;

            var state = new CharacterRuntimeState(baseData);
            runtimeStates.Add(state);
        }
    }

    private void InitDropdown()
    {
        if (characterDropdown == null)
            return;

        characterDropdown.ClearOptions();

        var options = new List<Dropdown.OptionData>();
        foreach (var state in runtimeStates)
        {
            options.Add(new Dropdown.OptionData(state.baseData.displayName));
        }

        characterDropdown.AddOptions(options);
        characterDropdown.onValueChanged.AddListener(OnCharacterDropdownChanged);
        characterDropdown.value = 0;
    }

    private CharacterRuntimeState GetCurrentState()
    {
        if (runtimeStates.Count == 0)
            return null;

        int index = Mathf.Clamp(characterDropdown.value, 0, runtimeStates.Count - 1);
        return runtimeStates[index];
    }

    private void OnCharacterDropdownChanged(int index)
    {
        RefreshAllUI();
    }

    public void EquipTestWeapon()
    {
        var state = GetCurrentState();
        if (state == null)
            return;

        state.weapon = testWeapon;
        StatCalculator.Recalculate(state);
        RefreshAllUI();
    }

    public void UnequipWeapon()
    {
        var state = GetCurrentState();
        if (state == null)
            return;

        state.weapon = null;
        StatCalculator.Recalculate(state);
        RefreshAllUI();
    }

    public void EquipTestArmor()
    {
        var state = GetCurrentState();
        if (state == null)
            return;

        state.armor = testArmor;
        StatCalculator.Recalculate(state);
        RefreshAllUI();
    }

    public void UnequipArmor()
    {
        var state = GetCurrentState();
        if (state == null)
            return;

        state.armor = null;
        StatCalculator.Recalculate(state);
        RefreshAllUI();
    }

    private void RefreshAllUI()
    {
        var state = GetCurrentState();
        if (state == null)
        {
            if (baseStatsText != null) baseStatsText.text = "No Character";
            if (totalStatsText != null) totalStatsText.text = "";
            if (derivedStatsText != null) derivedStatsText.text = "";
            if (partyStatsText != null) partyStatsText.text = "";
            return;
        }

        // 基礎能力值（只看 BaseData，不含裝備）
        if (baseStatsText != null)
        {
            baseStatsText.text =
                $"Base Stats ({state.baseData.displayName})\n" +
                $"CON: {state.baseData.baseCon}\n" +
                $"LUK: {state.baseData.baseLuk}\n" +
                $"PER: {state.baseData.basePer}\n" +
                $"STR: {state.baseData.baseStr}\n" +
                $"AGI: {state.baseData.baseAgi}\n" +
                $"INT: {state.baseData.baseInt}";
        }

        // 合併後能力值（基礎 + 裝備）
        if (totalStatsText != null)
        {
            totalStatsText.text =
                $"Total Stats (with equipment)\n" +
                $"CON: {state.totalCon}\n" +
                $"LUK: {state.totalLuk}\n" +
                $"PER: {state.totalPer}\n" +
                $"STR: {state.totalStr}\n" +
                $"AGI: {state.totalAgi}\n" +
                $"INT: {state.totalInt}";
        }

        // 衍生數值顯示
        if (derivedStatsText != null)
        {
            derivedStatsText.text =
                $"Derived Stats\n" +
                $"MaxHP: {state.maxHP}\n" +
                $"CurrentHP: {state.currentHP}\n" +
                //$"PhysicalAtk: {state.physicalAttack}\n" +
                //$"MagicAtk: {state.magicAttack}\n" +
                //$"DodgeRate: {state.dodgeRate:P0}\n" +
                $"LuckContribution: {state.luckContribution}";
        }

        // 簡單的隊伍總血量預覽：先假設前 3 隻是隊伍
        if (partyStatsText != null)
        {
            int partyCount = Mathf.Min(3, runtimeStates.Count);
            int totalMaxHP = 0;
            int totalLuck = 0;
            for (int i = 0; i < partyCount; i++)
            {
                totalMaxHP += runtimeStates[i].maxHP;
                totalLuck += runtimeStates[i].luckContribution;
            }

            partyStatsText.text =
                $"Party Preview (前 {partyCount} 角色)\n" +
                $"Total MaxHP: {totalMaxHP}\n" +
                $"Total Luck: {totalLuck}";
        }
    }
}
