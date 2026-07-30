using UnityEngine;

public class HeroCombatState : MonoBehaviour
{
    [Header("目前戰鬥狀態")]
    [SerializeField]
    private bool isBlocking;

    /// <summary>
    /// 勇者目前是否正在格擋。
    /// </summary>
    public bool IsBlocking => isBlocking;

    /// <summary>
    /// 開始格擋。
    /// </summary>
    public void BeginBlock()
    {
        isBlocking = true;

        Debug.Log(
            $"[HeroCombatState] {gameObject.name} 開始格擋。"
        );
    }

    /// <summary>
    /// 結束格擋。
    /// </summary>
    public void EndBlock()
    {
        isBlocking = false;

        Debug.Log(
            $"[HeroCombatState] {gameObject.name} 結束格擋。"
        );
    }

    /// <summary>
    /// 戰鬥初始化或重置時使用。
    /// </summary>
    public void ResetState()
    {
        isBlocking = false;
    }
}