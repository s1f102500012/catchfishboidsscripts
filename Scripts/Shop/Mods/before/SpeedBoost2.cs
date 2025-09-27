using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/SpeedBoost2")]
public class SpeedBoost2 : PlayerModifier
{
    public float multiplier = 1.25f;          // Inspector 可调
    public override void Apply(PlayerController p)
        => p.maxSpeed *= multiplier;
}
