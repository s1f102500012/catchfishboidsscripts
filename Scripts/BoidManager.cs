// BoidManager.cs — 08041955 + 修正无 Initialise()
using System.Collections;
using System.Collections.Generic;
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


    void Awake()
    {
        if (spawnPoints.Count == 0)
            foreach (var g in GameObject.FindGameObjectsWithTag("SpawnPoint"))
                spawnPoints.Add(g.transform);
        Instance = this;
    }

    void OnEnable() => GameManager.OnMatchBegin += HandleMatchBegin;
    void OnDisable() => GameManager.OnMatchBegin -= HandleMatchBegin;

    void FixedUpdate()
    {
        var snapshot = ActiveBoids.ToArray();        // ← 先拍快照
        for (int i = 0; i < snapshot.Length; i++)
        {
            var b = snapshot[i];
            if (!b) continue;
            b.Simulate();
        }
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
        Vector2 flee = player ? ((Vector2)sp.position - (Vector2)player.position).normalized
                              : Vector2.up;

        for (int i = 0; i < boidsPerWave; i++)
        {
            Vector2 pos = (Vector2)sp.position + Random.insideUnitCircle * scatterRadius;
            Boid b = Instantiate(boidPrefab, pos, Quaternion.identity, transform);

            SampleRandomScales(false, out float speedScale, out float forceScale, out float sizeScale);

            b.SetRandomScales(speedScale, forceScale, sizeScale);
            b.SetGlobalScales(globalSpeedMult, globalForceMult);


            // ① 若你已算好 goldenChanceAddFromSpeed（0~1）：
            float p = Mathf.Clamp01(goldenChance + goldenChanceAddFromSpeed);
            if (Random.value < p)
                b.ConfigureAsGolden(goldenSpeedMultiplier, goldenForceMultiplier, goldenScoreValue, goldenColor);


#if UNITY_2023_1_OR_NEWER
            b.GetComponent<Rigidbody2D>().linearVelocity = flee * b.maxSpeed * 0.5f;
#else
            b.GetComponent<Rigidbody2D>().velocity       = flee * b.maxSpeed * 0.5f;
#endif
            ActiveBoids.Add(b);
        }

        // 额外金鱼（来自道具）
        for (int g = 0; g < extraGoldenPerWave; g++)
        {
            // 与外层变量避名冲突：用 goldFleeDir
            Vector2 goldFleeDir = player
                ? ((Vector2)sp.position - (Vector2)player.position).normalized
                : Vector2.up;

            Vector2 pos = (Vector2)sp.position + Random.insideUnitCircle * scatterRadius;

            Boid b = Instantiate(boidPrefab, pos, Quaternion.identity, transform);
            SampleRandomScales(true, out float speedScale, out float forceScale, out float sizeScale);

            b.SetRandomScales(speedScale, forceScale, sizeScale);
            b.SetGlobalScales(globalSpeedMult, globalForceMult);
            b.ConfigureAsGolden(goldenSpeedMultiplier, goldenForceMultiplier, goldenScoreValue, goldenColor);

#if UNITY_2023_1_OR_NEWER
            b.GetComponent<Rigidbody2D>().linearVelocity = goldFleeDir * b.maxSpeed * 0.5f;
#else
            b.GetComponent<Rigidbody2D>().velocity       = goldFleeDir * b.maxSpeed * 0.5f;
#endif

            ActiveBoids.Add(b);
        }
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
