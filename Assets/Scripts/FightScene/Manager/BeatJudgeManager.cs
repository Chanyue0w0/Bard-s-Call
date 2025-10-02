using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BeatJudgeManager : MonoBehaviour
{
    //[Header("判定範圍 (秒)")]
    //public float perfectRange = 0.05f;
    //public float greatRange = 0.1f;

    //public static BeatJudgeManager Instance { get; private set; }

    //private void Awake()
    //{
    //    if (Instance != null && Instance != this)
    //    {
    //        Destroy(gameObject);
    //        return;
    //    }
    //    Instance = this;
    //}

    //public void TryHit(string lane)
    //{
    //    float musicTime = MusicManager.Instance.GetMusicTime();

    //    BeatUI targetBeat = FindClosestBeat(lane);
    //    if (targetBeat == null) return;

    //    float delta = Mathf.Abs(musicTime - targetBeat.GetNoteTime());

    //    if (delta <= perfectRange)
    //    {
    //        Debug.Log("Perfect!");
    //        BeatManager.Instance.RemoveBeat(targetBeat.gameObject);
    //        // 通知攻擊程式
    //        BattleEffectManager.Instance.OnHit(/*傳 attacker/target 參數*/);
    //    }
    //    else if (delta <= greatRange)
    //    {
    //        Debug.Log("Great!");
    //        BeatManager.Instance.RemoveBeat(targetBeat.gameObject);
    //        BattleEffectManager.Instance.OnHit(/*attacker, target, 0.8f*/);
    //    }
    //    else
    //    {
    //        Debug.Log("Miss!");
    //    }
    //}

    //private BeatUI FindClosestBeat(string lane)
    //{
    //    float musicTime = MusicManager.Instance.GetMusicTime();

    //    BeatUI closest = null;
    //    float minDelta = Mathf.Infinity;

    //    foreach (var beatObj in BeatManager.Instance.activeBeats)
    //    {
    //        if (beatObj == null) continue;

    //        BeatUI beat = beatObj.GetComponent<BeatUI>();
    //        float delta = Mathf.Abs(musicTime - beat.GetNoteTime());

    //        if (delta < minDelta)
    //        {
    //            minDelta = delta;
    //            closest = beat;
    //        }
    //    }
    //    return closest;
    //}
}
