using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using System;
using System.Runtime.InteropServices;

public class FMODBeatListener : MonoBehaviour
{
    // 主音樂事件
    public EventReference musicEvent;

    // 每拍要播放的音效事件
    public EventReference beatSFXEvent;

    private EventInstance musicInstance;
    private static bool beatTriggered = false;

    private FMOD.Studio.EVENT_CALLBACK beatCallback;
    private GCHandle timelineHandle;

    [StructLayout(LayoutKind.Sequential)]
    public struct TimelineInfo
    {
        public int currentBar;
        public int currentBeat;
        public float tempo;
        public int timeSigUpper;
        public int timeSigLower;
    }

    private static TimelineInfo timelineInfo;

    void Start()
    {
        // 建立音樂事件
        musicInstance = RuntimeManager.CreateInstance(musicEvent);

        // 綁定拍點回呼
        beatCallback = new FMOD.Studio.EVENT_CALLBACK(BeatEventCallback);
        timelineHandle = GCHandle.Alloc(timelineInfo, GCHandleType.Pinned);
        musicInstance.setUserData(GCHandle.ToIntPtr(timelineHandle));
        musicInstance.setCallback(beatCallback, EVENT_CALLBACK_TYPE.TIMELINE_BEAT);

        // 播放音樂
        musicInstance.start();
        musicInstance.release();
    }

    void Update()
    {
        // 每拍播放一次 SFX
        if (beatTriggered)
        {
            beatTriggered = false;
            RuntimeManager.PlayOneShot(beatSFXEvent);
        }
    }

    [AOT.MonoPInvokeCallback(typeof(FMOD.Studio.EVENT_CALLBACK))]
    private static FMOD.RESULT BeatEventCallback(EVENT_CALLBACK_TYPE type, IntPtr eventInstance, IntPtr parameters)
    {
        if (type != EVENT_CALLBACK_TYPE.TIMELINE_BEAT)
            return FMOD.RESULT.OK;

        var beatProps = (TIMELINE_BEAT_PROPERTIES)Marshal.PtrToStructure(parameters, typeof(TIMELINE_BEAT_PROPERTIES));
        Debug.Log($"[FMOD] Beat! Bar: {beatProps.bar}, Beat: {beatProps.beat}, Tempo: {beatProps.tempo}");

        // 設旗標給主執行緒觸發音效
        beatTriggered = true;

        return FMOD.RESULT.OK;
    }

    void OnDestroy()
    {
        musicInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        if (timelineHandle.IsAllocated)
            timelineHandle.Free();
    }
}
