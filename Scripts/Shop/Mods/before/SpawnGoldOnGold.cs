using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/20% Spawn Gold on Gold")]
public class SpawnGoldOnGold : PlayerModifier
{
    [Range(0f,1f)] public float baseChance = 0.20f;
    public float backOffset = 0.8f;
    public float initSpeedFactor = 0.6f;

    static int   sStacks = 0;
    static bool  sHooked = false;
    static float sBaseChance = 0.20f;
    static float sBackOffset = 0.8f;
    static float sInitSpeed  = 0.6f;

    public override void Apply(PlayerController player)
    {
        sStacks++;
        sBaseChance = baseChance;
        sBackOffset = backOffset;
        sInitSpeed  = initSpeedFactor;

        if (!sHooked) { GlobalEvents.OnGoldFishEaten += OnGold; sHooked = true; }
    }

    static void OnGold(Vector2 _)
    {
        var bm = Object.FindFirstObjectByType<BoidManager>();
        var pl = Object.FindFirstObjectByType<PlayerController>();
        if (!bm || !pl) return;

        float eff = 1f - Mathf.Pow(1f - sBaseChance, sStacks);
        if (Random.value >= eff) return;

        SpawnBehind(pl, bm, true);
    }

    static void SpawnBehind(PlayerController pl, BoidManager bm, bool golden)
    {
        Vector2 fwd = pl.transform.up.sqrMagnitude > 1e-4f ? (Vector2)pl.transform.up : Vector2.up;
        Vector2 pos = (Vector2)pl.transform.position - fwd * sBackOffset + Random.insideUnitCircle * 0.05f;

        Boid b = Object.Instantiate(bm.boidPrefab, pos, Quaternion.identity, bm.transform);
        float speedScale = Random.Range(0.99f, 1.02f);
        float forceScale = Random.Range(0.99f, 1.02f);
        float sizeScale  = Random.Range(0.99f, 1.02f);

        b.SetRandomScales(speedScale, forceScale, sizeScale);
        b.SetGlobalScales(bm.globalSpeedMult, bm.globalForceMult);
        if (golden)
            b.ConfigureAsGolden(bm.goldenSpeedMultiplier, bm.goldenForceMultiplier, bm.goldenScoreValue, bm.goldenColor);

#if UNITY_2023_1_OR_NEWER
        var prv = pl.GetComponent<Rigidbody2D>().linearVelocity;
        b.GetComponent<Rigidbody2D>().linearVelocity = (-fwd * b.maxSpeed * sInitSpeed) + prv * 0.25f;
#else
        var prv = pl.GetComponent<Rigidbody2D>().velocity;
        b.GetComponent<Rigidbody2D>().velocity = (-fwd * b.maxSpeed * sInitSpeed) + prv * 0.25f;
#endif
        bm.ActiveBoids.Add(b);
    }

    public static void HardReset()
    {
        sStacks = 0; sBaseChance = 0.20f;
        sBackOffset = 0.8f; sInitSpeed = 0.6f;
        if (sHooked){ GlobalEvents.OnGoldFishEaten -= OnGold; sHooked = false; }
    }
}
