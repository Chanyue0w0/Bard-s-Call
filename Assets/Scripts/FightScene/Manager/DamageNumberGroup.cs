using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageNumberGroup : MonoBehaviour
{
    [System.Serializable]
    private struct DigitEntry
    {
        public ParticleSystem ps;
        public int digit;
        public float ttl;
        public float t;
    }

    [HideInInspector] public DamageNumberManager manager;

    private readonly List<DigitEntry> entries = new List<DigitEntry>();
    private Vector3 startPos;
    private Vector3 endPos;
    private float groupT;
    private float groupDuration;
    private bool running;

    public void RegisterDigit(ParticleSystem ps, int digit, float lifetime)
    {
        entries.Add(new DigitEntry
        {
            ps = ps,
            digit = digit,
            ttl = lifetime,
            t = 0f
        });
    }

    public void Begin(float floatUp, float duration)
    {
        startPos = transform.position;
        endPos = startPos + Vector3.up * floatUp;
        groupDuration = Mathf.Max(0.01f, duration);
        running = true;
    }

    private void Update()
    {
        if (!running) return;

        // 群組上浮（線性）
        groupT += Time.deltaTime;
        float u = Mathf.Clamp01(groupT / groupDuration);
        transform.position = Vector3.Lerp(startPos, endPos, u);

        // 個別位數的壽命倒數
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var e = entries[i];
            e.t += Time.deltaTime;

            // 若該位數時間到，歸還到池並移除
            if (e.t >= e.ttl || (e.ps == null))
            {
                if (e.ps != null && manager != null)
                    manager.ReturnDigit(e.digit, e.ps);

                entries.RemoveAt(i);
            }
            else
            {
                entries[i] = e;
            }
        }

        // 當所有位數皆歸還或群組上浮時間到，就結束群組
        if (entries.Count == 0 || u >= 1f)
        {
            running = false;
            Destroy(gameObject);
        }
    }
}
