using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/Extra 1 Gold Per Wave")]
public class ExtraGoldPerWave : PlayerModifier
{
    public override void Apply(PlayerController player)
    {
        var bm = Object.FindFirstObjectByType<BoidManager>();
        if (bm) bm.extraGoldenPerWave += 1;     // 需要在 BoidManager 里新增该字段，见下文补丁
    }
}
