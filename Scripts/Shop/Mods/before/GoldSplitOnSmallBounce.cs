using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/0917/2.5% split GOLD when SMALL bounces")]
public class GoldSplitOnSmallBounce : PlayerModifier
{
    [Range(0f,1f)] public float baseChance = 0.025f;
    public float backOffset   = 0.6f;  // 生成点往反方向退
    public float initSpeedK   = 0.5f;  // 初速度系数

    static int sStacks = 0;
    static float sChance = 0.025f, sBack = 0.6f, sK = 0.5f;
    static bool sHooked = false;

    public override void Apply(PlayerController player)
    {
        sStacks++;
        sChance = baseChance; sBack = backOffset; sK = initSpeedK;
        if (!sHooked){ GlobalEvents.OnFishBoundaryBounce += OnBounce; sHooked = true; }
    }

    static void OnBounce(Boid b, Vector2 pos)
    {
        if (!b || b.isGolden) return;                 // 仅对白鱼生效
        var bm = BoidManager.Instance; if (!bm) return;

        float eff = 1f - Mathf.Pow(1f - sChance, sStacks);
        if (Random.value >= eff) return;

#if UNITY_2023_1_OR_NEWER
        Vector2 v = b.GetComponent<Rigidbody2D>().linearVelocity;
#else
        Vector2 v = b.GetComponent<Rigidbody2D>().velocity;
#endif
        Vector2 back = (v.sqrMagnitude > 1e-6f ? -v.normalized : Random.insideUnitCircle.normalized);
        Vector2 p2 = pos + back * sBack;

        var nb = Object.Instantiate(bm.boidPrefab, p2, Quaternion.identity, bm.transform);
        bm.SampleRandomScales(false, out float speedScale, out float forceScale, out float sizeScale);

        nb.SetRandomScales(speedScale, forceScale, sizeScale);
        nb.SetGlobalScales(bm.globalSpeedMult, bm.globalForceMult);
        nb.ConfigureAsGolden(bm.goldenSpeedMultiplier, bm.goldenForceMultiplier, bm.goldenScoreValue, bm.goldenColor);
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
        sChance = 0.025f; sBack = 0.6f; sK = 0.5f;
    }
}
