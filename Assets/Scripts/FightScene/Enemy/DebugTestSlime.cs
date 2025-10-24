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
    public int warningBeats = 2;
    public Color warningColor = Color.red;

    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private bool isHolding = false;
    private float holdTimer = 0f;
    private bool isAttacking = false;
    private bool readyToAttack = false;
    private bool isWarning = false;
    private float nextAttackTime;
    private float warningTime;
    private GameObject activeTargetWarning;

    protected override void Awake()
    {
        base.Awake(); // �� �I�s�����O�۰ʤ��t index
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;
    }

    void Start()
    {
        ScheduleNextAttack();
        //nextAttackTime = Time.time + Random.Range(1f, 3f);

    }

    void Update()
    {
        if (forceMove || isAttacking) return; // �� �s�W forceMove �ˬd

        if (isAttacking) return;

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
                Mathf.SmoothStep(0f, 1f, Time.unscaledDeltaTime * returnSpeed)
            );
        }

        if (!isWarning && Time.time >= warningTime)
        {
            if (spriteRenderer != null)
                spriteRenderer.color = warningColor;
            isWarning = true;
        }

        if (Time.time >= nextAttackTime)
            readyToAttack = true;
    }

    // �� ��@�����O����H OnBeat
    protected override void OnBeat()
    {
        if (forceMove) return; // �� �Ȱ�������Ĳ�o�`��欰

        transform.localScale = baseScale * peakMultiplier;
        isHolding = true;
        holdTimer = holdDuration;

        if (readyToAttack && targetSlot != null && targetSlot.Actor != null)
        {
            StartCoroutine(AttackSequence());
        }
    }

    private IEnumerator AttackSequence()
    {
        isAttacking = true;

        Vector3 origin = transform.position;
        Vector3 contactPoint = targetSlot.Actor.transform.position - BattleManager.Instance.meleeContactOffset;

        yield return Dash(origin, contactPoint, dashDuration);

        if (attackVfxPrefab != null)
        {
            var vfx = Instantiate(attackVfxPrefab, targetSlot.Actor.transform.position, Quaternion.identity);
            if (vfxLifetime > 0f) Destroy(vfx, vfxLifetime);
        }

        BattleEffectManager.Instance?.OnHit(selfSlot, targetSlot, true);

        yield return new WaitForSeconds(actionLockDuration);

        transform.position = origin;

        if (spriteRenderer != null)
            spriteRenderer.color = originalColor;
        isWarning = false;

        ScheduleNextAttack();
        isAttacking = false;
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
        if (warningTime < Time.time) warningTime = Time.time;
    }

    public void RefreshBasePosition()
    {
        basePosLocal = transform.localPosition;
        basePosWorld = transform.position;
    }
}

