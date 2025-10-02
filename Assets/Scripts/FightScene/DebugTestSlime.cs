using UnityEngine;

public class DebugTestSlime : MonoBehaviour
{
    [Header("���k�L�ʰѼ�")]
    public float amplitude = 0.05f;
    public float speed = 1.5f;
    public bool useLocalSpace = true;
    public bool randomizePhase = true;

    [Header("�`���Y��Ѽ�")]
    public Vector3 baseScale = new Vector3(0.15f, 0.15f, 0.15f);
    public float beatScaleMultiplier = 1.2f;
    public float scaleLerpSpeed = 6f;

    [Header("�����]�w")]
    public int attackDamage = 20;           // �ˮ`�ƭ�
    public float minAttackInterval = 0f;    // �̤p�N�o���
    public float maxAttackInterval = 10f;   // �̤j�N�o���
    public int slotIndex = 0;               // �o�ӥv�ܩi�ݩ�Ĥ誺�ĴX�� (0,1,2)

    private float phase = 0f;
    private Vector3 basePosLocal;
    private Vector3 basePosWorld;
    private Vector3 targetScale;

    private float nextAttackTime;
    private bool readyToAttack = false;

    private BattleManager.TeamSlotInfo selfSlot => BattleManager.Instance.ETeamInfo[slotIndex];
    private BattleManager.TeamSlotInfo targetSlot => BattleManager.Instance.CTeamInfo[slotIndex];

    void OnEnable()
    {
        basePosLocal = transform.localPosition;
        basePosWorld = transform.position;
        phase = randomizePhase ? Random.Range(0f, Mathf.PI * 2f) : 0f;

        transform.localScale = baseScale;
        targetScale = baseScale;

        BeatManager.OnBeat += OnBeat;

        ScheduleNextAttack();
    }

    void OnDisable()
    {
        BeatManager.OnBeat -= OnBeat;
    }

    void Update()
    {
        // ���k�\��
        float offsetX = Mathf.Sin((Time.unscaledTime + phase) * speed) * amplitude;

        if (useLocalSpace)
        {
            Vector3 p = basePosLocal;
            p.x += offsetX;
            transform.localPosition = p;
        }
        else
        {
            Vector3 p = basePosWorld;
            p.x += offsetX;
            transform.position = p;
        }

        // �����Y��
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * scaleLerpSpeed);

        // �N�o�ˬd
        if (Time.time >= nextAttackTime)
        {
            readyToAttack = true;
        }
    }

    private void OnBeat()
    {
        // Beat �Y��ʵe
        targetScale = baseScale * beatScaleMultiplier;
        transform.localScale = baseScale;

        // �p�G�N�o�����A�N����
        if (readyToAttack && targetSlot != null && targetSlot.Actor != null)
        {
            AttackTarget();
        }
    }

    private void AttackTarget()
    {
        // �ϥ� BattleManager ���y�{�i�����
        Debug.Log($"�v�ܩi(slot {slotIndex}) �bBeat�W�o�ʧ��� �� {targetSlot.UnitName}");

        // ��������]�o�̬O��²�檺�@�k�^
        targetSlot.HP -= attackDamage;
        if (targetSlot.HP < 0) targetSlot.HP = 0;

        // ���s�Ƶ{
        readyToAttack = false;
        ScheduleNextAttack();
    }

    private void ScheduleNextAttack()
    {
        float wait = Random.Range(minAttackInterval, maxAttackInterval);
        nextAttackTime = Time.time + wait;
    }

    void OnValidate()
    {
        amplitude = Mathf.Max(0f, amplitude);
        speed = Mathf.Max(0f, speed);
        minAttackInterval = Mathf.Max(0f, minAttackInterval);
        maxAttackInterval = Mathf.Max(minAttackInterval, maxAttackInterval);
    }
}
