using UnityEngine;

public class BossData : MonoBehaviour
{
    [Header("魔王基本資料")]
    public string bossId = "BOSS_JOHN_WEAK";
    public string bossName = "John Weak";

    [Header("普通攻擊連段")]
    [Tooltip("目前可使用的普通攻擊段數，初期為3，升級後可改為4")]
    [Range(1, 4)]
    public int unlockedNormalAttackCount = 3;

    private void OnValidate()
    {
        unlockedNormalAttackCount = Mathf.Clamp(
            unlockedNormalAttackCount,
            1,
            4
        );
    }
}