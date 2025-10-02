using UnityEngine;

public class BeatJudge : MonoBehaviour
{
    [Header("�P�w�d�� (��)")]
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
        if (targetBeat == null) return 0f; // �S���`�� �� �P�w Miss

        float delta = Mathf.Abs(musicTime - targetBeat.GetNoteTime());

        if (delta <= perfectRange)
        {
            BeatManager.Instance.RemoveBeat(targetBeat.gameObject);
            return 1f; // Perfect �� 100% �ˮ`
        }
        else if (delta <= greatRange)
        {
            BeatManager.Instance.RemoveBeat(targetBeat.gameObject);
            return 1f; // Great �]�⺡�ˮ`
        }
        else
        {
            return 0f; // Miss �� 0% �ˮ`�A��������
        }
    }



    // �ھ� Lane ���ڤ訤��
    private BattleManager.TeamSlotInfo GetAttackerFromLane(string lane)
    {
        int keyIndex = -1;
        switch (lane)
        {
            case "E": keyIndex = 0; break; // �A�� AssignedKeyIndex �W�h�G0=E,1=W,2=Q
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

    // �o��²�氵�u�P���޹������v
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
