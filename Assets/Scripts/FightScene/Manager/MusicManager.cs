using UnityEngine;
using System;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    private AudioSource audioSource;

    [Header("音樂設定")]
    public AudioClip defaultClip;
    [Range(0f, 2f)] public float pitch = 1.0f;
    [Range(0f, 1f)] public float volume = 1.0f;
    public bool loop = true;
    public bool autoPlayOnStart = true; // ★ 新增：是否開場自動播放

    [Header("時間同步設定")]
    [Tooltip("修正播放延遲 (秒)。正值：延後節拍；負值：提前節拍。")]
    public float globalOffset = 0f;

    private double dspStartTime = 0;
    private bool isPlaying = false;

    // 事件
    public event Action OnMusicStart;
    public event Action OnMusicStop;
    public event Action OnMusicChange;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = loop;
    }

    private void Start()
    {
        // ★ 開場自動播放
        if (autoPlayOnStart && defaultClip != null)
        {
            PlayMusic(defaultClip, loop);
        }
    }

    private void Update()
    {
        if (audioSource == null) return;
        audioSource.pitch = pitch;
        audioSource.volume = volume;
    }

    // ==============================
    // 播放控制
    // ==============================
    public void PlayMusic(AudioClip clip = null, bool shouldLoop = true)
    {
        if (clip == null) clip = defaultClip;
        if (clip == null) return;

        loop = shouldLoop;
        audioSource.clip = clip;
        audioSource.loop = shouldLoop;

        // 等待下一個音訊更新幀開始播放，確保無排程延遲
        dspStartTime = AudioSettings.dspTime + 0.05;  // 提前預排 50ms 播放
        audioSource.PlayScheduled(dspStartTime);

        isPlaying = true;
        OnMusicStart?.Invoke();
    }

    public void PauseMusic()
    {
        if (!isPlaying) return;
        audioSource.Pause();
        isPlaying = false;
    }

    public void ResumeMusic()
    {
        if (isPlaying) return;
        audioSource.UnPause();
        isPlaying = true;
    }

    public void StopMusic()
    {
        if (!isPlaying) return;
        audioSource.Stop();
        isPlaying = false;
        OnMusicStop?.Invoke();
    }

    public void ChangeMusic(AudioClip newClip, bool shouldLoop = true)
    {
        StopMusic();
        PlayMusic(newClip, shouldLoop);
        OnMusicChange?.Invoke();
    }

    // ==============================
    // 時間與音量接口
    // ==============================
    public float GetMusicTime()
    {
        if (!isPlaying) return 0f;
        return (float)((AudioSettings.dspTime - dspStartTime) * pitch) + globalOffset;
    }

    public bool IsPlaying()
    {
        return isPlaying;
    }

    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);
        if (audioSource != null)
            audioSource.volume = volume;
    }

    public float GetVolume()
    {
        return volume;
    }

    public void SetPitch(float newPitch)
    {
        pitch = Mathf.Clamp(newPitch, 0.1f, 2f);
        if (audioSource != null)
            audioSource.pitch = pitch;
    }
}
