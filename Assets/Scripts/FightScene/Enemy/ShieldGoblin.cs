using UnityEngine;

public class ShieldGoblin : MonoBehaviour
{
    [Header("防禦狀態")]
    public bool isBlocking = true;   // 是否處於防禦中
    public bool isBroken = false;    // 是否已被破防

    private CharacterData charData;

    void Start()
    {
        charData = GetComponent<CharacterData>();
        if (charData == null)
        {
            Debug.LogWarning("ShieldGoblin 缺少 CharacterData 組件。");
            return;
        }

        // 啟動永久格檔
        BattleEffectManager.Instance.ActivateInfiniteBlock(gameObject, charData);
        Debug.Log("【ShieldGoblin】啟動永久格檔狀態");
    }

    // 被重攻擊命中時呼叫
    public void BreakShield()
    {
        if (isBroken) return;

        isBroken = true;
        isBlocking = false;

        // 移除格檔特效
        BattleEffectManager.Instance.RemoveBlockEffect(gameObject);

        // 可選：播放破防特效或動畫
        Debug.Log("【ShieldGoblin】防禦被重攻擊破壞！");
    }

    // 可選：提供外部查詢
    public bool IsBlocking()
    {
        return isBlocking && !isBroken;
    }
}
