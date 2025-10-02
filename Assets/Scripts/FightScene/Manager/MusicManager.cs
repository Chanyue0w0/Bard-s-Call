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

    private void Awake()
    {
        // �إ� Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // �T�O���|�Q�P��
        //DontDestroyOnLoad(gameObject);

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
        // �ʺA�վ� pitch�]�i�H�b Inspector �W���ܡ^
        if (audioSource != null)
        {
            audioSource.pitch = pitch;
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
}
