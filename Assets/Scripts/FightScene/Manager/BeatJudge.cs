using UnityEngine;

public class BeatJudge : MonoBehaviour
{
    [Header("判定範圍 (秒)")]
    public float perfectRange = 0.05f;
    public float greatRange = 0.1f;

    public static BeatJudge Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    public enum HitResult { Miss, Great, Perfect }

    public float TryHit(string lane)
    {
        float musicTime = MusicManager.Instance.GetMusicTime();
        BeatUI targetBeat = BeatManager.Instance.FindClosestBeat(musicTime);
        if (targetBeat == null) return 0f; // 沒有節拍 → 判定 Miss

        float delta = Mathf.Abs(musicTime - targetBeat.GetNoteTime());

        if (delta <= perfectRange)
        {
            BeatManager.Instance.RemoveBeat(targetBeat.gameObject);
            return 1f; // Perfect → 100% 傷害
        }
        else if (delta <= greatRange)
        {
            BeatManager.Instance.RemoveBeat(targetBeat.gameObject);
            return 1f; // Great 也算滿傷害
        }
        else
        {
            return 0f; // Miss → 0% 傷害，但仍攻擊
        }
    }



    // 根據 Lane 找到我方角色
    private BattleManager.TeamSlotInfo GetAttackerFromLane(string lane)
    {
        int keyIndex = -1;
        switch (lane)
        {
            case "E": keyIndex = 0; break; // 你的 AssignedKeyIndex 規則：0=E,1=W,2=Q
            case "W": keyIndex = 1; break;
            case "Q": keyIndex = 2; break;
        }

        foreach (var slot in BattleManager.Instance.CTeamInfo)
        {
            if (slot.AssignedKeyIndex == keyIndex)
                return slot;
        }
        return null;
    }

    // 這裡簡單做「同索引對位攻擊」
    private BattleManager.TeamSlotInfo GetTargetFromLane(string lane)
    {
        int keyIndex = -1;
        switch (lane)
        {
            case "E": keyIndex = 0; break;
            case "W": keyIndex = 1; break;
            case "Q": keyIndex = 2; break;
        }

        return BattleManager.Instance.ETeamInfo[keyIndex];
    }
}
