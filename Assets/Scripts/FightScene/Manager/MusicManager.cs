using UnityEngine;
using System;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    private AudioSource audioSource;

    [Header("���ֳ]�w")]
    public AudioClip defaultClip;
    [Range(0f, 2f)] public float pitch = 1.0f;
    [Range(0f, 1f)] public float volume = 1.0f;
    public bool loop = true;
    public bool autoPlayOnStart = true; // �� �s�W�G�O�_�}���۰ʼ���

    [Header("�ɶ��P�B�]�w")]
    [Tooltip("�ץ����񩵿� (��)�C���ȡG����`��F�t�ȡG���e�`��C")]
    public float globalOffset = 0f;

    private double dspStartTime = 0;
    private bool isPlaying = false;

    // �ƥ�
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
        // �� �}���۰ʼ���
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
    // ���񱱨�
    // ==============================
    public void PlayMusic(AudioClip clip = null, bool shouldLoop = true)
    {
        if (clip == null) clip = defaultClip;
        if (clip == null) return;

        loop = shouldLoop;
        audioSource.clip = clip;
        audioSource.loop = shouldLoop;

        // ���ݤU�@�ӭ��T��s�V�}�l����A�T�O�L�Ƶ{����
        dspStartTime = AudioSettings.dspTime + 0.05;  // ���e�w�� 50ms ����
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
    // �ɶ��P���q���f
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
