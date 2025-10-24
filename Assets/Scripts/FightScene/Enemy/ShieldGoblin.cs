using UnityEngine;

public class ShieldGoblin : EnemyBase
{
    [Header("防禦狀態")]
    public bool isBlocking = true;
    public bool isBroken = false;

    private CharacterData charData;

    protected override void Awake()
    {
        base.Awake(); // ★ 自動配對索引
    }

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

    public void BreakShield()
    {
        if (isBroken) return;

        isBroken = true;
        isBlocking = false;
        BattleEffectManager.Instance.RemoveBlockEffect(gameObject);
        Debug.Log("【ShieldGoblin】防禦被重攻擊破壞！");
    }

    public bool IsBlocking()
    {
        return isBlocking && !isBroken;
    }

    //protected override void OnBeat()
    //{
    //    if (forceMove) return; // ★ 疊帶中不動作
    //    // 可加呼吸動畫或特效
    //}
}
