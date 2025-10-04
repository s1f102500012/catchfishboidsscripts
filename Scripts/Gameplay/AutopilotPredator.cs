using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerController), typeof(Rigidbody2D))]
public class AutopilotPredator : MonoBehaviour
{
    PlayerController pc;
    Rigidbody2D rb;
    readonly List<Transform> spikeTs = new();
    float spikeRefreshTimer;

    [Header("Predictive Targeting")]
    public float clusterRadius   = 3.2f;
    public int   sampleLimit     = 200;
    public float goldenBias      = 1.8f;
    public int   minClusterCount = 6;
    public float leadMin         = 0.18f;
    public float leadMax         = 0.9f;
    public float commitTime      = 0.7f;
    public float dirLerp         = 0.22f;

    [Header("Steering Responsiveness")]
    public float dirSnapAngle    = 55f;   // 当期望方向急转时使用更大的插值
    public float dirSnapLerp     = 0.65f; // 急转时的目标插值因子
    public float commitBreakDist = 3.2f;  // 新目标与旧锁定差距过大时提前解锁

    [Header("Avoidance (normal)")]
    public float spikeAvoidRadius = 2.8f;
    public float spikeAvoidWeight = 2.2f;
    public float wallMargin       = 0.9f;
    public float wallPushWeight   = 2.2f;

    [Header("Emergency Spike Evasion (highest priority)")]
    public float eLookahead      = 2.4f;   // 前方预测距离（单位：世界单位）
    public float eConeHalfAngle  = 28f;    // 危险锥半角（度）
    public float eDangerWidth    = 1.0f;   // 危险走廊半宽（随前向距离线性放宽）
    public float eHoldTime       = 0.30f;  // 触发后保持逃逸的时长
    public float eWallBias       = 1.5f;   // 紧急时靠墙推回权重

    [Header("Dash (Shift)")]
    public float dashTriggerDist    = 5.5f;
    public float dashProbeRange     = 5.5f;
    public float dashConeHalfAngle  = 20f;
    public float dashSafeWallMargin = 1.2f;
    public int   dashMinMoney       = 6;
    public float dashCooldown       = 0.35f;

    Vector2 desiredDirSmoothed;
    Vector2 committedAim;
    float   commitUntil, nextDashTime;

    // emergency state
    float   emergencyUntil = 0f;
    Vector2 lastEvadeDir   = Vector2.zero;

    void OnEnable()
    {
        pc = GetComponent<PlayerController>();
        rb = GetComponent<Rigidbody2D>();
        desiredDirSmoothed = Vector2.zero;
        RefreshSpikes();
    }

    void OnDisable()
    {
        if (pc) { pc.externalMoveDir = Vector2.zero; pc.externalSprint = false; }
    }

    void FixedUpdate()
    {
        var gm = GameManager.Instance;
        var bm = BoidManager.Instance;
        if (pc == null || rb == null || bm == null || gm == null || !gm.IsRunning || !pc.externalControl)
        {
            if (pc){ pc.externalMoveDir = Vector2.zero; pc.externalSprint = false; }
            return;
        }

        spikeRefreshTimer += Time.fixedDeltaTime;
        if (spikeRefreshTimer > 0.5f) { RefreshSpikes(); spikeRefreshTimer = 0f; }

        // —— 最高优先级：紧急避刺 —— 
        Vector2 emergencyDir;
        if (EmergencyEvade(bm.spawnArea, out emergencyDir))
        {
            // 立刻转向并禁用冲刺
            pc.externalMoveDir = emergencyDir;
            pc.externalSprint  = false;
            desiredDirSmoothed = emergencyDir;
            return; // 本帧不做其它逻辑
        }

        // —— 预测“最大群”的未来质心 + 锁定/平滑 —— 
        Vector2 aim; int count; bool hasGold; Vector2 avgVel;
        PredictBestCluster(bm, out aim, out count, out hasGold, out avgVel);

        float now = Time.time;
        float commitDist2 = (aim - committedAim).sqrMagnitude;
        if (now < commitUntil && commitDist2 <= commitBreakDist * commitBreakDist &&
            (committedAim - rb.position).sqrMagnitude <= (aim - rb.position).sqrMagnitude * 1.25f)
        {
            aim = committedAim;
        }
        else
        {
            committedAim = aim;
            commitUntil  = now + commitTime;
        }

        Vector2 p = rb.position;
        Vector2 dir = (aim - p);
        if (dir.sqrMagnitude > 1e-6f) dir.Normalize();

        // 常规避刺 + 靠墙推回 + 动量对齐
        dir += ComputeSpikeRepulsion(p);
        dir += ComputeWallPush(p, bm.spawnArea);
        if (rb.linearVelocity.sqrMagnitude > 0.01f) dir += rb.linearVelocity.normalized * 0.2f;

        dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.zero;

        float lerpFactor = dirLerp;
        if (desiredDirSmoothed.sqrMagnitude > 1e-6f && dir.sqrMagnitude > 1e-6f)
        {
            float angle = Vector2.Angle(desiredDirSmoothed, dir);
            if (angle > dirSnapAngle)
            {
                float t = Mathf.InverseLerp(dirSnapAngle, 180f, angle);
                lerpFactor = Mathf.Lerp(dirLerp, dirSnapLerp, t);
            }
        }
        else
        {
            lerpFactor = dirSnapLerp;
        }
        desiredDirSmoothed = Vector2.Lerp(desiredDirSmoothed, dir, Mathf.Clamp01(lerpFactor));
        pc.externalMoveDir = desiredDirSmoothed;

        // —— 冲刺策略（安全/远距/有钱/冷却） ——
        float dist = (aim - p).magnitude;
        bool nearWall  = NearWall(p, bm.spawnArea, dashSafeWallMargin);
        bool pathClear = ConeClearOfSpikes(p, desiredDirSmoothed, dashConeHalfAngle, dashProbeRange);
        bool dense     = count >= Mathf.Max(minClusterCount, 6);

        float align = (desiredDirSmoothed.sqrMagnitude > 1e-6f && dir.sqrMagnitude > 1e-6f)
                        ? Vector2.Dot(desiredDirSmoothed, dir)
                        : 0f;
        bool aligned = align >= 0.4f;

        bool clusterRetreat = false;
        if (avgVel.sqrMagnitude > 1e-4f && (aim - p).sqrMagnitude > 1e-4f)
        {
            Vector2 toAim = (aim - p).normalized;
            clusterRetreat = Vector2.Dot(avgVel.normalized, toAim) < -0.35f;
        }

        bool wantDash = dense &&
                        (dist > dashTriggerDist ||
                         (hasGold && dist > dashTriggerDist * 0.6f) ||
                         (clusterRetreat && dist > dashTriggerDist * 0.45f)) &&
                        pathClear && !nearWall && aligned &&
                        gm.CurrentMoney >= dashMinMoney &&
                        Time.time >= nextDashTime;

        pc.externalSprint = wantDash;
        if (wantDash) nextDashTime = Time.time + dashCooldown;
    }

    // ============ Emergency Evade ============
    bool EmergencyEvade(Vector2 half, out Vector2 dirOut)
    {
        dirOut = Vector2.zero;

        // 若仍在避刺保持期，沿上次逃逸方向并加墙体推回
        if (Time.time < emergencyUntil && lastEvadeDir.sqrMagnitude > 1e-6f)
        {
            dirOut = (lastEvadeDir + ComputeWallPush(rb.position, half) * eWallBias).normalized;
            return true;
        }

        if (spikeTs.Count == 0) return false;

        Vector2 p = rb.position;
#if UNITY_2023_1_OR_NEWER
        Vector2 v = rb.linearVelocity;
#else
        Vector2 v = rb.velocity;
#endif
        Vector2 hint = desiredDirSmoothed.sqrMagnitude > 1e-6f ? desiredDirSmoothed :
                       (v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.up);
        float cosTh = Mathf.Cos(eConeHalfAngle * Mathf.Deg2Rad);

        bool imminent = false;
        Vector2 repel = Vector2.zero;

        for (int i = 0; i < spikeTs.Count; i++)
        {
            var t = spikeTs[i]; if (!t) continue;
            Vector2 to = (Vector2)t.position - p;
            float dist = to.magnitude;
            if (dist < 1e-4f) dist = 1e-4f;

            Vector2 nt = to / dist;
            float forward = Vector2.Dot(hint, nt); // 前向分量
            if (forward <= 0f) continue;          // 在身后

            // 在前方危险锥内且距离小于前视距离
            if (forward >= cosTh && dist <= eLookahead)
            {
                // 计算横向危险度（越窄越危险）
                float lateral = Mathf.Abs(hint.x * to.y - hint.y * to.x); // 垂距
                float corridor = eDangerWidth + 0.4f * Mathf.Clamp(dist, 0f, eLookahead);
                if (lateral <= corridor)
                {
                    imminent = true;

                    // 逃逸方向：远离尖刺 + 选择左右更宽的侧向
                    Vector2 away = (-nt) / Mathf.Max(0.2f, dist);     // 直线后退
                    // 侧向按左右两个方向选更远离尖刺集合的方向（用与当前前进方向正交的向量）
                    Vector2 left  = new Vector2(-hint.y, hint.x);
                    Vector2 right = -left;
                    // 依据尖刺相对朝向选择侧向，使避让更快
                    Vector2 side  = (Vector2.Dot(left, nt) > 0f) ? right : left;

                    repel += away * 1.0f + side * 0.8f; // 混合直退与侧滑
                }
            }
        }

        if (imminent)
        {
            Vector2 wall = ComputeWallPush(p, half) * eWallBias;
            Vector2 d = repel + wall;
            if (d.sqrMagnitude < 1e-6f) d = -hint;   // 极端兜底：原路后退
            lastEvadeDir = d.normalized;
            emergencyUntil = Time.time + eHoldTime;

            dirOut = lastEvadeDir;
            return true;
        }

        return false;
    }

    // ============ Helpers ============
    void PredictBestCluster(BoidManager bm, out Vector2 aim, out int count, out bool hasGold, out Vector2 avgVel)
    {
        aim = rb.position; count = 0; hasGold = false; avgVel = Vector2.zero;
        var list = bm.ActiveBoids; if (list == null || list.Count == 0) return;

        int n = list.Count, step = Mathf.Max(1, n / Mathf.Max(1, sampleLimit));
        float r2 = clusterRadius * clusterRadius;

        float bestScore = -1f; Vector2 bestPred = aim; int bestCnt = 0; bool bestGold = false; Vector2 bestV = Vector2.zero;
        Vector2 playerPos = rb.position;
        float playerSpd = pc ? pc.maxSpeed : 8f;

        for (int i = 0; i < n; i += step)
        {
            var seed = list[i]; if (!seed) continue;
            Vector2 sp = seed.transform.position;

            int c = 0; Vector2 sum = Vector2.zero; Vector2 vSum = Vector2.zero; float score = 0f; bool anyGold = false;

            for (int j = 0; j < n; j += step)
            {
                var b = list[j]; if (!b) continue;
                Vector2 bp = b.transform.position;
                if ((bp - sp).sqrMagnitude <= r2)
                {
                    c++; sum += bp;
#if UNITY_2023_1_OR_NEWER
                    var rb2 = b.GetComponent<Rigidbody2D>(); Vector2 bv = rb2 ? rb2.linearVelocity : Vector2.zero;
#else
                    var rb2 = b.GetComponent<Rigidbody2D>(); Vector2 bv = rb2 ? rb2.velocity : Vector2.zero;
#endif
                    vSum += bv;
                    score += b.isGolden ? goldenBias : 1f;
                    if (b.isGolden) anyGold = true;
                }
            }
            if (c == 0) continue;

            Vector2 centroid = sum / c;
            Vector2 vAvg = vSum / Mathf.Max(1, c);

            float dist = (centroid - playerPos).magnitude;
            float rel  = Mathf.Max(1f, playerSpd + vAvg.magnitude * 0.5f);
            float t    = Mathf.Clamp(dist / rel, leadMin, leadMax);
            Vector2 pred = centroid + vAvg * t;

            float towards = 0f;
            if (dist > 0.1f && vAvg.sqrMagnitude > 1e-6f)
            {
                Vector2 toPlayer = (playerPos - centroid).normalized;
                towards = Mathf.Max(0f, Vector2.Dot(vAvg.normalized, toPlayer)) * 0.5f;
            }
            float distW = 1f / (1f + dist * 0.25f);
            float total = score * distW * (1f + towards);

            if (c >= Mathf.Max(1, minClusterCount) && total > bestScore)
            {
                bestScore = total; bestPred = pred; bestCnt = c; bestGold = anyGold; bestV = vAvg;
            }
        }

        aim = (bestScore > 0f) ? bestPred : aim;
        count = bestCnt; hasGold = bestGold; avgVel = bestV;
    }

    Vector2 ComputeSpikeRepulsion(Vector2 p)
    {
        if (spikeTs.Count == 0) return Vector2.zero;
        float r2 = spikeAvoidRadius * spikeAvoidRadius;
        Vector2 sum = Vector2.zero;
        for (int i = 0; i < spikeTs.Count; i++)
        {
            var t = spikeTs[i]; if (!t) continue;
            Vector2 d = p - (Vector2)t.position;
            float d2 = d.sqrMagnitude;
            if (d2 <= r2 && d2 > 1e-4f) sum += d.normalized * (spikeAvoidWeight / Mathf.Max(0.25f, d2));
        }
        return sum;
    }

    Vector2 ComputeWallPush(Vector2 p, Vector2 half)
    {
        Vector2 push = Vector2.zero;
        if (p.x >  half.x - wallMargin) push += Vector2.left  * wallPushWeight;
        if (p.x < -half.x + wallMargin) push += Vector2.right * wallPushWeight;
        if (p.y >  half.y - wallMargin) push += Vector2.down  * wallPushWeight;
        if (p.y < -half.y + wallMargin) push += Vector2.up    * wallPushWeight;
        return push;
    }

    bool NearWall(Vector2 p, Vector2 half, float margin)
    {
        return (p.x >  half.x - margin) || (p.x < -half.x + margin) ||
               (p.y >  half.y - margin) || (p.y < -half.y + margin);
    }

    bool ConeClearOfSpikes(Vector2 p, Vector2 dir, float halfAngleDeg, float range)
    {
        if (spikeTs.Count == 0) return true;
        if (dir.sqrMagnitude < 1e-6f) return false;
        float cosTh = Mathf.Cos(halfAngleDeg * Mathf.Deg2Rad);
        Vector2 nd = dir.normalized;

        for (int i = 0; i < spikeTs.Count; i++)
        {
            var t = spikeTs[i]; if (!t) continue;
            Vector2 to = (Vector2)t.position - p;
            float dist = to.magnitude;
            if (dist > range || dist < 0.01f) continue;
            Vector2 nt = to / dist;
            if (Vector2.Dot(nd, nt) >= cosTh)
            {
                float lateral = Mathf.Abs(nd.x * to.y - nd.y * to.x);
                if (lateral < 1.0f) return false;
            }
        }
        return true;
    }

    void RefreshSpikes()
    {
#if UNITY_2023_1_OR_NEWER
        var spikes = Object.FindObjectsByType<Spike>(FindObjectsSortMode.None);
#else
        var spikes = Object.FindObjectsOfType<Spike>();
#endif
        spikeTs.Clear();
        for (int i = 0; i < spikes.Length; i++) if (spikes[i]) spikeTs.Add(spikes[i].transform);
    }
}
