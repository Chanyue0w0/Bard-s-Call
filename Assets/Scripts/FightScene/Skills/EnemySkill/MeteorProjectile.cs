using UnityEngine;

public class MeteorProjectile : MonoBehaviour
{
    [Header("移動資料")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float lifetime = 5f;

    [Header("命中特效")]
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private Vector3 explosionOffset = new Vector3(0f, 0.5f, 0f);
    [SerializeField] private float explosionLifetime = 2f;

    [Header("命中加分")]
    [SerializeField] private int scoreGain = 100;

    private Vector3 moveDirection = Vector3.zero;
    private bool hasHit = false;

    public void Initialize(float angle, float speed, float meteorLifetime)
    {
        float angleRadians = angle * Mathf.Deg2Rad;
        moveDirection = new Vector3(Mathf.Cos(angleRadians), Mathf.Sin(angleRadians), 0f).normalized;
        moveSpeed = Mathf.Max(0f, speed);
        lifetime = meteorLifetime;

        if (lifetime > 0f) Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        transform.position += moveDirection * moveSpeed * Time.deltaTime;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit) return;

        if (!other.CompareTag("Hero")) return;

        hasHit = true;

        Vector3 hitPosition = other.bounds.center;

        if (explosionPrefab != null)
        {
            GameObject explosion = Instantiate(
                explosionPrefab,
                hitPosition + explosionOffset,
                Quaternion.identity
            );

            if (explosionLifetime > 0f)
                Destroy(explosion, explosionLifetime);
        }

        int addedScore = 0;

        if (ScoreManager.Instance != null)
            addedScore = ScoreManager.Instance.AddDirectScore(scoreGain);

        if (addedScore > 0 && DamageNumberManager.Instance != null)
        {
            DamageNumberManager.Instance.ShowScore(
                other.transform,
                addedScore
            );
        }

        Debug.Log($"[MeteorProjectile] 命中 Hero：{other.name}");

        Destroy(gameObject);
    }

    private int HitHero(Transform hitTarget)
    {
        hasHit = true;

        Vector3 explosionPosition = hitTarget != null ? hitTarget.position + explosionOffset : transform.position;

        if (explosionPrefab != null)
        {
            GameObject explosion = Instantiate(explosionPrefab, explosionPosition, Quaternion.identity);
            if (explosionLifetime > 0f) Destroy(explosion, explosionLifetime);
        }

        int addedScore = 0;

        if (ScoreManager.Instance != null) addedScore = ScoreManager.Instance.AddDirectScore(scoreGain);

        if (addedScore > 0 && DamageNumberManager.Instance != null && hitTarget != null) DamageNumberManager.Instance.ShowScore(hitTarget, addedScore);

        Debug.Log($"[MeteorProjectile] 隕石命中勇者，增加分數={addedScore}");

        Destroy(gameObject);

        return addedScore;
    }
}