using UnityEngine;

public class MusicManager : MonoBehaviour
{
    // 單例
    public static MusicManager Instance { get; private set; }

    private AudioSource audioSource;

    [Header("音樂設定")]
    public AudioClip defaultClip;   // 預設音樂
    [Range(0f, 2f)]
    public float pitch = 1.0f;      // 音高（變速用）

    private void Awake()
    {
        // 建立 Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 確保不會被銷毀
        //DontDestroyOnLoad(gameObject);

        // 初始化 AudioSource
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    private void Start()
    {
        MusicManager.Instance.PlayMusic();

    }

    private void Update()
    {
        // 動態調整 pitch（可以在 Inspector 上改變）
        if (audioSource != null)
        {
            audioSource.pitch = pitch;
        }
    }

    // 播放音樂
    public void PlayMusic(AudioClip clip = null, bool loop = true)
    {
        if (clip == null) clip = defaultClip;
        if (clip == null) return;

        audioSource.clip = clip;
        audioSource.loop = loop;
        audioSource.Play();
    }

    // 暫停音樂
    public void PauseMusic()
    {
        if (audioSource.isPlaying)
        {
            audioSource.Pause();
        }
    }

    // 繼續播放
    public void ResumeMusic()
    {
        if (!audioSource.isPlaying)
        {
            audioSource.UnPause();
        }
    }

    // 停止音樂
    public void StopMusic()
    {
        audioSource.Stop();
    }

    // 換曲
    public void ChangeMusic(AudioClip newClip, bool loop = true)
    {
        PlayMusic(newClip, loop);
    }

    // 取得當前播放時間（秒）
    public float GetMusicTime()
    {
        return audioSource.time;
    }

    // 是否正在播放
    public bool IsPlaying()
    {
        return audioSource.isPlaying;
    }
}
