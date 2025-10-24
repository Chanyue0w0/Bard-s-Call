using System.Collections;
using UnityEngine;

public class DebugTestSlime : EnemyBase
{
    [Header("�򥻼ƭ�")]
    public int maxHP = 50;
    public int hp = 50;
    public float respawnDelay = 10f;

    [Header("�`���Y��Ѽ�")]
    public Vector3 baseScale = new Vector3(0.15f, 0.15f, 0.15f);
    public float peakMultiplier = 1.3f;
    public float holdDuration = 0.05f;
    public float returnSpeed = 8f;

    [Header("�����]�w")]
    public int attackDamage = 20;
    public float dashDuration = 0.1f;
    public float actionLockDuration = 0.3f;

    [Header("�S�ĳ]�w")]
    public GameObject attackVfxPrefab;
    public float vfxLifetime = 1.5f;

    [Header("ĵ�ܳ]�w")]
    public int warningBeats = 3;
    public Color warningColor = Color.red;
    public GameObject targetWarningPrefab;

    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private bool isHolding = false;
    private float holdTimer = 0f;
    private bool isAttacking = false;
    private bool isWarning = false;

    private float nextAttackTime;
    private float warningTime;
    private GameObject activeTargetWarning;
    private int beatsBeforeAttack = -1;

    protected override void Awake()
    {
        base.Awake();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;
    }

    void Start()
    {
        ScheduleNextAttack();

        // �� �s�W�G�q�\ BeatManager ���I�ƥ�
       
        BeatManager.OnBeat += OnBeat;
    }

    void OnDestroy()
    {
        // �� �s�W�G�����q�\�A�קK������������
        BeatManager.OnBeat -= OnBeat;
    }


    void Update()
    {
        if (forceMove || isAttacking) return;

        // �^�_�Y��
        if (isHolding)
        {
            holdTimer -= Time.unscaledDeltaTime;
            if (holdTimer <= 0f)
                isHolding = false;
        }
        else
        {
            transform.localScale = Vector3.Lerp(
                transform.localScale,
                baseScale,
                Time.unscaledDeltaTime * returnSpeed
            );
        }

        // �Y��Fĵ�ܮɶ����٨S�i�Jĵ�ܶ��q
        if (!isWarning && Time.time >= warningTime)
        {
            EnterWarningPhase();
        }

        // �Y�������j�ɶ��w������i�Jĵ�ܡ]�w���ˬd�^
        if (!isWarning && !isAttacking && Time.time >= nextAttackTime)
        {
            ScheduleNextAttack();
        }
    }

    // �C��Ĳ�o
    private void OnBeat()
    {
        if (forceMove || isAttacking) return;

        transform.localScale = baseScale * peakMultiplier;
        isHolding = true;
        holdTimer = holdDuration;

        if (isWarning)
        {
            beatsBeforeAttack--;

            if (beatsBeforeAttack == 1)
            {
                transform.localScale = baseScale * (peakMultiplier + 0.4f);
            }

            if (beatsBeforeAttack <= 0)
            {
                StartCoroutine(AttackSequence());
            }
        }
    }

    private void EnterWarningPhase()
    {
        isWarning = true;
        beatsBeforeAttack = warningBeats;

        if (spriteRenderer != null)
            spriteRenderer.color = warningColor;

        if (targetSlot != null && targetSlot.Actor != null && targetWarningPrefab != null)
        {
            activeTargetWarning = Instantiate(
                targetWarningPrefab,
                targetSlot.Actor.transform.position,
                Quaternion.identity
            );
        }
    }

    private IEnumerator AttackSequence()
    {
        isAttacking = true;

        Vector3 origin = transform.position;
        Vector3 contactPoint = targetSlot.Actor.transform.position - BattleManager.Instance.meleeContactOffset;

        // ���ʬ�i
        yield return Dash(origin, contactPoint, dashDuration);

        // �ͦ������S��
        if (attackVfxPrefab != null)
        {
            var vfx = Instantiate(attackVfxPrefab, targetSlot.Actor.transform.position, Quaternion.identity);
            if (vfxLifetime > 0f)
                Destroy(vfx, vfxLifetime);
        }

        // �ˮ`�P�w
        BattleEffectManager.Instance?.OnHit(selfSlot, targetSlot, true);

        // �����]���y�^
        yield return new WaitForSeconds(actionLockDuration);

        // ���Ʀ^���
        yield return Dash(transform.position, origin, dashDuration);

        // ���A���m
        if (spriteRenderer != null)
            spriteRenderer.color = originalColor;

        if (activeTargetWarning != null)
            Destroy(activeTargetWarning);

        isWarning = false;
        isAttacking = false;

        // ���s�w�ƤU�@������
        ScheduleNextAttack();
    }

    private IEnumerator Dash(Vector3 from, Vector3 to, float duration)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.position = Vector3.Lerp(from, to, t);
            yield return null;
        }
    }

    private void ScheduleNextAttack()
    {
        float wait = Random.Range(2f, 5f);
        nextAttackTime = Time.time + wait;

        float beatInterval = 60f / BeatManager.Instance.bpm;
        warningTime = nextAttackTime - warningBeats * beatInterval;

        // �Yĵ�ܮɶ��w�g�L �� �ߧY�i�Jĵ��
        if (warningTime <= Time.time)
            warningTime = Time.time;

        // ���\���s�i�Jĵ��
        isWarning = false;
    }

    public void RefreshBasePosition()
    {
        basePosLocal = transform.localPosition;
        basePosWorld = transform.position;
    }
}
