using UnityEngine;

public class MusicManager : MonoBehaviour
{
    // ���
    public static MusicManager Instance { get; private set; }

    private AudioSource audioSource;

    [Header("���ֳ]�w")]
    public AudioClip defaultClip;   // �w�]����
    [Range(0f, 2f)]
    public float pitch = 1.0f;      // �����]�ܳt�Ρ^
    [Range(0f, 1f)]
    public float volume = 1.0f;     // ���q�j�p

    private void Awake()
    {
        // �إ� Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // ��l�� AudioSource
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    private void Start()
    {
        MusicManager.Instance.PlayMusic();
    }

    private void Update()
    {
        // �ʺA�վ� pitch & volume
        if (audioSource != null)
        {
            audioSource.pitch = pitch;
            audioSource.volume = volume;
        }
    }

    // ���񭵼�
    public void PlayMusic(AudioClip clip = null, bool loop = true)
    {
        if (clip == null) clip = defaultClip;
        if (clip == null) return;

        audioSource.clip = clip;
        audioSource.loop = loop;
        audioSource.Play();
    }

    // �Ȱ�����
    public void PauseMusic()
    {
        if (audioSource.isPlaying)
        {
            audioSource.Pause();
        }
    }

    // �~�򼽩�
    public void ResumeMusic()
    {
        if (!audioSource.isPlaying)
        {
            audioSource.UnPause();
        }
    }

    // �����
    public void StopMusic()
    {
        audioSource.Stop();
    }

    // ����
    public void ChangeMusic(AudioClip newClip, bool loop = true)
    {
        PlayMusic(newClip, loop);
    }

    // ���o��e����ɶ��]��^
    public float GetMusicTime()
    {
        return audioSource.time;
    }

    // �O�_���b����
    public bool IsPlaying()
    {
        return audioSource.isPlaying;
    }

    // ���ѵ{���I�s���f�ӧﭵ�q
    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume); // ����b 0~1
        if (audioSource != null)
            audioSource.volume = volume;
    }

    public float GetVolume()
    {
        return volume;
    }
}
