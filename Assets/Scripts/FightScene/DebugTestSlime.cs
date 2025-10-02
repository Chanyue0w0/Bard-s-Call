using System.Collections;
using UnityEngine;

public class DebugTestSlime : MonoBehaviour
{
    [Header("左右微動參數")]
    public float amplitude = 0.05f;
    public float speed = 1.5f;
    public bool useLocalSpace = true;
    public bool randomizePhase = true;

    [Header("節拍縮放參數")]
    public Vector3 baseScale = new Vector3(0.15f, 0.15f, 0.15f);
    public float beatScaleMultiplier = 1.2f;
    public float scaleLerpSpeed = 6f;

    [Header("攻擊設定")]
    public int attackDamage = 20;
    public float minAttackInterval = 0f;
    public float maxAttackInterval = 10f;
    public int slotIndex = 0;
    public float dashDuration = 0.1f;
    public float actionLockDuration = 0.3f;

    [Header("特效設定")]
    public GameObject attackVfxPrefab;
    public float vfxLifetime = 1.5f;

    [Header("警示設定")]
    public int warningBeats = 2; // 提前幾個 Beat 警示
    public Color warningColor = Color.red;

    private float phase = 0f;
    private Vector3 basePosLocal;
    private Vector3 basePosWorld;
    private Vector3 targetScale;

    private float nextAttackTime;
    private float warningTime; // 何時開始變紅
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
        // BeatManager 已經完成 Start()，Instance 保證存在
        ScheduleNextAttack();
    }



    void Update()
    {
        if (isAttacking) return;

        // 左右擺動
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

        // 平滑縮放
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * scaleLerpSpeed);

        // 到達警示時間 → 變紅
        if (!isWarning && Time.time >= warningTime)
        {
            if (spriteRenderer != null)
                spriteRenderer.color = warningColor;
            isWarning = true;
        }

        // 冷卻檢查
        if (Time.time >= nextAttackTime)
        {
            readyToAttack = true;
        }
    }

    private void OnBeat()
    {
        // Beat 縮放動畫
        targetScale = baseScale * beatScaleMultiplier;
        transform.localScale = baseScale;

        // ★ 攻擊的前一拍 → Y 縮放到 1.5 倍
        float beatInterval = 60f / BeatManager.Instance.bpm;
        if (!readyToAttack && Time.time + beatInterval >= nextAttackTime)
        {
            Vector3 s = transform.localScale;
            s.y = baseScale.y * 3f;
            transform.localScale = s;
        }

        // 如果冷卻結束，就攻擊
        if (readyToAttack && targetSlot != null && targetSlot.Actor != null)
        {
            StartCoroutine(AttackSequence());
        }
    }


    private IEnumerator AttackSequence()
    {
        isAttacking = true;
        readyToAttack = false;

        Debug.Log($"史萊姆(slot {slotIndex}) 在Beat上發動攻擊 → {targetSlot.UnitName}");

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
            // ★ 敵人攻擊 → 統一交給 BattleEffectManager 處理 (才會吃到格檔)
            BattleEffectManager.Instance.OnHit(selfSlot, targetSlot, true);
        }
        else
        {
            // 備用：直接扣血
            targetSlot.HP -= attackDamage;
            if (targetSlot.HP < 0) targetSlot.HP = 0;
        }


        yield return new WaitForSeconds(actionLockDuration);

        transform.position = origin;

        // 恢復顏色
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

        // 換算提前警示時間 (用 BeatManager 的間隔)
        float beatInterval = 60f / BeatManager.Instance.bpm;
        warningTime = nextAttackTime - warningBeats * beatInterval;
        if (warningTime < Time.time) warningTime = Time.time; // 不要比現在早
    }

    void OnValidate()
    {
        amplitude = Mathf.Max(0f, amplitude);
        speed = Mathf.Max(0f, speed);
        minAttackInterval = Mathf.Max(0f, minAttackInterval);
        maxAttackInterval = Mathf.Max(minAttackInterval, maxAttackInterval);
    }
}
