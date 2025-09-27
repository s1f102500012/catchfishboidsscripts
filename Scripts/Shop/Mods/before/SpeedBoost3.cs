using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/SpeedBoost3")]
public class SpeedBoost3 : PlayerModifier
{
    public float multiplier = 1.5f;          // Inspector 可调
    public override void Apply(PlayerController p)
        => p.maxSpeed *= multiplier;
}
