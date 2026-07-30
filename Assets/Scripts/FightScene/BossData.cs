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

    // ============================================================
    // 普通攻擊 VFX
    // ============================================================

    [Header("普通攻擊 VFX Library")]
    [Tooltip("普通攻擊可使用的所有 VFX")]
    public GameObject[] normalAttackVfxPrefabs;

    [Tooltip("各 VFX 對應的預設 Offset")]
    public Vector3[] normalAttackVfxOffsets;

    [Header("普通攻擊 VFX 指定")]
    [Tooltip("每段普通攻擊使用哪個 VFX Index\n例如：0,0,1,2")]
    public int[] normalAttackVfxIndex;

    private void OnValidate()
    {
        unlockedNormalAttackCount = Mathf.Clamp(
            unlockedNormalAttackCount,
            1,
            4
        );
    }
}