using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/Fish Speed×1.1 & Force×0.9")]
public class BoidSpeedUpForceDown : PlayerModifier
{
    public float speedMul = 1.1f;
    public float forceMul = 0.9f;

    public override void Apply(PlayerController player)
    {
        var bm = Object.FindObjectOfType<BoidManager>();
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
