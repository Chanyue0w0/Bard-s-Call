using UnityEngine;

public class BeatJudge : MonoBehaviour
{
    [Header("�P�w�d�� (��)")]
    public float perfectRange = 0.05f;
    //public float greatRange = 0.1f;

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

    // �ˬd�O�_���]²�檩�^
    public bool IsOnBeat()
    {
        float musicTime = MusicManager.Instance.GetMusicTime();
        BeatUI targetBeat = BeatManager.Instance.FindClosestBeat(musicTime);
        if (targetBeat == null) return false;

        float delta = Mathf.Abs(musicTime - targetBeat.GetNoteTime());
        return delta <= perfectRange;
    }
}
