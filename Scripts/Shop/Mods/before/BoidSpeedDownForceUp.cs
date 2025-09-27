using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/Fish Speed×0.9 & Force×1.1")]
public class BoidSpeedDownForceUp : PlayerModifier
{
    public float speedMul = 0.9f;
    public float forceMul = 1.1f;

    public override void Apply(PlayerController player)
    {
        var bm = Object.FindFirstObjectByType<BoidManager>();
        if (!bm) return;

        // 影响未来生成
        bm.globalSpeedMult *= speedMul;
        bm.globalForceMult *= forceMul;

        // 影响场上现存
        if (bm.ActiveBoids != null)
        {
            foreach (var b in bm.ActiveBoids)
            {
                if (!b) continue;
                b.maxSpeed *= speedMul;
                b.maxForce *= forceMul;
            }
        }
    }
}
