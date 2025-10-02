using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/1002/5% Full Golden Wave")]
public class GoldenWaveChance5 : PlayerModifier
{
    public float additionalChance = 0.05f;

    public override void Apply(PlayerController player)
    {
        var bm = BoidManager.Instance;
        if (!bm)
            bm = Object.FindFirstObjectByType<BoidManager>();
        if (!bm) return;

        bm.fullGoldenWaveChance = Mathf.Clamp01(bm.fullGoldenWaveChance + additionalChance);
    }
}
