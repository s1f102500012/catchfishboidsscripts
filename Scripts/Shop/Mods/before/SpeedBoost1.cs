using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/SpeedBoost1")]
public class SpeedBoost1 : PlayerModifier
{
    public float multiplier = 1.1f;          // Inspector 可调
    public override void Apply(PlayerController p)
        => p.maxSpeed *= multiplier;
}
