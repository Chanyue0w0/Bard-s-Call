using System.Collections;
using UnityEngine;

public class DebugTestSlime : MonoBehaviour
{
    [Header("�򥻼ƭ�")]
    public int maxHP = 50;
    public int hp = 50;
    public float respawnDelay = 10f;

    [Header("���k�L�ʰѼ�")]
    public float amplitude = 0.05f;
    public float speed = 1.5f;
    public bool useLocalSpace = true;
    public bool randomizePhase = true;

    [Header("�`���Y��Ѽ� (�u�ƪ�)")]
    public Vector3 baseScale = new Vector3(0.15f, 0.15f, 0.15f);
    public float peakMultiplier = 1.3f;       // �`��������j����
    public float holdDuration = 0.05f;        // ��j�ᰱ�d�ɶ�
    public float returnSpeed = 8f;            // �^�_�t��

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
    public int warningBeats = 2;
    public Color warningColor = Color.red;

    private float phase = 0f;
    private Vector3 basePosLocal;
    private Vector3 basePosWorld;

    private bool isHolding = false;
    private float holdTimer = 0f;

    private float nextAttackTime;
    private float warningTime;
    private bool readyToAttack = false;
    private bool isAttacking = false;
    private bool isWarning = false;
    private bool isDead = false;

    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private Vector3 spawnPoint;
    private Collider2D col;

    private BattleManager.TeamSlotInfo selfSlot => BattleManager.Instance.ETeamInfo[slotIndex];
    private BattleManager.TeamSlotInfo targetSlot => BattleManager.Instance.CTeamInfo[slotIndex];

    void OnEnable()
    {
        basePosLocal = transform.localPosition;
        basePosWorld = transform.position;
        spawnPoint = transform.position;
        phase = randomizePhase ? Random.Range(0f, Mathf.PI * 2f) : 0f;

        transform.localScale = baseScale;

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;

        col = GetComponent<Collider2D>();

        BeatManager.OnBeat += OnBeat;
    }

    void OnDisable()
    {
        BeatManager.OnBeat -= OnBeat;
    }

    void Start()
    {
        ScheduleNextAttack();
    }

    void Update()
    {
        if (isDead || isAttacking) return;

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

        // �u�ƫ᪺�Y��`���G�O�� �� �^�_
        if (isHolding)
        {
            holdTimer -= Time.unscaledDeltaTime;
            if (holdTimer <= 0f)
            {
                isHolding = false;
            }
        }
        else
        {
            transform.localScale = Vector3.Lerp(
                transform.localScale,
                baseScale,
                Mathf.SmoothStep(0f, 1f, Time.unscaledDeltaTime * returnSpeed)
            );
        }

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
        if (isDead) return;

        // Beat�����u�u�_�v�� ���d �� �^�_
        transform.localScale = baseScale * peakMultiplier;
        isHolding = true;
        holdTimer = holdDuration;

        float beatInterval = 60f / BeatManager.Instance.bpm;
        if (!readyToAttack && Time.time + beatInterval >= nextAttackTime)
        {
            Vector3 s = transform.localScale;
            s.y = baseScale.y * 3f;
            transform.localScale = s;
        }

        if (readyToAttack && targetSlot != null && targetSlot.Actor != null)
        {
            StartCoroutine(AttackSequence());
        }
    }

    // ����
    public void TakeDamage(int dmg)
    {
        if (isDead) return;

        hp -= dmg;
        if (hp <= 0)
        {
            StartCoroutine(DieAndRespawn());
        }
    }

    private IEnumerator DieAndRespawn()
    {
        isDead = true;
        hp = 0;

        Debug.Log($"�v�ܩi(slot {slotIndex}) ���`�A{respawnDelay} ���_��");

        if (spriteRenderer != null) spriteRenderer.enabled = false;
        if (col != null) col.enabled = false;

        yield return new WaitForSeconds(respawnDelay);

        hp = maxHP;
        transform.position = spawnPoint;
        transform.localScale = baseScale;

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            spriteRenderer.color = originalColor;
        }
        if (col != null) col.enabled = true;

        isDead = false;
        ScheduleNextAttack();
    }

    private IEnumerator AttackSequence()
    {
        isAttacking = true;
        readyToAttack = false;

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
            BattleEffectManager.Instance.OnHit(selfSlot, targetSlot, true);
        }
        else
        {
            targetSlot.HP -= attackDamage;
            if (targetSlot.HP < 0) targetSlot.HP = 0;
        }

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

        float beatInterval = 60f / BeatManager.Instance.bpm;
        warningTime = nextAttackTime - warningBeats * beatInterval;
        if (warningTime < Time.time) warningTime = Time.time;
    }
}
