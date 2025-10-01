using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/5% Small -> Gold")]
public class SmallToGoldChance : PlayerModifier
{
    [Range(0f, 1f)] public float chance = 0.05f;

    [Header("Spawn Behind Player")]
    public float backOffset = 1.2f;
    public float sideSpread = 0.8f;
    [Range(0f,1f)] public float initialSpeedFrac = 0.6f;
    public float margin = 0.5f;

    public override void Apply(PlayerController player)
    {
        GlobalEvents.OnSmallFishEaten += _ => TrySpawn();
    }

    void TrySpawn()
    {
        if (Random.value > chance) return;

        var mgr = Object.FindFirstObjectByType<BoidManager>();
        if (!mgr || !mgr.boidPrefab) return;

        var pTr = mgr.Player ? mgr.Player : Object.FindFirstObjectByType<PlayerController>()?.transform;
        if (!pTr) return;

        Vector2 spawnPos;
        Vector2 fleeDir;
        GetBehind(mgr, pTr, out spawnPos, out fleeDir);

        Boid b = Object.Instantiate(mgr.boidPrefab, spawnPos, Quaternion.identity, mgr.transform);
        mgr.SampleRandomScales(false, out float speedScale, out float forceScale, out float sizeScale);


        b.SetRandomScales(speedScale, forceScale, sizeScale);
        b.SetGlobalScales(mgr.globalSpeedMult, mgr.globalForceMult);
        b.ConfigureAsGolden(mgr.goldenSpeedMultiplier, mgr.goldenForceMultiplier,
                            mgr.goldenScoreValue, mgr.goldenColor);
#if UNITY_2023_1_OR_NEWER
        b.GetComponent<Rigidbody2D>().linearVelocity = fleeDir * b.maxSpeed * initialSpeedFrac;
#else
        b.GetComponent<Rigidbody2D>().velocity       = fleeDir * b.maxSpeed * initialSpeedFrac;
#endif
        mgr.ActiveBoids.Add(b);
    }

    void GetBehind(BoidManager mgr, Transform pTr, out Vector2 pos, out Vector2 fleeDir)
    {
        Vector2 back = -(Vector2)pTr.up;
        Vector2 side = new Vector2(-back.y, back.x);
        pos = (Vector2)pTr.position + back * backOffset +
              side * Random.Range(-sideSpread, sideSpread);

        Vector2 half = mgr.spawnArea;
        pos = new Vector2(
            Mathf.Clamp(pos.x, -half.x + margin, half.x - margin),
            Mathf.Clamp(pos.y, -half.y + margin, half.y - margin)
        );

        fleeDir = (pos - (Vector2)pTr.position).normalized;
        if (fleeDir.sqrMagnitude < 1e-4f) fleeDir = back;
    }
}
