using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class MenuPredator : MonoBehaviour
{
    [Header("Move")]
    public float accel = 10f;
    public float maxSpeed = 5f;

    [Header("Flock Targeting")]
    public float searchRadius = 2.5f;
    public int   sampleLimit  = 180;

    [Header("Bounce")]
    public float bounceDamping = 0.5f;   // 反弹衰减
    float radius = 0.2f;                  // 碰撞半径（由碰撞体估算）

    Rigidbody2D rb;

    public void Init(MenuBackground _) { }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // 与墙碰撞可选；即使不碰撞，也会用下面的矩形反弹兜底
        var col = GetComponent<Collider2D>();
        if (col) col.isTrigger = false;

        // 估算半径
        if (TryGetComponent(out CircleCollider2D cc))
            radius = cc.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
        else if (TryGetComponent(out CapsuleCollider2D cap))
            radius = Mathf.Max(cap.size.x, cap.size.y) * 0.5f;
        else if (TryGetComponent(out BoxCollider2D bc))
            radius = Mathf.Max(bc.size.x, bc.size.y) * 0.5f;
    }

    void FixedUpdate()
    {
        var bm = BoidManager.Instance;
        if (bm == null || bm.ActiveBoids == null || bm.ActiveBoids.Count == 0) { BoundBounce(); return; }

        // —— 追“最大鱼群”的质心 ——
        Vector2 myPos = rb.position;
        float r2 = searchRadius * searchRadius;
        var list = bm.ActiveBoids;
        int n = list.Count, step = Mathf.Max(1, n / sampleLimit);

        Boid bestSeed = null; int bestCount = 0; Vector2 bestCentroid = Vector2.zero;
        for (int s = 0; s < n; s += step)
        {
            var seed = list[s]; if (!seed) continue;
            Vector2 sp = seed.transform.position;
            int count = 0; Vector2 sum = Vector2.zero;

            for (int j = 0; j < n; j += step)
            {
                var b = list[j]; if (!b) continue;
                Vector2 bp = b.transform.position;
                if ((bp - sp).sqrMagnitude <= r2) { count++; sum += bp; }
            }
            if (count > bestCount)
            {
                bestCount = count; bestSeed = seed;
                bestCentroid = (count > 0) ? sum / count : sp;
            }
        }

        if (bestSeed)
        {
            Vector2 dir = (bestCentroid - myPos).normalized;
            Vector2 v = rb.linearVelocity + dir * accel * Time.fixedDeltaTime;
            if (v.sqrMagnitude > maxSpeed * maxSpeed) v = v.normalized * maxSpeed;
            rb.linearVelocity = v;
            if (v.sqrMagnitude > 1e-4f) transform.up = v.normalized;
        }

        // —— 16×9 边界反弹（以 BoidManager.spawnArea 为准）——
        BoundBounce();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent(out Boid b))
        {
            var bm = BoidManager.Instance;
            if (bm) bm.DespawnBoid(b); else Destroy(b.gameObject);
        }
    }

    void OnCollisionEnter2D(Collision2D c)
    {
        if (c.contactCount == 0) return;
        Vector2 n = c.GetContact(0).normal;
        rb.linearVelocity = Vector2.Reflect(rb.linearVelocity, n) * bounceDamping;
        if (rb.linearVelocity.sqrMagnitude > 1e-4f) transform.up = rb.linearVelocity.normalized;
    }

    // 基于 BoidManager.spawnArea 的矩形边界反弹
    void BoundBounce()
    {
        var bm = BoidManager.Instance; if (!bm) return;
        float hx = bm.spawnArea.x * 1.03f;
        float hy = bm.spawnArea.y * 1.03f;

        Vector2 p = rb.position;
        Vector2 v = rb.linearVelocity;
        bool bounced = false;

        if (p.x < -hx + radius && v.x < 0f) { p.x = -hx + radius; v.x = -v.x * bounceDamping; bounced = true; }
        else if (p.x >  hx - radius && v.x > 0f) { p.x =  hx - radius; v.x = -v.x * bounceDamping; bounced = true; }

        if (p.y < -hy + radius && v.y < 0f) { p.y = -hy + radius; v.y = -v.y * bounceDamping; bounced = true; }
        else if (p.y >  hy - radius && v.y > 0f) { p.y =  hy - radius; v.y = -v.y * bounceDamping; bounced = true; }

        if (bounced)
        {
            rb.position = p;
            rb.linearVelocity = v;
            if (v.sqrMagnitude > 1e-4f) transform.up = v.normalized;
        }
    }
}
