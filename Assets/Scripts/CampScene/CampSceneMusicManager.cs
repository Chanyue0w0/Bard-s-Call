using UnityEngine;

public class CampSceneMusicManager : MonoBehaviour
{
    [Header("背景音樂設定")]
    public AudioClip campBGM;   // 指定 .ogg 音樂檔
    [Range(0f, 1f)]
    public float volume = 0.7f; // 預設音量

    private AudioSource audioSource;

    void Start()
    {
        // 檢查是否已存在 AudioSource，沒有則自動新增
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        // 設定音源屬性
        audioSource.clip = campBGM;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.volume = volume;

        // 開始播放
        if (campBGM != null)
            audioSource.Play();
        else
            Debug.LogWarning("CampSceneMusicManager: 未指定 campBGM！");
    }

    // 若想手動停止播放，可呼叫這個方法
    public void StopMusic()
    {
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }

    // 若想重新播放，可呼叫這個方法
    public void PlayMusic()
    {
        if (audioSource != null && !audioSource.isPlaying)
            audioSource.Play();
    }
}
