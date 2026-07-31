using System.Collections;
using UnityEngine;

public class MeteorAttack : MonoBehaviour
{
    [Header("隕石 Prefab")]
    [SerializeField] private GameObject meteorPrefab;

    [Header("生成設定")]
    [SerializeField] private Transform spawnCenter;
    [SerializeField] private Vector2 spawnRange = new Vector2(8f, 2f);
    [SerializeField] private float spawnIntervalBeats = 0.3f;
    [SerializeField] private float durationBeats = 4f;
    [SerializeField] private int meteorsPerSpawn = 1;
    [SerializeField] private bool spawnImmediately = true;

    [Header("隕石移動設定")]
    [SerializeField] private float meteorMoveAngle = -45f;
    [SerializeField] private float meteorMoveSpeed = 8f;
    [SerializeField] private float meteorLifetime = 5f;

    [Header("技能結束設定")]
    [SerializeField] private bool destroyAfterFinished = true;

    private Coroutine meteorRoutine;
    private bool isActive = false;

    private void OnEnable()
    {
        StartMeteorAttack();
    }

    private void OnDisable()
    {
        StopMeteorAttack();
    }

    private void StartMeteorAttack()
    {
        if (meteorRoutine != null) StopCoroutine(meteorRoutine);

        isActive = true;
        meteorRoutine = StartCoroutine(MeteorSpawnRoutine());
    }

    private void StopMeteorAttack()
    {
        isActive = false;

        if (meteorRoutine != null)
        {
            StopCoroutine(meteorRoutine);
            meteorRoutine = null;
        }
    }

    private IEnumerator MeteorSpawnRoutine()
    {
        if (FMODBeatListener2.Instance == null)
        {
            Debug.LogWarning("[MeteorAttack] FMODBeatListener2 尚未初始化，無法取得每拍秒數。");
            FinishSkill();
            yield break;
        }

        float secondsPerBeat = FMODBeatListener2.Instance.SecondsPerBeat;
        float intervalSeconds = Mathf.Max(0.01f, spawnIntervalBeats * secondsPerBeat);
        float durationSeconds = Mathf.Max(0.01f, durationBeats * secondsPerBeat);
        float elapsedSeconds = 0f;

        if (spawnImmediately)
        {
            SpawnMeteors();
        }

        while (isActive)
        {
            yield return new WaitForSeconds(intervalSeconds);

            elapsedSeconds += intervalSeconds;

            if (elapsedSeconds >= durationSeconds) break;

            SpawnMeteors();
        }

        FinishSkill();
    }

    private void SpawnMeteors()
    {
        if (meteorPrefab == null)
        {
            Debug.LogWarning("[MeteorAttack] 尚未指定 Meteor Prefab。");
            return;
        }

        Vector3 centerPosition = spawnCenter != null ? spawnCenter.position : transform.position;

        for (int i = 0; i < Mathf.Max(1, meteorsPerSpawn); i++)
        {
            float randomX = Random.Range(-spawnRange.x * 0.5f, spawnRange.x * 0.5f);
            float randomY = Random.Range(-spawnRange.y * 0.5f, spawnRange.y * 0.5f);
            Vector3 spawnPosition = centerPosition + new Vector3(randomX, randomY, 0f);

            GameObject meteor = Instantiate(meteorPrefab, spawnPosition, Quaternion.identity);
            MeteorProjectile projectile = meteor.GetComponent<MeteorProjectile>();

            if (projectile == null)
            {
                Debug.LogWarning("[MeteorAttack] Meteor Prefab 上找不到 MeteorProjectile，已自動新增元件。");
                projectile = meteor.AddComponent<MeteorProjectile>();
            }

            projectile.Initialize(meteorMoveAngle, meteorMoveSpeed, meteorLifetime);
        }
    }

    private void FinishSkill()
    {
        isActive = false;
        meteorRoutine = null;

        if (destroyAfterFinished) Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 centerPosition = spawnCenter != null ? spawnCenter.position : transform.position;
        Gizmos.DrawWireCube(centerPosition, new Vector3(spawnRange.x, spawnRange.y, 0f));
    }
}