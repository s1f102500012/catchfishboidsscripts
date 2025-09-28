// Boid.cs — Golden + 墙反弹 + 避刺（2025‑08‑04 20:00）
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Boid : MonoBehaviour
{
    /* ---------- 运动 / 感知 ---------- */
    [Header("Speeds & Forces")]
    public float maxSpeed = 10f;
    public float maxForce = 0.2f;

    [Header("Perception (u)")]
    public float perceptionRadius = 2.5f;
    public float separationRadius = 1.25f;
    public float dangerRadius = 5f;

    /* ---------- 行为权重 ---------- */
    [Header("Behaviour Weights")]
    public float weightSeparation = 1.5f;
    public float weightCohesion = 1.0f;
    public float weightAlignment = 1.0f;
    public float weightFlee = 2.0f;

    [Header("Spike Avoid")]
    public float spikeRepelRadius = 2f;   // 探测尖刺半径
    public float spikeRepelWeight = 3f;   // 避刺权重 (越大越躲)

    [Header("Wall Repulsion")]
    public float wallRepelRadius = 1.5f;
    public float wallRepelWeight = 3.0f;
    [Range(0f, 1f)] public float wallBounceDamping = 0.9f;

    [Header("Fusion")]
    [Tooltip("0=普通, 1=绿, 2=蓝")]
    public int fusionTier = 0;
    public SpriteRenderer tierRing;                     // 预制体子物体，可留空
    public Color tier1Color = new(0.2f, 1f, 0.2f, 0.9f);   // 绿色描边
    public Color tier2Color = new(0.3f, 0.6f, 1f, 0.9f);   // 蓝色描边
    public Color tier3Color = new(0.78f, 0.36f, 1f, 0.95f); // 紫
    public Color tier4Color = new(1f,    0.60f, 0.20f,0.95f); // 橙


    public int EatCount => 1 << Mathf.Clamp(fusionTier, 0, 10);


    /* ---------- Golden ---------- */
    [HideInInspector] public bool isGolden = false;
    [HideInInspector] public int scoreValue = 1;

    /* ---------- 引用 ---------- */
    Rigidbody2D rb;
    BoidManager mgr;
    readonly List<Boid> neighbors = new(32);
    LayerMask spikeMask;

    /* ========= 初始化 ========= */
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        mgr = FindFirstObjectByType<BoidManager>();
        spikeMask = LayerMask.GetMask("Spike");           // ★ Layer 需命名 Spike
        UpdateTierVisual();
    }

    /* ========= 每帧模拟 ========= */
    public void Simulate()
    {
        SampleNeighbors();

        Vector2 accel =
              weightSeparation * SteerSeparation()
            + weightCohesion * SteerCohesion()
            + weightAlignment * SteerAlignment()
            + weightFlee * SteerFleePredator()
            + spikeRepelWeight * SteerAvoidSpike()        // ★ 新增
            + wallRepelWeight * SteerAvoidWalls();

        float dt = Time.fixedDeltaTime;
        Vector2 v = rb.linearVelocity + accel * dt;
        rb.linearVelocity = Vector2.ClampMagnitude(v, maxSpeed);

        if (rb.linearVelocity.sqrMagnitude > 0.0001f)
            transform.up = rb.linearVelocity;

        BounceWalls();
    }

    /* ---------- GOLDEN ---------- */
    public void ConfigureAsGolden(float speedMul, float forceMul, int val, Color col)
    {
        isGolden = true; scoreValue = val;
        maxSpeed *= speedMul;
        maxForce *= forceMul;
        GetComponent<SpriteRenderer>().color = col;
        GetComponent<Trail2D>()?.RefreshFromSprite();
    }

    /* ---------- Steering ---------- */
    #region Steering
    Vector2 SteerSeparation()
    {
        if (neighbors.Count == 0) return Vector2.zero;
        Vector2 sum = Vector2.zero;
        float radiusSqr = separationRadius * separationRadius;
        int validCount = 0;
        foreach (var n in neighbors)
        {
            if (!n) continue;
            Vector2 diff = (Vector2)transform.position - (Vector2)n.transform.position;
            float sqrMag = diff.sqrMagnitude;
            if (sqrMag < radiusSqr && sqrMag > 1e-6f)
            {
                float invMag = 1f / Mathf.Sqrt(sqrMag);
                sum += diff * invMag;
                validCount++;
            }
        }
        return validCount > 0 ? SteerTowards(sum / validCount) : Vector2.zero;
    }
    Vector2 SteerCohesion()
    {
        if (neighbors.Count == 0) return Vector2.zero;
        Vector2 center = Vector2.zero;
        int validCount = 0;
        foreach (var n in neighbors)
        {
            if (!n) continue;
            center += (Vector2)n.transform.position;
            validCount++;
        }
        if (validCount == 0) return Vector2.zero;
        float invCount = 1f / validCount;
        return SteerTowards(center * invCount - rb.position);
    }
    Vector2 SteerAlignment()
    {
        if (neighbors.Count == 0) return Vector2.zero;
        Vector2 avg = Vector2.zero;
        int validCount = 0;
        foreach (var n in neighbors)
        {
            if (!n) continue;
            avg += n.rb.linearVelocity;
            validCount++;
        }
        if (validCount == 0) return Vector2.zero;
        float invCount = 1f / validCount;
        return SteerTowards(avg * invCount);
    }
    Vector2 SteerFleePredator()
    {
        if (!mgr.Player) return Vector2.zero;
        Vector2 d = rb.position - (Vector2)mgr.Player.position;
        float dist = d.magnitude;
        return dist < dangerRadius ? SteerTowards(d) : Vector2.zero;
    }
    /* -- 新：避刺墙 -- */
    Vector2 SteerAvoidSpike()
    {
        Vector2 pos = rb.position;
        Collider2D[] hits = Physics2D.OverlapCircleAll(pos, spikeRepelRadius, spikeMask);
        if (hits == null || hits.Length == 0)
            return Vector2.zero;

        Vector2 repel = Vector2.zero;
        float totalWeight = 0f;
        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (!hit) continue;

            Vector2 closest = hit.ClosestPoint(pos);
            Vector2 diff = pos - closest;
            float dist = diff.magnitude;
            if (dist < 1e-4f)
            {
                // Fallback to collider center to avoid zero-length vectors.
                diff = pos - (Vector2)hit.transform.position;
                dist = diff.magnitude;
                if (dist < 1e-4f) continue;
            }

            float weight = 1f / Mathf.Max(dist, 0.05f); // closer spikes push更强
            repel += diff.normalized * weight;
            totalWeight += weight;
        }

        if (totalWeight <= 0f)
            return Vector2.zero;

        Vector2 steerDir = repel / totalWeight;
        if (steerDir == Vector2.zero)
        {
            // Near完美对称时，向当前速度的侧向偏移一点，帮助滑出拐角
            Vector2 v = rb.linearVelocity;
            if (v.sqrMagnitude > 0.0001f)
                steerDir = new Vector2(-v.y, v.x).normalized; // 取一侧切向
        }

        return SteerTowards(steerDir);
    }
    Vector2 SteerAvoidWalls()
    {
        Vector2 steer = Vector2.zero, pos = rb.position, half = mgr.spawnArea;
        float r = wallRepelRadius;
        float dL = pos.x + half.x;
        if (dL < r)
        {
            float ratio = 1f - dL / r;
            steer += Vector2.right * (ratio * ratio);
        }
        float dR = half.x - pos.x;
        if (dR < r)
        {
            float ratio = 1f - dR / r;
            steer += Vector2.left * (ratio * ratio);
        }
        float dB = pos.y + half.y;
        if (dB < r)
        {
            float ratio = 1f - dB / r;
            steer += Vector2.up * (ratio * ratio);
        }
        float dT = half.y - pos.y;
        if (dT < r)
        {
            float ratio = 1f - dT / r;
            steer += Vector2.down * (ratio * ratio);
        }
        return SteerTowards(steer);
    }
    Vector2 SteerTowards(Vector2 vec)
    {
        if (vec == Vector2.zero) return Vector2.zero;
        Vector2 desired = vec.normalized * maxSpeed;
        return Vector2.ClampMagnitude(desired - rb.linearVelocity, maxForce);
    }
    #endregion

    /* ---------- 邻居 & 硬边 ---------- */
    void SampleNeighbors()
    {
        neighbors.Clear();
        float radiusSqr = perceptionRadius * perceptionRadius;
        foreach (var o in mgr.ActiveBoids)
        {
            if (!o || o == this) continue;
            Vector2 delta = (Vector2)o.transform.position - rb.position;
            if (delta.sqrMagnitude < radiusSqr)
                neighbors.Add(o);
        }
    }
    void BounceWalls()
    {
        Vector2 pos = rb.position;
    #if UNITY_2023_1_OR_NEWER
        Vector2 vel = rb.linearVelocity;
    #else
        Vector2 vel = rb.velocity;
    #endif
        Vector2 half = mgr.spawnArea;   // 你当前用的是半尺寸

        bool hit = false;
        if (pos.x >  half.x) { pos.x =  half.x; vel.x = -vel.x * wallBounceDamping; hit = true; }
        if (pos.x < -half.x) { pos.x = -half.x; vel.x = -vel.x * wallBounceDamping; hit = true; }
        if (pos.y >  half.y) { pos.y =  half.y; vel.y = -vel.y * wallBounceDamping; hit = true; }
        if (pos.y < -half.y) { pos.y = -half.y; vel.y = -vel.y * wallBounceDamping; hit = true; }

        if (hit)
        {
    #if UNITY_2023_1_OR_NEWER
            rb.linearVelocity = vel;
    #else
        rb.velocity = vel;
    #endif
            rb.position = pos;
            transform.position = pos;

            // ★ 通知两个道具：边界发生了一次弹反
            GlobalEvents.RaiseFishBoundaryBounce(this, pos);
        }
    }
    
    public void SetTier(int t)
    {
        fusionTier = Mathf.Clamp(t, 0, (BoidManager.Instance ? BoidManager.Instance.maxFusionTier : 2));
        UpdateTierVisual();
    }
    void UpdateTierVisual()
    {
        if (!tierRing) return;
        if (fusionTier <= 0){ tierRing.enabled = false; return; }

        tierRing.enabled = true;
        switch (fusionTier)
        {
            case 1: tierRing.color = tier1Color; break; // 绿
            case 2: tierRing.color = tier2Color; break; // 蓝
            case 3: tierRing.color = tier3Color; break; // 紫
            default: tierRing.color = tier4Color; break; // 橙（4级及以上都用橙）
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!BoidManager.Instance) return;
        if (!other) return;
        if (other.TryGetComponent(out Boid b2))
            BoidManager.Instance.RequestFusion(this, b2);
    }

}
