using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/Double Gold Chance")]
public class DoubleNaturalGoldChance : PlayerModifier
{
    public float multiplier = 2f;

    public override void Apply(PlayerController player)
    {
        var mgr = Object.FindFirstObjectByType<BoidManager>();
        if (!mgr) return;

        // 对“当前概率”直接乘算；仅物理上限到 100%
        mgr.goldenChance = Mathf.Clamp01(mgr.goldenChance * multiplier);
    }
}
