using System.Collections;
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
    public int attackDamage = 20;
    public float minAttackInterval = 0f;
    public float maxAttackInterval = 10f;
    public int slotIndex = 0;
    public float dashDuration = 0.1f;
    public float actionLockDuration = 0.3f;

    [Header("�S�ĳ]�w")]
    public GameObject attackVfxPrefab;
    public float vfxLifetime = 1.5f;

    [Header("ĵ�ܳ]�w")]
    public int warningBeats = 2; // ���e�X�� Beat ĵ��
    public Color warningColor = Color.red;

    private float phase = 0f;
    private Vector3 basePosLocal;
    private Vector3 basePosWorld;
    private Vector3 targetScale;

    private float nextAttackTime;
    private float warningTime; // ��ɶ}�l�ܬ�
    private bool readyToAttack = false;
    private bool isAttacking = false;
    private bool isWarning = false;

    private SpriteRenderer spriteRenderer;
    private Color originalColor;

    private BattleManager.TeamSlotInfo selfSlot => BattleManager.Instance.ETeamInfo[slotIndex];
    private BattleManager.TeamSlotInfo targetSlot => BattleManager.Instance.CTeamInfo[slotIndex];

    void OnEnable()
    {
        basePosLocal = transform.localPosition;
        basePosWorld = transform.position;
        phase = randomizePhase ? Random.Range(0f, Mathf.PI * 2f) : 0f;

        transform.localScale = baseScale;
        targetScale = baseScale;

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;

        BeatManager.OnBeat += OnBeat;
    }
    void OnDisable()
    {
        BeatManager.OnBeat -= OnBeat;
    }

    void Start()
    {
        // BeatManager �w�g���� Start()�AInstance �O�Ҧs�b
        ScheduleNextAttack();
    }



    void Update()
    {
        if (isAttacking) return;

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

        // ��Fĵ�ܮɶ� �� �ܬ�
        if (!isWarning && Time.time >= warningTime)
        {
            if (spriteRenderer != null)
                spriteRenderer.color = warningColor;
            isWarning = true;
        }

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

        // �� �������e�@�� �� Y �Y��� 1.5 ��
        float beatInterval = 60f / BeatManager.Instance.bpm;
        if (!readyToAttack && Time.time + beatInterval >= nextAttackTime)
        {
            Vector3 s = transform.localScale;
            s.y = baseScale.y * 3f;
            transform.localScale = s;
        }

        // �p�G�N�o�����A�N����
        if (readyToAttack && targetSlot != null && targetSlot.Actor != null)
        {
            StartCoroutine(AttackSequence());
        }
    }


    private IEnumerator AttackSequence()
    {
        isAttacking = true;
        readyToAttack = false;

        Debug.Log($"�v�ܩi(slot {slotIndex}) �bBeat�W�o�ʧ��� �� {targetSlot.UnitName}");

        Vector3 origin = transform.position;
        Vector3 contactPoint = targetSlot.Actor.transform.position - BattleManager.Instance.meleeContactOffset;

        yield return Dash(origin, contactPoint, dashDuration);

        if (attackVfxPrefab != null)
        {
            var vfx = Instantiate(attackVfxPrefab, targetSlot.Actor.transform.position, Quaternion.identity);
            if (vfxLifetime > 0f) Destroy(vfx, vfxLifetime);
        }

        if (BattleEffectManager.Instance != null)
        {
            // �� �ĤH���� �� �Τ@�浹 BattleEffectManager �B�z (�~�|�Y�����)
            BattleEffectManager.Instance.OnHit(selfSlot, targetSlot, true);
        }
        else
        {
            // �ƥΡG��������
            targetSlot.HP -= attackDamage;
            if (targetSlot.HP < 0) targetSlot.HP = 0;
        }


        yield return new WaitForSeconds(actionLockDuration);

        transform.position = origin;

        // ��_�C��
        if (spriteRenderer != null)
            spriteRenderer.color = originalColor;
        isWarning = false;

        ScheduleNextAttack();
        isAttacking = false;
    }

    private IEnumerator Dash(Vector3 from, Vector3 to, float duration)
    {
        if (duration <= 0f)
        {
            transform.position = to;
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            if (t > 1f) t = 1f;
            transform.position = Vector3.Lerp(from, to, t);
            yield return null;
        }
    }

    private void ScheduleNextAttack()
    {
        float wait = Random.Range(minAttackInterval, maxAttackInterval);
        nextAttackTime = Time.time + wait;

        // ���ⴣ�eĵ�ܮɶ� (�� BeatManager �����j)
        float beatInterval = 60f / BeatManager.Instance.bpm;
        warningTime = nextAttackTime - warningBeats * beatInterval;
        if (warningTime < Time.time) warningTime = Time.time; // ���n��{�b��
    }

    void OnValidate()
    {
        amplitude = Mathf.Max(0f, amplitude);
        speed = Mathf.Max(0f, speed);
        minAttackInterval = Mathf.Max(0f, minAttackInterval);
        maxAttackInterval = Mathf.Max(minAttackInterval, maxAttackInterval);
    }
}
