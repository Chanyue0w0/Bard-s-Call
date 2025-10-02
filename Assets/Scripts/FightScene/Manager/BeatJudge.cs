using UnityEngine;

public class BeatJudge : MonoBehaviour
{
    [Header("�P�w�d�� (��)")]
    public float perfectRange = 0.05f;

    [Header("�S�� UI Prefab")]
    public GameObject beatHitLightUIPrefab; // Perfect �R���S��
    public RectTransform beatHitPointUI;    // �����I UI ����m (Canvas �U)

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
        bool perfect = delta <= perfectRange;

        if (perfect)
        {
            SpawnPerfectEffect();
        }

        return perfect;
    }

    private void SpawnPerfectEffect()
    {
        if (beatHitLightUIPrefab == null || beatHitPointUI == null) return;

        // �ͦ��b Canvas �U�A��m��������I
        GameObject effect = Instantiate(beatHitLightUIPrefab, beatHitPointUI.parent);
        RectTransform effectRect = effect.GetComponent<RectTransform>();
        if (effectRect != null)
        {
            effectRect.anchoredPosition = beatHitPointUI.anchoredPosition;
        }

        // �۰ʾP���A�קK��n
        Destroy(effect, 0.5f);
    }
}
