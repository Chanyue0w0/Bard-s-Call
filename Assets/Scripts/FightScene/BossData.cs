using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BossActionData
{
    [Header("行為識別")]
    [Tooltip("例如 BACT_NORMAL_01、BACT_GUARD、BACT_HEAVY")]
    public string actionId;

    [Tooltip("編輯器中方便閱讀的名稱")]
    public string displayName;

    [Header("BeatSpriteAnimator Clip")]
    [Tooltip("必須與 BeatSpriteAnimator 裡的 clipName 完全相同")]
    public string animationClipName;

    [Header("行為 Prefab")]
    [Tooltip("攻擊特效、技能 Prefab 或其他行為物件，可先留空")]
    public GameObject actionPrefab;
}

public class BossData : MonoBehaviour
{
    [Header("魔王基本資料")]
    public string bossId = "BOSS_JOHN_WEAK";
    public string bossName = "John Weak";

    [Header("共用動畫")]
    [Tooltip("BeatSpriteAnimator 的待機 Clip 名稱")]
    public string idleClipName = "Idle";

    [Tooltip("輸入 Miss 時播放的 Clip，可留空")]
    public string missClipName = "Miss";

    [Header("普通攻擊連段")]
    [Tooltip("目前已解鎖的普通攻擊段數")]
    [Range(1, 4)]
    public int unlockedNormalAttackCount = 3;

    [Tooltip("依序放入第1、2、3、4段普通攻擊")]
    public List<BossActionData> normalAttacks =
        new List<BossActionData>();

    [Header("格擋")]
    public BossActionData guardAction;

    [Header("重擊")]
    public BossActionData heavyAttack;

    /// <summary>
    /// 取得目前實際可使用的普通攻擊段數。
    /// 避免解鎖數量超過資料清單長度。
    /// </summary>
    public int GetUsableNormalAttackCount()
    {
        if (normalAttacks == null)
            return 0;

        return Mathf.Clamp(
            unlockedNormalAttackCount,
            0,
            normalAttacks.Count
        );
    }

    /// <summary>
    /// 取得指定段數的普通攻擊。
    /// index 使用 0 起算。
    /// </summary>
    public BossActionData GetNormalAttack(int index)
    {
        int usableCount = GetUsableNormalAttackCount();

        if (usableCount <= 0)
            return null;

        if (index < 0 || index >= usableCount)
            return null;

        return normalAttacks[index];
    }

    private void OnValidate()
    {
        if (normalAttacks == null)
            normalAttacks = new List<BossActionData>();

        unlockedNormalAttackCount = Mathf.Clamp(
            unlockedNormalAttackCount,
            1,
            4
        );
    }
}