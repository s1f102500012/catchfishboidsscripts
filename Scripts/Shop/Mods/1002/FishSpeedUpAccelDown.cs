using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/1002/Fish Speed×1.2 & Force×0.8")]
public class FishSpeedUpAccelDown : PlayerModifier
{
    public float speedMultiplier = 1.2f;
    public float forceMultiplier = 0.8f;

    public override void Apply(PlayerController player)
    {
        var bm = BoidManager.Instance;
        if (!bm)
            bm = Object.FindFirstObjectByType<BoidManager>();
        if (!bm) return;

        bm.globalSpeedMult *= speedMultiplier;
        bm.globalForceMult *= forceMultiplier;

        if (bm.ActiveBoids != null)
        {
            foreach (var b in bm.ActiveBoids)
            {
                if (!b) continue;
                b.MultiplyGlobalScales(speedMultiplier, forceMultiplier);
            }
        }
    }
}
