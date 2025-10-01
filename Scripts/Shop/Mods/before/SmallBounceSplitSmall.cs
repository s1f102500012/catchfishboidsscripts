using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/0917/10% split SMALL when SMALL bounces")]
public class SmallBounceSplitSmall : PlayerModifier
{
    [Range(0f,1f)] public float baseChance = 0.10f;
    public float backOffset = 0.6f;
    public float initSpeedK = 0.5f;

    static int   sStacks = 0;
    static bool  sHooked = false;
    static float sChance = 0.10f, sBack = 0.6f, sK = 0.5f;

    public override void Apply(PlayerController player)
    {
        sStacks++;
        sChance = baseChance; sBack = backOffset; sK = initSpeedK;
        if (!sHooked){ GlobalEvents.OnFishBoundaryBounce += OnBounce; sHooked = true; }
    }

    static void OnBounce(Boid b, Vector2 pos)
    {
        if (!b || b.isGolden) return;
        var bm = BoidManager.Instance; if (!bm) return;

        float eff = 1f - Mathf.Pow(1f - sChance, sStacks);
        if (Random.value >= eff) return;

#if UNITY_2023_1_OR_NEWER
        Vector2 v = b.GetComponent<Rigidbody2D>().linearVelocity;
#else
        Vector2 v = b.GetComponent<Rigidbody2D>().velocity;
#endif
        Vector2 back = (v.sqrMagnitude > 1e-6f ? -v.normalized : Random.insideUnitCircle.normalized);
        Vector2 p2   = pos + back * sBack;

        var nb = Object.Instantiate(bm.boidPrefab, p2, Quaternion.identity, bm.transform);
        nb.isGolden = false;

        bm.SampleRandomScales(false, out float speedScale, out float forceScale, out float sizeScale);

        nb.SetRandomScales(speedScale, forceScale, sizeScale);
        nb.SetGlobalScales(bm.globalSpeedMult, bm.globalForceMult);
        nb.SetTier(b.fusionTier);
#if UNITY_2023_1_OR_NEWER
        nb.GetComponent<Rigidbody2D>().linearVelocity = back * (nb.maxSpeed * sK);
#else
        nb.GetComponent<Rigidbody2D>().velocity       = back * (nb.maxSpeed * sK);
#endif
        bm.ActiveBoids.Add(nb);
    }

    public static void HardReset()
    {
        sStacks = 0;
        if (sHooked){ GlobalEvents.OnFishBoundaryBounce -= OnBounce; sHooked = false; }
        sChance = 0.10f; sBack = 0.6f; sK = 0.5f;
    }
}
