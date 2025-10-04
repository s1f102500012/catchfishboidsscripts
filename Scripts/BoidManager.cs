// BoidManager.cs — 08041955 + 修正无 Initialise()
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class BoidManager : MonoBehaviour
{
    [Header("Prefabs & Bounds")]
    public Boid boidPrefab;
    public Vector2 spawnArea = new(16f, 9f);

    [Header("Golden Settings")]
    [Range(0f, 1f)] public float goldenChance = 0.05f;
    public float goldenSpeedMultiplier = 1.5f;
    public float goldenForceMultiplier = 1.5f;
    public int goldenScoreValue = 5;
    public Color goldenColor = Color.yellow;

    [Header("Spawn Randomization")]
    [Tooltip("Random speed multiplier range for newly spawned fish (x-axis is used as minimum, y-axis as maximum).")]
    public Vector2 speedRandomRange = new(0.99f, 1.02f);
    [Tooltip("Random steering force multiplier range for newly spawned fish.")]
    public Vector2 forceRandomRange = new(0.99f, 1.02f);
    [Tooltip("Random local scale multiplier range for newly spawned fish.")]
    public Vector2 sizeRandomRange = new(0.99f, 1.02f);

    [Header("Extra Golden Randomization")]
    [Tooltip("Random speed multiplier range when spawning extra golden fish from modifiers.")]
    public Vector2 goldenSpeedRandomRange = new(0.98f, 1.03f);
    [Tooltip("Random steering force multiplier range when spawning extra golden fish from modifiers.")]
    public Vector2 goldenForceRandomRange = new(0.98f, 1.03f);
    [Tooltip("Random local scale multiplier range when spawning extra golden fish from modifiers.")]
    public Vector2 goldenSizeRandomRange = new(0.95f, 1.10f);

    [Header("Links")]
    [SerializeField] Transform player;
    public Transform Player { get => player; set => player = value; }

    /* ---------- 运行时 ---------- */
    public readonly List<Boid> ActiveBoids = new();

    [Header("Spawn Points")]
    public List<Transform> spawnPoints = new();

    [Header("Wave Settings")]
    public int boidsPerWave = 10;
    public float scatterRadius = 1f;
    public float safeDistance = 6f;
    public float firstInterval = 3f;
    public float minInterval = 0.5f;
    [Header("Runtime Modifiers")]
    public int extraGoldenPerWave = 0;   // ← 新增

    public static BoidManager Instance { get; private set; }

    [Header("Fusion")]
    public bool fusionEnabled   = true;
    [Tooltip("普通鱼→一级鱼的融合启用阈值")]
    public int  fusionThreshold = 200;
    [Tooltip("一级鱼→二级鱼的融合启用阈值")]
    public int  tier2FusionThreshold = 210;
    [Tooltip("二级鱼→三级鱼的融合启用阈值")]
    public int  tier3FusionThreshold = 220;
    [Tooltip("三级鱼→四级鱼的融合启用阈值")]
    public int  tier4FusionThreshold = 230;
    public int  maxFusionTier   = 4;   // ← 从 2 改为 4

    HashSet<int> merging = new();           // 防止同对重复融合

    [Header("Global Fish Multipliers")]
    public float globalSpeedMult = 1f;
    public float globalForceMult = 1f;
    [Header("Golden Chance (runtime adds)")]
    [Range(0f,1f)] public float goldenChanceAddFromSpeed = 0f;   // 由道具①动态写入

    [Header("Golden Wave Bonus")]
    [Range(0f,1f)] public float fullGoldenWaveChance = 0f;


    void Awake()
    {
        if (spawnPoints.Count == 0)
            foreach (var g in GameObject.FindGameObjectsWithTag("SpawnPoint"))
                spawnPoints.Add(g.transform);
        Instance = this;
    }

    void OnEnable() => GameManager.OnMatchBegin += HandleMatchBegin;
    void OnDisable() => GameManager.OnMatchBegin -= HandleMatchBegin;

    [Header("Async Simulation")]
    [Tooltip("Number of boids processed per asynchronous batch.")]
    [Min(1)] public int asyncBatchSize = 32;

    bool isProcessingBoids = false;

    async void FixedUpdate()
    {
        if (isProcessingBoids) return;
        if (ActiveBoids.Count == 0) return;

        var snapshot = ActiveBoids.ToArray();
        bool hasValid = false;
        for (int i = 0; i < snapshot.Length; i++)
        {
            if (snapshot[i]) { hasValid = true; break; }
        }
        if (!hasValid) return;

        isProcessingBoids = true;
        try
        {
            await SimulateBoidsAsync(snapshot);
        }
        finally
        {
            isProcessingBoids = false;
        }
    }

    async Task SimulateBoidsAsync(Boid[] boidSnapshot)
    {
        int length = boidSnapshot.Length;
        var snapshots = new BoidSimulationSnapshot[length];
        var spikeAvoidance = new Vector2[length];
        var results = new Vector2[length];

        bool anyValid = false;
        for (int i = 0; i < length; i++)
        {
            var boid = boidSnapshot[i];
            if (!boid) continue;
            anyValid = true;

            var snap = boid.CaptureSnapshot(i);
            snapshots[i] = snap;
            spikeAvoidance[i] = boid.SampleSpikeAvoidanceForce();
            results[i] = snap.Velocity;
        }

        if (!anyValid)
            return;

        float dt = Time.fixedDeltaTime;
        Vector2 spawnHalf = spawnArea;
        Vector2? playerPos = player ? new Vector2(player.position.x, player.position.y) : null;

        int batchSize = Mathf.Max(1, asyncBatchSize);
        int batchCount = Mathf.CeilToInt((float)length / batchSize);

        Task processingTask;

#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL 平台不支持多线程 Task.Run，直接在主线程同步处理所有批次。
        for (int b = 0; b < batchCount; b++)
        {
            int start = b * batchSize;
            int end = Mathf.Min(start + batchSize, length);
            if (start >= end)
                break;

            ProcessBoidBatch(start, end, snapshots, spikeAvoidance, spawnHalf, playerPos, dt, results);
        }
        processingTask = Task.CompletedTask;
#else
        if (batchCount <= 1)
        {
            ProcessBoidBatch(0, length, snapshots, spikeAvoidance, spawnHalf, playerPos, dt, results);
            processingTask = Task.CompletedTask;
        }
        else
        {
            var tasks = new List<Task>(batchCount);
            for (int b = 0; b < batchCount; b++)
            {
                int start = b * batchSize;
                int end = Mathf.Min(start + batchSize, length);
                if (start >= end)
                    break;

                tasks.Add(Task.Run(() =>
                    ProcessBoidBatch(start, end, snapshots, spikeAvoidance, spawnHalf, playerPos, dt, results)));
            }

            processingTask = Task.WhenAll(tasks);
        }
#endif

        await processingTask;

        for (int i = 0; i < length; i++)
        {
            var boid = boidSnapshot[i];
            if (!boid) continue;
            boid.ApplySimulationResult(results[i]);
        }
    }

    static void ProcessBoidBatch(int start, int end, BoidSimulationSnapshot[] snapshots, Vector2[] spikeAvoidance, Vector2 spawnHalf, Vector2? playerPos, float dt, Vector2[] results)
    {
        for (int i = start; i < end; i++)
        {
            ref readonly var boid = ref snapshots[i];
            if (!boid.IsValid)
                continue;

            Vector2 accel = Vector2.zero;

            accel += boid.WeightSeparation * ComputeSeparation(in boid, snapshots);
            accel += boid.WeightCohesion * ComputeCohesion(in boid, snapshots);
            accel += boid.WeightAlignment * ComputeAlignment(in boid, snapshots);
            accel += boid.WeightFlee * ComputeFlee(in boid, playerPos);
            accel += boid.SpikeRepelWeight * spikeAvoidance[i];
            accel += boid.WallRepelWeight * ComputeWallAvoidance(in boid, spawnHalf);

            Vector2 velocity = boid.Velocity + accel * dt;
            results[i] = Vector2.ClampMagnitude(velocity, boid.MaxSpeed);
        }
    }

    static Vector2 ComputeSeparation(in BoidSimulationSnapshot boid, BoidSimulationSnapshot[] snapshots)
    {
        if (boid.PerceptionRadius <= 0f || boid.SeparationRadius <= 0f)
            return Vector2.zero;

        Vector2 sum = Vector2.zero;
        int count = 0;

        for (int j = 0; j < snapshots.Length; j++)
        {
            if (j == boid.Index) continue;
            ref readonly var other = ref snapshots[j];
            if (!other.IsValid) continue;

            Vector2 diff = boid.Position - other.Position;
            float sqrMag = diff.sqrMagnitude;
            if (sqrMag >= boid.PerceptionRadiusSqr || sqrMag <= 1e-6f) continue;
            if (sqrMag >= boid.SeparationRadiusSqr) continue;

            float invMag = 1f / Mathf.Sqrt(sqrMag);
            sum += diff * invMag;
            count++;
        }

        if (count == 0)
            return Vector2.zero;

        return SteerTowards(sum / count, in boid);
    }

    static Vector2 ComputeCohesion(in BoidSimulationSnapshot boid, BoidSimulationSnapshot[] snapshots)
    {
        if (boid.PerceptionRadius <= 0f)
            return Vector2.zero;

        Vector2 center = Vector2.zero;
        int count = 0;

        for (int j = 0; j < snapshots.Length; j++)
        {
            if (j == boid.Index) continue;
            ref readonly var other = ref snapshots[j];
            if (!other.IsValid) continue;

            Vector2 diff = other.Position - boid.Position;
            float sqrMag = diff.sqrMagnitude;
            if (sqrMag >= boid.PerceptionRadiusSqr) continue;

            center += other.Position;
            count++;
        }

        if (count == 0)
            return Vector2.zero;

        float invCount = 1f / count;
        return SteerTowards(center * invCount - boid.Position, in boid);
    }

    static Vector2 ComputeAlignment(in BoidSimulationSnapshot boid, BoidSimulationSnapshot[] snapshots)
    {
        if (boid.PerceptionRadius <= 0f)
            return Vector2.zero;

        Vector2 sum = Vector2.zero;
        int count = 0;

        for (int j = 0; j < snapshots.Length; j++)
        {
            if (j == boid.Index) continue;
            ref readonly var other = ref snapshots[j];
            if (!other.IsValid) continue;

            Vector2 diff = other.Position - boid.Position;
            if (diff.sqrMagnitude >= boid.PerceptionRadiusSqr) continue;

            sum += other.Velocity;
            count++;
        }

        if (count == 0)
            return Vector2.zero;

        float invCount = 1f / count;
        return SteerTowards(sum * invCount, in boid);
    }

    static Vector2 ComputeFlee(in BoidSimulationSnapshot boid, Vector2? playerPos)
    {
        if (!playerPos.HasValue || boid.DangerRadius <= 0f)
            return Vector2.zero;

        Vector2 d = boid.Position - playerPos.Value;
        if (d.sqrMagnitude >= boid.DangerRadiusSqr)
            return Vector2.zero;

        return SteerTowards(d, in boid);
    }

    static Vector2 ComputeWallAvoidance(in BoidSimulationSnapshot boid, Vector2 spawnHalf)
    {
        if (boid.WallRepelRadius <= 0f)
            return Vector2.zero;

        Vector2 steer = Vector2.zero;
        Vector2 pos = boid.Position;
        float r = boid.WallRepelRadius;

        float dL = pos.x + spawnHalf.x;
        if (dL < r)
        {
            float ratio = 1f - dL / r;
            steer += Vector2.right * (ratio * ratio);
        }

        float dR = spawnHalf.x - pos.x;
        if (dR < r)
        {
            float ratio = 1f - dR / r;
            steer += Vector2.left * (ratio * ratio);
        }

        float dB = pos.y + spawnHalf.y;
        if (dB < r)
        {
            float ratio = 1f - dB / r;
            steer += Vector2.up * (ratio * ratio);
        }

        float dT = spawnHalf.y - pos.y;
        if (dT < r)
        {
            float ratio = 1f - dT / r;
            steer += Vector2.down * (ratio * ratio);
        }

        return SteerTowards(steer, in boid);
    }

    static Vector2 SteerTowards(Vector2 vec, in BoidSimulationSnapshot boid)
    {
        if (vec == Vector2.zero)
            return Vector2.zero;

        Vector2 desired = vec.normalized * boid.MaxSpeed;
        return Vector2.ClampMagnitude(desired - boid.Velocity, boid.MaxForce);
    }


    /* ---------- 回合开始 ---------- */
    void HandleMatchBegin()
    {
        StopAllCoroutines();
        StartCoroutine(WaveSpawner());
    }

    /* ---------- 清场 ---------- */
    public void ClearAllBoids()
    {
        StopAllCoroutines();
        foreach (var b in new List<Boid>(ActiveBoids))
            Destroy(b.gameObject);
        ActiveBoids.Clear();
    }

    /* ---------- 波次协程 ---------- */
    IEnumerator WaveSpawner()
    {
        while (GameManager.Instance == null || !GameManager.Instance.IsRunning)
            yield return null;

        var gm = GameManager.Instance;
        float stopAt = gm.stopSpawnBeforeEnd;
        float elapsed = 0f;

        while (gm.IsRunning && gm.TimeLeft > stopAt)
        {
            float t = elapsed / Mathf.Max(gm.matchTime - stopAt, 0.0001f);
            float interval = Mathf.Lerp(firstInterval, minInterval, t);

            Transform sp = ChooseSpawn();
            if (sp) SpawnWaveAt(sp);

            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }
    }

    Transform ChooseSpawn()
    {
        if (spawnPoints.Count == 0) return null;
        if (!player) return spawnPoints[Random.Range(0, spawnPoints.Count)];

        List<Transform> safe = new();
        foreach (var p in spawnPoints)
            if (Vector2.Distance(p.position, player.position) >= safeDistance)
                safe.Add(p);

        if (safe.Count > 0) return safe[Random.Range(0, safe.Count)];

        Transform far = spawnPoints[0];
        float maxD = 0f;
        foreach (var p in spawnPoints)
        {
            float d = Vector2.Distance(p.position, player.position);
            if (d > maxD) { maxD = d; far = p; }
        }
        return far;
    }

    void SpawnWaveAt(Transform sp)
    {
        if (!sp) return;
        StartCoroutine(SpawnWaveRoutine(sp));
    }

    IEnumerator SpawnWaveRoutine(Transform sp)
    {
        Vector2 flee = player
            ? ((Vector2)sp.position - (Vector2)player.position).normalized
            : Vector2.up;

        bool spawnFullGolden = false;
        if (fullGoldenWaveChance > 0f)
            spawnFullGolden = Random.value < Mathf.Clamp01(fullGoldenWaveChance);

        float combinedGoldenChance = Mathf.Clamp01(goldenChance + goldenChanceAddFromSpeed + SpikeHitGoldenChanceBuff.CurrentBonus);

        int regularCount = Mathf.Max(0, boidsPerWave);
        int extraGoldenCount = Mathf.Max(0, extraGoldenPerWave);
        int totalCount = regularCount + extraGoldenCount;
        if (totalCount <= 0)
            yield break;

        float duration = totalCount > 1 ? 0.5f * Mathf.Log10(totalCount) : 0f;
        float interval = (duration > 0f && totalCount > 0) ? duration / totalCount : 0f;
        WaitForSeconds wait = interval > 0f ? new WaitForSeconds(interval) : null;

        for (int i = 0; i < regularCount; i++)
        {
            if (wait != null)
                yield return wait;

            SpawnSingleBoid(sp, flee, false, spawnFullGolden, combinedGoldenChance);
        }

        for (int g = 0; g < extraGoldenCount; g++)
        {
            if (wait != null)
                yield return wait;

            SpawnSingleBoid(sp, flee, true, false, 0f);
        }
    }

    void SpawnSingleBoid(Transform sp, Vector2 fleeDirection, bool forceGolden, bool spawnFullGolden, float combinedGoldenChance)
    {
        Vector2 pos = (Vector2)sp.position + Random.insideUnitCircle * scatterRadius;
        Boid b = Instantiate(boidPrefab, pos, Quaternion.identity, transform);

        bool useGoldenRandom = forceGolden;
        SampleRandomScales(useGoldenRandom, out float speedScale, out float forceScale, out float sizeScale);

        b.SetRandomScales(speedScale, forceScale, sizeScale);
        b.SetGlobalScales(globalSpeedMult, globalForceMult);

        bool makeGolden = forceGolden || spawnFullGolden;
        if (!makeGolden && combinedGoldenChance > 0f && Random.value < combinedGoldenChance)
            makeGolden = true;

        if (makeGolden)
            b.ConfigureAsGolden(goldenSpeedMultiplier, goldenForceMultiplier, goldenScoreValue, goldenColor);
        else
            b.isGolden = false;

#if UNITY_2023_1_OR_NEWER
        b.GetComponent<Rigidbody2D>().linearVelocity = fleeDirection * b.maxSpeed * 0.5f;
#else
        b.GetComponent<Rigidbody2D>().velocity       = fleeDirection * b.maxSpeed * 0.5f;
#endif

        ActiveBoids.Add(b);
    }

    public void DespawnBoid(Boid b)
    {
        ActiveBoids.Remove(b);
        Destroy(b.gameObject);
    }
    
    // 请求融合：同色同阶，数量超过阈值才允许
    public void RequestFusion(Boid a, Boid b)
    {
        if (!fusionEnabled || ActiveBoids == null) return;
        if (!a || !b || a == b) return;
        int allowedFusionTier = GetHighestAllowedFusionTier(ActiveBoids.Count);
        if (allowedFusionTier < 0 || a.fusionTier > allowedFusionTier) return;
        if (a.isGolden != b.isGolden) return;
        if (a.fusionTier != b.fusionTier) return;
        if (a.fusionTier >= maxFusionTier) return;

        // 只让较小实例ID的一方执行，避免两边同时触发
        if (a.GetInstanceID() > b.GetInstanceID()) { var t = a; a = b; b = t; }

        if (merging.Contains(a.GetInstanceID()) || merging.Contains(b.GetInstanceID())) return;
        StartCoroutine(FuseNextFrame(a, b));
    }

    int GetHighestAllowedFusionTier(int currentCount)
    {
        int allowed = -1;

        if (currentCount > fusionThreshold)
            allowed = 0;
        if (currentCount > tier2FusionThreshold)
            allowed = Mathf.Max(allowed, 1);
        if (currentCount > tier3FusionThreshold)
            allowed = Mathf.Max(allowed, 2);
        if (currentCount > tier4FusionThreshold)
            allowed = Mathf.Max(allowed, 3);

        return allowed;
    }

    System.Collections.IEnumerator FuseNextFrame(Boid a, Boid b)
    {
        merging.Add(a.GetInstanceID());
        merging.Add(b.GetInstanceID());
        yield return null; // 避开同帧物理回调
        DoFusion(a, b);
        merging.Remove(a.GetInstanceID());
        merging.Remove(b.GetInstanceID());
    }

    void DoFusion(Boid a, Boid b)
    {
        if (!a || !b) return;
        if (!ActiveBoids.Contains(a) || !ActiveBoids.Contains(b)) return;

    #if UNITY_2023_1_OR_NEWER
        var rbA = a.GetComponent<Rigidbody2D>(); var rbB = b.GetComponent<Rigidbody2D>();
        Vector2 pos = (rbA.position + rbB.position) * 0.5f;
        Vector2 vel = (rbA.linearVelocity + rbB.linearVelocity) * 0.5f;
    #else
        var rbA = a.GetComponent<Rigidbody2D>(); var rbB = b.GetComponent<Rigidbody2D>();
        Vector2 pos = (rbA.position + rbB.position) * 0.5f;
        Vector2 vel = (rbA.velocity + rbB.velocity) * 0.5f;
    #endif

        // 生成新鱼（同色，阶数+1），并重新随机基础属性
        Boid nb = Instantiate(boidPrefab, pos, Quaternion.identity, transform);

        float combinedSpeedScale = CombineRandomScales(a.RandomSpeedScale, b.RandomSpeedScale);
        float combinedForceScale = CombineRandomScales(a.RandomForceScale, b.RandomForceScale);
        float combinedSizeScale  = CombineRandomScales(a.RandomSizeScale,  b.RandomSizeScale);

        nb.SetRandomScales(combinedSpeedScale, combinedForceScale, combinedSizeScale);
        nb.SetGlobalScales(globalSpeedMult, globalForceMult);

        if (a.isGolden)
            nb.ConfigureAsGolden(goldenSpeedMultiplier, goldenForceMultiplier, goldenScoreValue, goldenColor);
        else
            nb.isGolden = false;

        nb.perceptionRadius = a.perceptionRadius;
        nb.separationRadius = a.separationRadius;
        nb.SetTier(a.fusionTier + 1);

    #if UNITY_2023_1_OR_NEWER
        nb.GetComponent<Rigidbody2D>().linearVelocity = vel;
    #else
        nb.GetComponent<Rigidbody2D>().velocity       = vel;
    #endif

        ActiveBoids.Add(nb);
        DespawnBoid(a);
        DespawnBoid(b);
    }

    private static float CombineRandomScales(float first, float second)
    {
        return Mathf.Max(0f, first + second - 1f);
    }

    public void SampleRandomScales(bool forGolden, out float speed, out float force, out float size)
    {
        Vector2 speedRange = forGolden ? goldenSpeedRandomRange : speedRandomRange;
        Vector2 forceRange = forGolden ? goldenForceRandomRange : forceRandomRange;
        Vector2 sizeRange  = forGolden ? goldenSizeRandomRange  : sizeRandomRange;

        speed = RandomInRange(speedRange);
        force = RandomInRange(forceRange);
        size  = RandomInRange(sizeRange);
    }

    private static float RandomInRange(Vector2 range)
    {
        float min = Mathf.Min(range.x, range.y);
        float max = Mathf.Max(range.x, range.y);

        min = Mathf.Max(0f, min);
        max = Mathf.Max(0f, max);

        if (Mathf.Approximately(min, max))
            return min;

        return Random.Range(min, max);
    }


}